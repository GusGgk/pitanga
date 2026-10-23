using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using ImageMagick;
using Pitanga.Core;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;

namespace Pitanga.Conversion;

/// <summary>Gerador mínimo de .docx (texto, quebras de página e imagens inline) via Open XML SDK.</summary>
public sealed class DocxWriter : IDisposable
{
    const long EmuPerPixel = 9525;         // 96 dpi
    const long MaxWidthEmu = 5_943_600;    // 6,5" — largura útil de uma página Carta/A4 com margens padrão
    const long MaxHeightEmu = 8_229_600;   // 9"

    readonly WordprocessingDocument _doc;
    readonly MainDocumentPart _main;
    readonly Body _body;
    uint _nextDrawingId = 1;

    public DocxWriter(string path)
    {
        _doc = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document);
        _main = _doc.AddMainDocumentPart();
        _main.Document = new Document(new Body());
        _body = _main.Document.Body!;
    }

    public void AddParagraph(string text) =>
        _body.AppendChild(new Paragraph(new Run(new Text(Sanitize(text)) { Space = SpaceProcessingModeValues.Preserve })));

    public void PageBreak() =>
        _body.AppendChild(new Paragraph(new Run(new Break { Type = BreakValues.Page })));

    public void AddImage(byte[] data, bool isPng)
    {
        var info = new MagickImageInfo(data);
        AddImage(data, isPng, (int)info.Width, (int)info.Height);
    }

    public void AddImage(byte[] data, bool isPng, int pixelWidth, int pixelHeight)
    {
        var part = _main.AddImagePart(isPng ? ImagePartType.Png : ImagePartType.Jpeg);
        using (var ms = new MemoryStream(data)) part.FeedData(ms);
        var relationshipId = _main.GetIdOfPart(part);

        long cx = pixelWidth * EmuPerPixel, cy = pixelHeight * EmuPerPixel;
        double fit = Math.Min(1.0, Math.Min((double)MaxWidthEmu / cx, (double)MaxHeightEmu / cy));
        cx = (long)(cx * fit);
        cy = (long)(cy * fit);
        uint id = _nextDrawingId++;

        var drawing = new Drawing(
            new DW.Inline(
                new DW.Extent { Cx = cx, Cy = cy },
                new DW.EffectExtent { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 0L },
                new DW.DocProperties { Id = id, Name = $"Picture {id}" },
                new DW.NonVisualGraphicFrameDrawingProperties(new A.GraphicFrameLocks { NoChangeAspect = true }),
                new A.Graphic(
                    new A.GraphicData(
                        new PIC.Picture(
                            new PIC.NonVisualPictureProperties(
                                new PIC.NonVisualDrawingProperties { Id = id, Name = $"image{id}" },
                                new PIC.NonVisualPictureDrawingProperties()),
                            new PIC.BlipFill(
                                new A.Blip { Embed = relationshipId },
                                new A.Stretch(new A.FillRectangle())),
                            new PIC.ShapeProperties(
                                new A.Transform2D(
                                    new A.Offset { X = 0L, Y = 0L },
                                    new A.Extents { Cx = cx, Cy = cy }),
                                new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle })))
                    { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" }))
            {
                DistanceFromTop = 0U, DistanceFromBottom = 0U, DistanceFromLeft = 0U, DistanceFromRight = 0U,
            });

        _body.AppendChild(new Paragraph(new Run(drawing)));
    }

    public void Dispose()
    {
        _main.Document?.Save();
        _doc.Dispose();
    }

    /// <summary>XML 1.0 não aceita a maioria dos caracteres de controle que PDFs às vezes trazem.</summary>
    static string Sanitize(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var ch in text)
            if (ch >= 0x20 || ch is '\t') sb.Append(ch);
        return sb.ToString();
    }

    // ------------------------------------------------------------ atalhos de alto nível

    public static string FromImages(IEnumerable<string> images, string output)
    {
        using var docx = new DocxWriter(output);
        foreach (var path in images)
        {
            using var img = new MagickImage(path);
            img.AutoOrient();
            // Fotos continuam JPEG (arquivo menor); o resto vira PNG (Word não lê WEBP/HEIC/AVIF).
            bool keepJpeg = img.Format == MagickFormat.Jpeg;
            if (keepJpeg) img.Quality = 92;
            var data = img.ToByteArray(keepJpeg ? MagickFormat.Jpeg : MagickFormat.Png);
            docx.AddImage(data, isPng: !keepJpeg, (int)img.Width, (int)img.Height);
        }
        return output;
    }

    public static string FromText(string source)
    {
        var output = OutputPaths.ChangeExtension(source, ".docx");
        using var docx = new DocxWriter(output);
        foreach (var line in File.ReadAllLines(source)) docx.AddParagraph(line);
        return output;
    }

    public static string ToText(string source)
    {
        var output = OutputPaths.ChangeExtension(source, ".txt");
        using var doc = WordprocessingDocument.Open(source, false);
        var paragraphs = doc.MainDocumentPart?.Document?.Body?.Descendants<Paragraph>().Select(p => p.InnerText)
                         ?? Enumerable.Empty<string>();
        File.WriteAllLines(output, paragraphs, new UTF8Encoding(true));
        return output;
    }
}
