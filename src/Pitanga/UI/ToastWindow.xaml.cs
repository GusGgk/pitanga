using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Pitanga.Interop;

namespace Pitanga.UI;

public enum ToastKind { Success, Error, Busy, Info }

/// <summary>Notificações no canto superior direito, no estilo dos banners do macOS.</summary>
public static class Toast
{
    static ToastWindow? _window;

    public static void Show(string title, string body, ToastKind kind = ToastKind.Success, string? revealPath = null)
    {
        var app = Application.Current;
        if (app == null) return;
        app.Dispatcher.Invoke(() =>
        {
            _window ??= new ToastWindow();
            _window.Present(title, body, kind, revealPath);
        });
    }

    public static void Error(Exception ex, string title = "Something went wrong") =>
        Show(title, ex.Message.Split('\n')[0], ToastKind.Error);

    /// <summary>Usado pelo processo worker para não sair enquanto a notificação ainda está na tela.</summary>
    public static bool IsShowing => _window?.IsVisible == true;
}

public partial class ToastWindow : Window
{
    static readonly Geometry Check = Freeze(Geometry.Parse("M10.5,17.5 L15,22 L24,12.5"));
    static readonly Geometry Bang = Freeze(Geometry.Parse("M17,9.5 V19 M17,24 V24.4"));
    static readonly Geometry Arc = Freeze(Geometry.Parse("M17,8.5 A8.5,8.5 0 1 1 8.5,17"));
    static readonly Geometry Info = Freeze(Geometry.Parse("M17,15 V24 M17,10 V10.4"));

    readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(3.5) };
    string? _revealPath;

    public ToastWindow()
    {
        InitializeComponent();
        _timer.Tick += (_, _) => Dismiss();
        SourceInitialized += (_, _) => WindowStyles.MakeNoActivate(this);
        MouseLeftButtonUp += (_, _) =>
        {
            if (_revealPath != null) WindowStyles.RevealInExplorer(_revealPath);
            Dismiss();
        };
    }

    public void Present(string title, string body, ToastKind kind, string? revealPath)
    {
        TitleText.Text = title;
        BodyText.Text = body;
        BodyText.Visibility = string.IsNullOrEmpty(body) ? Visibility.Collapsed : Visibility.Visible;
        _revealPath = revealPath;

        IconPath.Data = kind switch
        {
            ToastKind.Error => Bang,
            ToastKind.Busy => Arc,
            ToastKind.Info => Info,
            _ => Check,
        };
        IconBg.Fill = kind == ToastKind.Error
            ? new SolidColorBrush(Color.FromRgb(0xFF, 0x45, 0x3A))
            : (Brush)FindResource("AccentBrush");

        Spin.BeginAnimation(RotateTransform.AngleProperty, kind == ToastKind.Busy
            ? new DoubleAnimation(0, 360, TimeSpan.FromSeconds(0.9)) { RepeatBehavior = RepeatBehavior.Forever }
            : null);

        if (!IsVisible) Show();
        UpdateLayout();
        var area = SystemParameters.WorkArea;
        Left = area.Right - ActualWidth;
        Top = area.Top;

        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        Root.BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(180)));
        Slide.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(260)) { EasingFunction = ease });

        _timer.Stop();
        if (kind != ToastKind.Busy) _timer.Start();
    }

    void Dismiss()
    {
        _timer.Stop();
        var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(200));
        fade.Completed += (_, _) =>
        {
            if (Root.Opacity > 0.01) return; // reapresentado durante o fade
            Slide.BeginAnimation(TranslateTransform.XProperty, null);
            Slide.X = 36;
            Hide();
        };
        Root.BeginAnimation(OpacityProperty, fade);
    }

    static Geometry Freeze(Geometry g)
    {
        g.Freeze();
        return g;
    }
}
