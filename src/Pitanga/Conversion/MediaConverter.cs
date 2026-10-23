using System.Diagnostics;
using Pitanga.Core;

namespace Pitanga.Conversion;

/// <summary>
/// Vídeo/áudio via ffmpeg local (opcional). Coloque ffmpeg.exe ao lado do Pitanga.exe
/// ou no PATH; sem ele, o menu simplesmente não aparece para mídia.
/// </summary>
public static class MediaConverter
{
    static readonly Lazy<string?> Ffmpeg = new(Locate);

    static readonly Dictionary<string, (string Extension, string Arguments)> Presets = new()
    {
        ["MP4"] = (".mp4", "-c:v libx264 -preset veryfast -crf 23 -c:a aac -b:a 160k -movflags +faststart"),
        ["MOV"] = (".mov", "-c:v libx264 -preset veryfast -crf 20 -c:a aac -b:a 192k"),
        ["WEBM"] = (".webm", "-c:v libvpx-vp9 -b:v 0 -crf 32 -row-mt 1 -c:a libopus -b:a 128k"),
        ["GIF"] = (".gif", "-vf fps=12,scale=480:-1:flags=lanczos,split[a][b];[a]palettegen[p];[b][p]paletteuse -loop 0"),
        ["MP3"] = (".mp3", "-vn -c:a libmp3lame -q:a 2"),
        ["WAV"] = (".wav", "-vn -c:a pcm_s16le"),
        ["M4A"] = (".m4a", "-vn -c:a aac -b:a 192k"),
        ["FLAC"] = (".flac", "-vn -c:a flac"),
        ["OGG"] = (".ogg", "-vn -c:a libvorbis -q:a 5"),
    };

    public static bool IsAvailable => Ffmpeg.Value != null;

    public static async Task<string> ConvertAsync(string source, string target)
    {
        var exe = Ffmpeg.Value ?? throw new InvalidOperationException("ffmpeg.exe not found (place it next to Pitanga.exe or on PATH).");
        if (!Presets.TryGetValue(target, out var preset)) throw new NotSupportedException($"Media → {target} is not supported.");

        var output = OutputPaths.ChangeExtension(source, preset.Extension);
        var psi = new ProcessStartInfo(exe)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
        };
        foreach (var arg in new[] { "-hide_banner", "-loglevel", "error", "-y", "-i", source }) psi.ArgumentList.Add(arg);
        foreach (var arg in preset.Arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries)) psi.ArgumentList.Add(arg);
        psi.ArgumentList.Add(output);

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Could not start ffmpeg.");
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            try { File.Delete(output); } catch { }
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(stderr) ? $"ffmpeg exited with {process.ExitCode}" : stderr.Trim());
        }
        return output;
    }

    static string? Locate()
    {
        foreach (var candidate in new[]
                 {
                     Path.Combine(AppContext.BaseDirectory, "ffmpeg.exe"),
                     Path.Combine(AppContext.BaseDirectory, "tools", "ffmpeg.exe"),
                 })
        {
            if (File.Exists(candidate)) return candidate;
        }

        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var candidate = Path.Combine(dir.Trim('"'), "ffmpeg.exe");
                if (File.Exists(candidate)) return candidate;
            }
            catch { /* entrada inválida no PATH */ }
        }
        return null;
    }
}
