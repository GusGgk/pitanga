using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Pitanga.Conversion;
using Pitanga.Core;
using Pitanga.Menu;
using Pitanga.Tools;
using Pitanga.UI;

namespace Pitanga;

/// <summary>
/// Executa o item escolhido no menu. Todo o trabalho pesado roda num processo "worker"
/// (o próprio Pitanga.exe com --convert / --tool) que termina ao final: ImageMagick, Office,
/// PDF e janelas com GPU nunca ficam carregados no processo da bandeja, que segue leve (~50 MB).
/// </summary>
public static class ActionRunner
{
    [DllImport("user32.dll")]
    static extern bool AllowSetForegroundWindow(int dwProcessId);

    static string WorkerExe => Environment.ProcessPath ?? throw new InvalidOperationException("Cannot locate Pitanga.exe.");

    public static void Run(RadialItem item, string[] files)
    {
        if (item.Kind == ItemKind.Tool && ToolLauncher.HasWindow(item.Id))
        {
            OpenTool(item, files[0]);
            return;
        }
        _ = ConvertAsync(item.Id, files);
    }

    static void OpenTool(RadialItem item, string file)
    {
        try
        {
            AllowSetForegroundWindow(-1); // ASFW_ANY: deixa a janela da ferramenta vir para frente
            var psi = new ProcessStartInfo(WorkerExe) { UseShellExecute = false };
            foreach (var arg in new[] { "--tool", item.Id, file }) psi.ArgumentList.Add(arg);
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"tool {item.Id}");
            Toast.Error(ex, $"Could not open {item.Label}");
        }
    }

    public static async Task<IReadOnlyList<ConversionResult>> ConvertAsync(string target, IReadOnlyList<string> files)
    {
        try
        {
            var work = RunWorkerAsync(new[] { "--convert", target }.Concat(files));
            // Só mostra "Converting…" se demorar (Office, vídeo); conversões rápidas vão direto ao resultado.
            if (await Task.WhenAny(work, Task.Delay(700)) != work)
                Toast.Show($"Converting to {target}…", Path.GetFileName(files[0]), ToastKind.Busy);

            var (exitCode, stdout, stderr) = await work;
            if (exitCode != 0)
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(stderr) ? $"Worker exited with code {exitCode}." : stderr.Trim());

            var results = stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(line => line.Split('\t'))
                .Select(parts => new ConversionResult(parts[0], parts.Length > 1 ? parts[1] : null))
                .ToList();

            if (results.Count == 1)
            {
                var r = results[0];
                Toast.Show($"Saved {Path.GetFileName(r.OutputPath)}", r.Note ?? FolderName(r.OutputPath), ToastKind.Success, r.OutputPath);
            }
            else if (results.Count > 1)
            {
                Toast.Show($"{results.Count} files saved", FolderName(results[0].OutputPath), ToastKind.Success, results[0].OutputPath);
            }
            return results;
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"convert {target} [{string.Join(" | ", files)}]");
            Toast.Error(ex, $"{target} failed");
            return Array.Empty<ConversionResult>();
        }
    }

    static async Task<(int ExitCode, string Stdout, string Stderr)> RunWorkerAsync(IEnumerable<string> args)
    {
        var psi = new ProcessStartInfo(WorkerExe)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var arg in args) psi.ArgumentList.Add(arg);

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Could not start the conversion worker.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return (process.ExitCode, await stdout, await stderr);
    }

    static string FolderName(string path) => Path.GetFileName(Path.GetDirectoryName(path)) ?? "";
}
