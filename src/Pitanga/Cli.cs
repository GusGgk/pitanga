using System.Text;
using System.Windows;
using Pitanga.Conversion;
using Pitanga.Interop;
using Pitanga.Tools;
using Pitanga.UI;

namespace Pitanga;

/// <summary>
/// Pitanga.exe --convert JPG arquivo.pdf [mais...]   → converte sem UI e imprime os caminhos gerados
/// Pitanga.exe --tool CROP foto.jpg                  → abre só a janela da ferramenta
/// É assim que a bandeja delega o trabalho pesado (processo worker), e também serve para testes
/// e para integrar com "Enviar para" / menu de contexto.
/// </summary>
public static class Cli
{
    const string Usage = "Usage:\n  Pitanga --convert <FORMAT|COMPRESS|MERGE> <file> [file...]\n  Pitanga --tool <CROP|ADDBG|METADATA|EDIT|ANNOTATE|REDACT> <file>";

    // UTF-8 explícito: nomes como "Relatório.pdf" chegam intactos à bandeja.
    static readonly TextWriter Out = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false)) { AutoFlush = true };
    static readonly TextWriter Err = new StreamWriter(Console.OpenStandardError(), new UTF8Encoding(false)) { AutoFlush = true };

    public static async Task RunAsync(string[] args)
    {
        var app = Application.Current;
        NativeMethods.AttachConsole(NativeMethods.ATTACH_PARENT_PROCESS);
        bool toolMode = args[0].Equals("--tool", StringComparison.OrdinalIgnoreCase);

        try
        {
            switch (args[0].ToLowerInvariant())
            {
                case "--convert" when args.Length >= 3:
                {
                    var files = args.Skip(2).Select(Path.GetFullPath).ToArray();
                    var results = await ConversionService.ConvertAsync(files, args[1]);
                    foreach (var r in results) Out.WriteLine(r.Note is null ? r.OutputPath : $"{r.OutputPath}\t{r.Note}");
                    app.Shutdown(0);
                    return;
                }
                case "--tool" when args.Length >= 3:
                {
                    app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                    var window = ToolLauncher.Open(args[1].ToUpperInvariant(), Path.GetFullPath(args[2]));
                    window.Closed += async (_, _) => await ShutdownAfterToastAsync(app, 0);
                    return;
                }
                default:
                    Out.WriteLine(Usage);
                    app.Shutdown(2);
                    return;
            }
        }
        catch (Exception ex)
        {
            Err.WriteLine(ex.Message);
            Core.Log.Error(ex, "cli");
            if (toolMode)
            {
                // Ninguém lê o stderr de uma ferramenta aberta pela bandeja: avisa na tela.
                Toast.Error(ex, "Could not open the tool");
                await ShutdownAfterToastAsync(app, 1);
            }
            else
            {
                app.Shutdown(1);
            }
        }
    }

    /// <summary>A ferramenta mostra "Saved …" e fecha; o processo espera a notificação sumir antes de sair.</summary>
    static async Task ShutdownAfterToastAsync(Application app, int exitCode)
    {
        while (Toast.IsShowing) await Task.Delay(250);
        app.Shutdown(exitCode);
    }
}
