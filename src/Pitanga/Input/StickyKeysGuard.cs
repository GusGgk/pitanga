using System.Runtime.InteropServices;
using System.Windows.Threading;
using Pitanga.Core;

namespace Pitanga.Input;

/// <summary>
/// Pressionar Shift 5× seguidas abre o aviso "Teclas de Aderência" do Windows — e alternar os modos
/// do menu com Shift chega nisso facilmente. Enquanto o menu está aberto, desligamos APENAS o atalho
/// (não o recurso), exatamente como a Microsoft recomenda para jogos, e restauramos ao fechar.
///
/// - Se o usuário usa Teclas de Aderência (recurso ligado), não mexemos em nada.
/// - A mudança não é gravada no perfil (fWinIni = 0): mesmo se o app for morto, volta no próximo logon.
/// </summary>
public static class StickyKeysGuard
{
    const uint SPI_GETSTICKYKEYS = 0x003A;
    const uint SPI_SETSTICKYKEYS = 0x003B;
    const uint SKF_STICKYKEYSON = 0x00000001;
    const uint SKF_HOTKEYACTIVE = 0x00000004;
    const uint SKF_CONFIRMHOTKEY = 0x00000008;

    [StructLayout(LayoutKind.Sequential)]
    struct STICKYKEYS
    {
        public uint cbSize;
        public uint dwFlags;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool SystemParametersInfo(uint uiAction, uint uiParam, ref STICKYKEYS pvParam, uint fWinIni);

    static STICKYKEYS? _original;
    static DispatcherTimer? _restoreTimer;

    /// <summary>Chamado quando o menu abre.</summary>
    public static void Suspend()
    {
        _restoreTimer?.Stop();
        if (_original != null) return; // já suspenso

        var current = new STICKYKEYS { cbSize = (uint)Marshal.SizeOf<STICKYKEYS>() };
        if (!SystemParametersInfo(SPI_GETSTICKYKEYS, current.cbSize, ref current, 0)) return;
        if ((current.dwFlags & SKF_STICKYKEYSON) != 0) return;  // usuário usa o recurso: respeita
        if ((current.dwFlags & SKF_HOTKEYACTIVE) == 0) return;  // atalho já desligado pelo usuário

        var patched = current;
        patched.dwFlags &= ~(SKF_HOTKEYACTIVE | SKF_CONFIRMHOTKEY);
        if (SystemParametersInfo(SPI_SETSTICKYKEYS, patched.cbSize, ref patched, 0))
            _original = current;
        else
            Log.Info($"could not suspend Sticky Keys hotkey (error {Marshal.GetLastWin32Error()})");
    }

    /// <summary>
    /// Chamado quando o menu fecha. Espera um pouco: o Windows conta as 5 batidas numa janela curta,
    /// e um Shift logo depois de fechar o menu ainda somaria às batidas feitas dentro dele.
    /// </summary>
    public static void RestoreSoon()
    {
        if (_original == null) return;
        if (_restoreTimer == null)
        {
            _restoreTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _restoreTimer.Tick += (_, _) => Restore();
        }
        _restoreTimer.Stop();
        _restoreTimer.Start();
    }

    /// <summary>Restaura imediatamente (também chamado ao sair do app).</summary>
    public static void Restore()
    {
        _restoreTimer?.Stop();
        if (_original is not { } original) return;
        SystemParametersInfo(SPI_SETSTICKYKEYS, original.cbSize, ref original, 0);
        _original = null;
    }
}
