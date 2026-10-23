using Pitanga.Conversion;
using Pitanga.Core;

namespace Pitanga.Menu;

public enum ItemKind { Convert, Tool, Merge }
public enum MenuMode { Convert, Tools }

/// <param name="Id">Identificador da ação ("JPG", "CROP", "MERGE"...).</param>
/// <param name="Label">Texto exibido na fatia.</param>
/// <param name="Icon">Chave em <see cref="Icons"/>; null = fatia só com texto (formatos).</param>
public sealed record RadialItem(string Id, string Label, ItemKind Kind, string? Icon = null);

/// <summary>Decide o que aparece no menu para cada tipo de arquivo e modo.</summary>
public static class MenuCatalog
{
    static readonly string[] ImageTargets = { "JPG", "PNG", "WEBP", "HEIC", "AVIF", "BMP", "TIFF", "PDF", "DOCX" };
    static readonly string[] PdfTargets = { "DOCX", "JPG", "PNG", "TXT" };
    static readonly string[] VideoTargets = { "MP4", "MOV", "WEBM", "GIF", "MP3" };
    static readonly string[] AudioTargets = { "MP3", "WAV", "M4A", "FLAC", "OGG" };

    static readonly RadialItem[] ImageTools =
    {
        new("CROP", "CROP", ItemKind.Tool, "crop"),
        new("COMPRESS", "COMPRESS", ItemKind.Tool, "compress"),
        new("METADATA", "METADATA", ItemKind.Tool, "metadata"),
        new("EDIT", "EDIT", ItemKind.Tool, "edit"),
        new("ANNOTATE", "ANNOTATE", ItemKind.Tool, "annotate"),
        new("ADDBG", "ADD BG", ItemKind.Tool, "addbg"),
        new("REDACT", "REDACT", ItemKind.Tool, "redact"),
    };

    /// <summary>Tipos que têm um segundo menu (alternado tocando Shift de novo).</summary>
    public static bool HasToolsMode(FileKind kind) => kind == FileKind.Image;

    public static IReadOnlyList<RadialItem> For(FileKind kind, IReadOnlyList<string> files, MenuMode mode)
    {
        if (files.Count == 0) return Array.Empty<RadialItem>();
        var source = FileKinds.FormatId(files[0]);

        switch (kind)
        {
            case FileKind.Image when mode == MenuMode.Tools:
                return ImageTools;

            case FileKind.Image:
                return Formats(ImageTargets.Where(t => t != source));

            case FileKind.Pdf:
                var pdf = Formats(PdfTargets);
                if (files.Count > 1) pdf.Add(new RadialItem("MERGE", "MERGE", ItemKind.Merge, "merge"));
                return pdf;

            case FileKind.Video when MediaConverter.IsAvailable:
                return Formats(VideoTargets.Where(t => t != source));

            case FileKind.Audio when MediaConverter.IsAvailable:
                return Formats(AudioTargets.Where(t => t != source));

            case FileKind.Text:
                return Formats(OfficeConverter.CanConvert(kind) ? new[] { "PDF", "DOCX" } : new[] { "DOCX" });

            case FileKind.Word when OfficeConverter.CanConvert(kind):
                return Formats(new[] { "PDF", "DOCX", "JPG", "PNG", "TXT" }.Where(t => t != source));

            case FileKind.Word when source == "DOCX":
                return Formats(new[] { "TXT" }); // sem Office: só extração de texto (Open XML)

            case FileKind.Spreadsheet when OfficeConverter.CanConvert(kind):
                return Formats(new[] { "PDF", "XLSX", "CSV" }.Where(t => t != source));

            case FileKind.Presentation when OfficeConverter.CanConvert(kind):
                return Formats(new[] { "PDF", "PPTX", "JPG", "PNG" }.Where(t => t != source));

            default:
                return Array.Empty<RadialItem>();
        }
    }

    static List<RadialItem> Formats(IEnumerable<string> ids) =>
        ids.Select(id => new RadialItem(id, id, ItemKind.Convert)).ToList();
}
