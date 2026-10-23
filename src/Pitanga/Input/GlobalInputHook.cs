using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using Pitanga.Interop;
using static Pitanga.Interop.NativeMethods;

namespace Pitanga.Input;

/// <summary>
/// Hooks globais de baixo nível (WH_MOUSE_LL + WH_KEYBOARD_LL) numa thread dedicada.
///
/// Por que uma thread própria: o Windows chama hooks LL na thread que os instalou e espera a
/// resposta em ~300 ms. Se a thread de UI estiver ocupada (renderizando, convertendo),
/// o mouse do sistema inteiro "engasga". Aqui a thread só faz contas triviais e repassa
/// eventos para a UI com BeginInvoke — o callback retorna em microssegundos.
/// </summary>
public sealed class GlobalInputHook : IDisposable
{
    readonly Dispatcher _ui;
    Thread? _thread;
    uint _threadId;
    IntPtr _keyboardHook, _mouseHook;
    HookProc? _keyboardProc, _mouseProc; // referências mantidas para o GC não coletar os delegates

    // Estado acessado apenas pela thread do hook
    bool _leftDown, _dragging, _shiftHeld;
    int _downX, _downY, _dragThreshold;

    /// <summary>Shift pressionado (borda de descida) enquanto o botão esquerdo arrasta algo.</summary>
    public event Action? ShiftTappedWhileDragging;
    public event Action? LeftButtonReleased;
    public event Action? EscapePressed;

    public GlobalInputHook(Dispatcher ui) => _ui = ui;

    public void Start()
    {
        _dragThreshold = Math.Max(6, Math.Max(GetSystemMetrics(SM_CXDRAG), GetSystemMetrics(SM_CYDRAG)));
        int error = 0;
        using var ready = new ManualResetEventSlim();

        _thread = new Thread(() =>
        {
            _threadId = GetCurrentThreadId();
            _keyboardProc = KeyboardProc;
            _mouseProc = MouseProc;
            var module = GetModuleHandle(null);
            _keyboardHook = SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardProc, module, 0);
            _mouseHook = SetWindowsHookEx(WH_MOUSE_LL, _mouseProc, module, 0);
            if (_keyboardHook == IntPtr.Zero || _mouseHook == IntPtr.Zero) error = Marshal.GetLastWin32Error();
            ready.Set();

            while (GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
            {
                TranslateMessage(ref msg);
                DispatchMessage(ref msg);
            }

            if (_keyboardHook != IntPtr.Zero) UnhookWindowsHookEx(_keyboardHook);
            if (_mouseHook != IntPtr.Zero) UnhookWindowsHookEx(_mouseHook);
        })
        {
            IsBackground = true,
            Name = "Pitanga.InputHook",
            Priority = ThreadPriority.AboveNormal,
        };
        _thread.Start();
        ready.Wait();

        if (error != 0) throw new Win32Exception(error, "Failed to install global input hooks.");
    }

    IntPtr MouseProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            switch ((int)wParam)
            {
                case WM_LBUTTONDOWN:
                {
                    var data = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                    _leftDown = true;
                    _dragging = false;
                    _downX = data.pt.X;
                    _downY = data.pt.Y;
                    break;
                }
                case WM_MOUSEMOVE when _leftDown && !_dragging:
                {
                    var data = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                    if (Math.Abs(data.pt.X - _downX) > _dragThreshold || Math.Abs(data.pt.Y - _downY) > _dragThreshold)
                        _dragging = true;
                    break;
                }
                case WM_LBUTTONUP:
                    _leftDown = false;
                    _dragging = false;
                    Post(LeftButtonReleased);
                    break;
            }
        }
        return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }

    IntPtr KeyboardProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int msg = (int)wParam;
            bool down = msg is WM_KEYDOWN or WM_SYSKEYDOWN;
            bool up = msg is WM_KEYUP or WM_SYSKEYUP;
            var vk = (int)Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam).vkCode;

            if (vk is VK_SHIFT or VK_LSHIFT or VK_RSHIFT)
            {
                if (down && !_shiftHeld) // ignora auto-repeat
                {
                    _shiftHeld = true;
                    if (_dragging) Post(ShiftTappedWhileDragging);
                }
                else if (up)
                {
                    _shiftHeld = false;
                }
            }
            else if (vk == VK_ESCAPE && down)
            {
                Post(EscapePressed);
            }
        }
        // Nunca engolimos teclas: o resto do sistema continua recebendo tudo normalmente.
        return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }

    void Post(Action? handler)
    {
        if (handler != null) _ui.BeginInvoke(DispatcherPriority.Send, handler);
    }

    public void Dispose()
    {
        if (_thread == null) return;
        PostThreadMessage(_threadId, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
        _thread.Join(500);
        _thread = null;
    }
}
