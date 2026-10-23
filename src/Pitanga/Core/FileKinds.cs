namespace Pitanga.Core;

public enum FileKind { Unknown, Image, Pdf, Video, Audio, Text, Word, Spreadsheet, Presentation }

public static class FileKinds
{
    static readonly Dictionary<string, string> ImageFormats = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = "JPG", [".jpeg"] = "JPG", [".jfif"] = "JPG",
        [".png"] = "PNG", [".webp"] = "WEBP",
        [".heic"] = "HEIC", [".heif"] = "HEIC", [".avif"] = "AVIF",
        [".bmp"] = "BMP", [".tif"] = "TIFF", [".tiff"] = "TIFF",
        [".gif"] = "GIF", [".ico"] = "ICO",
    };

    static readonly HashSet<string> VideoExt = new(StringComparer.OrdinalIgnoreCase)
        { ".mp4", ".mov", ".mkv", ".avi", ".webm", ".m4v", ".wmv" };

    static readonly HashSet<string> AudioExt = new(StringComparer.OrdinalIgnoreCase)
        { ".mp3", ".wav", ".m4a", ".aac", ".flac", ".ogg", ".opus", ".wma" };

    static readonly HashSet<string> WordExt = new(StringComparer.OrdinalIgnoreCase)
        { ".docx", ".doc", ".docm", ".rtf", ".odt" };

    static readonly HashSet<string> SpreadsheetExt = new(StringComparer.OrdinalIgnoreCase)
        { ".xlsx", ".xls", ".xlsm", ".csv", ".ods" };

    static readonly HashSet<string> PresentationExt = new(StringComparer.OrdinalIgnoreCase)
        { ".pptx", ".ppt", ".pptm", ".odp" };

    public static FileKind Classify(string path)
    {
        var ext = Path.GetExtension(path);
        if (ImageFormats.ContainsKey(ext)) return FileKind.Image;
        if (ext.Equals(".pdf", StringComparison.OrdinalIgnoreCase)) return FileKind.Pdf;
        if (VideoExt.Contains(ext)) return FileKind.Video;
        if (AudioExt.Contains(ext)) return FileKind.Audio;
        if (ext.Equals(".txt", StringComparison.OrdinalIgnoreCase) || ext.Equals(".md", StringComparison.OrdinalIgnoreCase)) return FileKind.Text;
        if (WordExt.Contains(ext)) return FileKind.Word;
        if (SpreadsheetExt.Contains(ext)) return FileKind.Spreadsheet;
        if (PresentationExt.Contains(ext)) return FileKind.Presentation;
        return FileKind.Unknown;
    }

    /// <summary>Identificador curto do formato ("JPG", "PDF", "MP4"...), usado no menu e no glifo central.</summary>
    public static string FormatId(string path)
    {
        var ext = Path.GetExtension(path);
        return ImageFormats.TryGetValue(ext, out var id) ? id : ext.TrimStart('.').ToUpperInvariant();
    }

    public static bool SupportsAlpha(string extension) =>
        extension.ToLowerInvariant() is ".png" or ".webp" or ".avif" or ".heic" or ".heif" or ".tif" or ".tiff" or ".gif";
}
