namespace Pitanga.Core;

public static class Log
{
    static readonly object Gate = new();

    public static string Directory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Pitanga");

    public static void Error(Exception ex, string? context = null) =>
        Write($"ERROR {context}: {ex}");

    public static void Info(string message) => Write($"INFO  {message}");

    static void Write(string line)
    {
        try
        {
            lock (Gate)
            {
                System.IO.Directory.CreateDirectory(Directory);
                File.AppendAllText(Path.Combine(Directory, "pitanga.log"), $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {line}{Environment.NewLine}");
            }
        }
        catch { /* log nunca derruba o app */ }
    }
}
