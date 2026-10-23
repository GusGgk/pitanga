using System.Windows;
using System.Windows.Threading;
using Pitanga.Core;
using Pitanga.Input;
using Pitanga.Menu;
using Pitanga.UI;

namespace Pitanga;

public partial class App : Application
{
    Mutex? _singleInstance;
    GlobalInputHook? _hook;
    RadialMenuWindow? _menu;
    TrayIcon? _tray;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += (_, args) =>
        {
            Log.Error(args.Exception, "dispatcher");
            args.Handled = true;
        };

        // Modo linha de comando: --convert / --tool (útil para testes e para "Enviar para")
        if (e.Args.Length > 0)
        {
            await Cli.RunAsync(e.Args);
            return;
        }

        _singleInstance = new Mutex(true, @"Local\Pitanga.SingleInstance", out bool isFirst);
        if (!isFirst)
        {
            Shutdown();
            return;
        }

        // Processo da bandeja: só menu, notificações e menu do ícone — nada que precise de GPU.
        // Renderizar em software evita carregar o driver Direct3D (~70 MB de RAM o dia todo).
        // As janelas de ferramenta rodam em processos worker, com aceleração de GPU normal.
        System.Windows.Media.RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;

        try
        {
            _menu = new RadialMenuWindow();
            _menu.EnsureCreated();
            _menu.ItemChosen += ActionRunner.Run;

            _hook = new GlobalInputHook(Dispatcher);
            _hook.ShiftTappedWhileDragging += _menu.OpenOrToggle;
            _hook.LeftButtonReleased += OnLeftButtonReleased;
            _hook.EscapePressed += () => _menu.CloseMenu();
            _hook.Start();

            _tray = new TrayIcon();
        }
        catch (Exception ex)
        {
            // Sem isso, uma falha aqui deixava um processo "zumbi": vivo, sem menu e sem ícone.
            Log.Error(ex, "startup");
            MessageBox.Show($"Pitanga could not start.\n\n{ex.Message}\n\nLog: {Log.Directory}",
                "Pitanga", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
            return;
        }

        Toast.Show("Pitanga is running", "Drag a file and tap Shift.", ToastKind.Info);
        Log.Info("started");

        // Uma linha de memória no log, 1 min após iniciar: ajuda a diagnosticar regressões.
        var memoryCheck = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        memoryCheck.Tick += (_, _) =>
        {
            memoryCheck.Stop();
            using var self = System.Diagnostics.Process.GetCurrentProcess();
            Log.Info($"memory: private {self.PrivateMemorySize64 >> 20} MB, ws {self.WorkingSet64 >> 20} MB, managed {GC.GetTotalMemory(false) >> 20} MB");
        };
        memoryCheck.Start();
    }

    void OnLeftButtonReleased()
    {
        if (_menu is not { IsOpen: true }) return;

        // O hook vê o "mouse up" antes do loop OLE entregar o Drop.
        // Esperamos um pouco: se o Drop chegar, ele mesmo fecha o menu; senão foi um cancelamento.
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            if (_menu.IsOpen) _menu.CloseMenu();
        };
        timer.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hook?.Dispose();
        StickyKeysGuard.Restore();
        _tray?.Dispose();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}
