using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using Pitanga.Core;

namespace Pitanga.UI;

/// <summary>
/// Ícone na bandeja via Shell_NotifyIcon direto, com menu em WPF no estilo macOS.
/// Antes usava o NotifyIcon do WinForms, que sozinho custava ~16 MB de RAM só para existir.
/// </summary>
public sealed class TrayIcon : IDisposable
{
    const int WM_APP_TRAY = 0x8000 + 1;
    const int WM_LBUTTONUP = 0x0202, WM_RBUTTONUP = 0x0205, WM_CONTEXTMENU = 0x007B;
    const int NIN_SELECT = 0x0400, NIN_KEYSELECT = 0x0401;
    const uint NIM_ADD = 0, NIM_DELETE = 2, NIM_SETVERSION = 4;
    const uint NIF_MESSAGE = 0x01, NIF_ICON = 0x02, NIF_TIP = 0x04, NIF_SHOWTIP = 0x80;
    const uint NOTIFYICON_VERSION_4 = 4;
    const string Tooltip = "Pitanga — drag a file, tap Shift";

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct NOTIFYICONDATA
    {
        public int cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szTip;
        public uint dwState;
        public uint dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szInfo;
        public uint uVersion; // união com uTimeout
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string szInfoTitle;
        public uint dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool Shell_NotifyIcon(uint dwMessage, ref NOTIFYICONDATA lpData);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    static extern uint ExtractIconEx(string lpszFile, int nIconIndex, IntPtr[]? phiconLarge, IntPtr[] phiconSmall, uint nIcons);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern uint RegisterWindowMessage(string lpString);

    readonly HwndSource _window;
    readonly ContextMenu _menu;
    readonly uint _taskbarCreated;
    readonly IntPtr _icon;

    public TrayIcon()
    {
        // Janela invisível que só recebe as mensagens do ícone.
        _window = new HwndSource(new HwndSourceParameters("PitangaTray")
        {
            WindowStyle = unchecked((int)0x80000000), // WS_POPUP, nunca mostrada
            ExtendedWindowStyle = 0x00000080,         // WS_EX_TOOLWINDOW
            Width = 0,
            Height = 0,
        });
        _window.AddHook(WndProc);
        _taskbarCreated = RegisterWindowMessage("TaskbarCreated");
        _icon = LoadSmallIcon();
        _menu = BuildMenu();
        Add();
    }

    /// <summary>Ícone pequeno embutido no próprio .exe (Assets/Pitanga.ico), no tamanho da bandeja.</summary>
    static IntPtr LoadSmallIcon()
    {
        var small = new IntPtr[1];
        return Environment.ProcessPath is { } exe && ExtractIconEx(exe, 0, null, small, 1) > 0 ? small[0] : IntPtr.Zero;
    }

    NOTIFYICONDATA Data() => new()
    {
        cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
        hWnd = _window.Handle,
        uID = 1,
        uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP | NIF_SHOWTIP,
        uCallbackMessage = WM_APP_TRAY,
        hIcon = _icon,
        szTip = Tooltip,
        szInfo = "",
        szInfoTitle = "",
    };

    void Add()
    {
        var data = Data();
        Shell_NotifyIcon(NIM_ADD, ref data);
        data.uVersion = NOTIFYICON_VERSION_4;
        Shell_NotifyIcon(NIM_SETVERSION, ref data);
    }

    IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_APP_TRAY)
        {
            int ev = (int)(lParam.ToInt64() & 0xFFFF);
            if (ev is WM_CONTEXTMENU or WM_RBUTTONUP or WM_LBUTTONUP or NIN_SELECT or NIN_KEYSELECT) ShowMenu();
            handled = true;
        }
        else if (msg == (int)_taskbarCreated)
        {
            Add(); // o Explorer reiniciou e perdeu os ícones da bandeja
        }
        return IntPtr.Zero;
    }

    void ShowMenu()
    {
        if (_menu.IsOpen) return;
        SetForegroundWindow(_window.Handle); // sem isso o menu não fecha ao clicar fora
        _menu.IsOpen = true;
    }

    ContextMenu BuildMenu()
    {
        var itemStyle = (Style)Application.Current.FindResource("MacMenuItem");
        var separatorStyle = (Style)Application.Current.FindResource("MacMenuSeparator");
        MenuItem Item(string header) => new() { Header = header, Style = itemStyle };
        Separator Line() => new() { Style = separatorStyle };

        var title = Item("Pitanga — drag a file + Shift");
        title.IsEnabled = false;

        var allPages = Item("PDF → image: export all pages");
        allPages.IsCheckable = true;
        allPages.IsChecked = AppSettings.Current.PdfAllPages;
        allPages.Click += (_, _) =>
        {
            AppSettings.Current.PdfAllPages = allPages.IsChecked;
            AppSettings.Save();
        };

        var startup = Item("Start with Windows");
        startup.IsCheckable = true;
        startup.IsChecked = StartWithWindows.IsEnabled;
        startup.Click += (_, _) => StartWithWindows.Set(startup.IsChecked);

        var log = Item("Open log folder");
        log.Click += (_, _) =>
        {
            Directory.CreateDirectory(Log.Directory);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Log.Directory) { UseShellExecute = true });
        };

        var quit = Item("Quit Pitanga");
        quit.Click += (_, _) => Application.Current.Shutdown();

        var menu = new ContextMenu
        {
            Style = (Style)Application.Current.FindResource("MacContextMenu"),
            Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint,
        };
        foreach (var element in new Control[] { title, Line(), allPages, startup, log, Line(), quit })
            menu.Items.Add(element);
        return menu;
    }

    public void Dispose()
    {
        var data = Data();
        Shell_NotifyIcon(NIM_DELETE, ref data);
        if (_icon != IntPtr.Zero) DestroyIcon(_icon);
        _window.Dispose();
    }
}
