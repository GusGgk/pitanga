using System.Diagnostics;
using System.Windows;
using System.Windows.Interop;

namespace Pitanga.Interop;

internal static class WindowStyles
{
    /// <summary>Janela que nunca rouba o foco e não aparece no Alt+Tab.</summary>
    public static void MakeNoActivate(IntPtr hwnd)
    {
        var ex = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE).ToInt64();
        ex |= NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_TOOLWINDOW;
        NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE, new IntPtr(ex));
    }

    public static void MakeNoActivate(Window window) =>
        MakeNoActivate(new WindowInteropHelper(window).EnsureHandle());

    public static void RevealInExplorer(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
        }
        catch { /* não crítico */ }
    }
}
