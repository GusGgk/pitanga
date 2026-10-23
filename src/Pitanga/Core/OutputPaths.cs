namespace Pitanga.Core;

/// <summary>Regras de nome dos arquivos gerados: sempre no mesmo diretório do original, nunca sobrescreve.</summary>
public static class OutputPaths
{
    /// <summary>foto.png → foto.jpg (ou "foto (2).jpg" se já existir).</summary>
    public static string ChangeExtension(string source, string newExtension) =>
        Unique(Path.Combine(DirectoryOf(source), Path.GetFileNameWithoutExtension(source) + newExtension));

    /// <summary>foto.jpg → foto_Cropped.jpg</summary>
    public static string WithSuffix(string source, string suffix, string? newExtension = null) =>
        Unique(Path.Combine(DirectoryOf(source),
            Path.GetFileNameWithoutExtension(source) + suffix + (newExtension ?? Path.GetExtension(source))));

    public static string Unique(string path)
    {
        if (!File.Exists(path) && !Directory.Exists(path)) return path;
        var dir = DirectoryOf(path);
        var name = Path.GetFileNameWithoutExtension(path);
        var ext = Path.GetExtension(path);
        for (int i = 2; ; i++)
        {
            var candidate = Path.Combine(dir, $"{name} ({i}){ext}");
            if (!File.Exists(candidate)) return candidate;
        }
    }

    static string DirectoryOf(string path) => Path.GetDirectoryName(Path.GetFullPath(path)) ?? Environment.CurrentDirectory;

    public static string HumanSize(long bytes) => bytes switch
    {
        >= 1L << 30 => $"{bytes / (double)(1L << 30):0.0} GB",
        >= 1L << 20 => $"{bytes / (double)(1L << 20):0.0} MB",
        >= 1L << 10 => $"{bytes / (double)(1L << 10):0} KB",
        _ => $"{bytes} B",
    };
}
