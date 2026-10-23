using System.Text;
using Pitanga.Core;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.PageSegmenter;
using UglyToad.PdfPig.DocumentLayoutAnalysis.ReadingOrderDetector;
using UglyToad.PdfPig.DocumentLayoutAnalysis.WordExtractor;
using UglyToad.PdfPig.Writer;
using Windows.Storage;
using Windows.Storage.Streams;
using WinPdf = Windows.Data.Pdf;

namespace Pitanga.Conversion;

/// <summary>
/// PDF → imagem: renderizador NATIVO do Windows (Windows.Data.Pdf, o mesmo do Edge/Fotos) — sem Ghostscript.
/// PDF → texto/DOCX: PdfPig (análise de layout em blocos + ordem de leitura).
/// </summary>
public static class PdfConverter
{
    public static async Task<IReadOnlyList<string>> ConvertAsync(string source, string target) => target switch
    {
        "JPG" => await RenderAsync(source, jpeg: true),
        "PNG" => await RenderAsync(source, jpeg: false),
        "TXT" => new[] { ExtractText(source) },
        "DOCX" => new[] { await ToDocxAsync(source) },
        _ => throw new NotSupportedException($"PDF → {target} is not supported."),
    };

    // ------------------------------------------------------------ renderização nativa

    static async Task<WinPdf.PdfDocument> OpenAsync(string path)
    {
        var file = await StorageFile.GetFileFromPathAsync(Path.GetFullPath(path));
        return await WinPdf.PdfDocument.LoadFromFileAsync(file);
    }

    public static async Task<byte[]> RenderPageAsync(WinPdf.PdfDocument doc, uint pageIndex, bool jpeg, double dpi)
    {
        using var page = doc.GetPage(pageIndex);
        double scale = dpi / 96.0; // page.Size está em DIPs (1/96")
        var options = new WinPdf.PdfPageRenderOptions
        {
            DestinationWidth = (uint)Math.Max(1, Math.Round(page.Size.Width * scale)),
            DestinationHeight = (uint)Math.Max(1, Math.Round(page.Size.Height * scale)),
            BackgroundColor = new Windows.UI.Color { A = 255, R = 255, G = 255, B = 255 },
            BitmapEncoderId = jpeg
                ? Windows.Graphics.Imaging.BitmapEncoder.JpegEncoderId
                : Windows.Graphics.Imaging.BitmapEncoder.PngEncoderId,
        };

        using var stream = new InMemoryRandomAccessStream();
        await page.RenderToStreamAsync(stream, options);
        stream.Seek(0);
        using var buffer = new MemoryStream();
        await stream.AsStreamForRead().CopyToAsync(buffer);
        return buffer.ToArray();
    }

    /// <param name="nameSource">Arquivo cujo nome/pasta batizam a saída (ex.: o .docx original quando
    /// <paramref name="source"/> é um PDF temporário). Padrão: o próprio PDF.</param>
    public static async Task<IReadOnlyList<string>> RenderAsync(string source, bool jpeg, string? nameSource = null)
    {
        var doc = await OpenAsync(source);
        var naming = nameSource ?? source;
        var ext = jpeg ? ".jpg" : ".png";
        int dpi = AppSettings.Current.PdfRenderDpi;

        if (doc.PageCount <= 1 || !AppSettings.Current.PdfAllPages)
        {
            var output = OutputPaths.ChangeExtension(naming, ext);
            await File.WriteAllBytesAsync(output, await RenderPageAsync(doc, 0, jpeg, dpi));
            return new[] { output };
        }

        var outputs = new List<string>();
        int digits = doc.PageCount.ToString().Length;
        for (uint i = 0; i < doc.PageCount; i++)
        {
            var output = OutputPaths.WithSuffix(naming, "_p" + (i + 1).ToString().PadLeft(digits, '0'), ext);
            await File.WriteAllBytesAsync(output, await RenderPageAsync(doc, i, jpeg, dpi));
            outputs.Add(output);
        }
        return outputs;
    }

    // ------------------------------------------------------------ texto

    /// <summary>Blocos de texto por página, em ordem de leitura.</summary>
    static List<List<string>> ReadBlocks(string source)
    {
        var pages = new List<List<string>>();
        using var doc = PdfDocument.Open(source);
        foreach (var page in doc.GetPages())
        {
            var words = NearestNeighbourWordExtractor.Instance.GetWords(page.Letters);
            var blocks = DocstrumBoundingBoxes.Instance.GetBlocks(words);
            var ordered = UnsupervisedReadingOrderDetector.Instance.Get(blocks);
            pages.Add(ordered.Select(b => b.Text).Where(t => !string.IsNullOrWhiteSpace(t)).ToList());
        }
        return pages;
    }

    static string ExtractText(string source)
    {
        var output = OutputPaths.ChangeExtension(source, ".txt");
        var sb = new StringBuilder();
        var pages = ReadBlocks(source);
        for (int i = 0; i < pages.Count; i++)
        {
            if (i > 0) sb.AppendLine().AppendLine($"──────── page {i + 1} ────────").AppendLine();
            foreach (var block in pages[i]) sb.AppendLine(block).AppendLine();
        }
        File.WriteAllText(output, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        return output;
    }

    /// <summary>
    /// Texto → parágrafos editáveis. Páginas sem texto (PDF escaneado) entram como imagem,
    /// para nada se perder. Fidelidade de layout total exigiria um motor como o do Word.
    /// </summary>
    static async Task<string> ToDocxAsync(string source)
    {
        var output = OutputPaths.ChangeExtension(source, ".docx");
        var pages = ReadBlocks(source);
        WinPdf.PdfDocument? rendered = null;

        using (var docx = new DocxWriter(output))
        {
            for (int i = 0; i < pages.Count; i++)
            {
                if (i > 0) docx.PageBreak();
                if (pages[i].Count == 0)
                {
                    rendered ??= await OpenAsync(source);
                    var png = await RenderPageAsync(rendered, (uint)i, jpeg: false, dpi: 150);
                    docx.AddImage(png, isPng: true);
                }
                else
                {
                    foreach (var block in pages[i]) docx.AddParagraph(block.Replace('\n', ' '));
                }
            }
        }
        return output;
    }

    // ------------------------------------------------------------ merge

    public static string Merge(IReadOnlyList<string> sources)
    {
        var ordered = sources.OrderBy(Path.GetFileName, StringComparer.CurrentCultureIgnoreCase).ToArray();
        var output = OutputPaths.WithSuffix(ordered[0], "_Merged", ".pdf");
        File.WriteAllBytes(output, PdfMerger.Merge(ordered));
        return output;
    }
}
