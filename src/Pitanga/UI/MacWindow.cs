using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Pitanga.UI;

/// <summary>
/// Base das janelas de ferramenta: cantos arredondados, sombra suave e "semáforos" à esquerda.
/// O visual vem de MacWindowStyle (Themes/Theme.xaml).
/// </summary>
public class MacWindow : Window
{
    Rect? _restoreBounds;

    public MacWindow()
    {
        Style = (Style)Application.Current.FindResource("MacWindowStyle");
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape && Keyboard.FocusedElement is not TextBox) Close();
        };
    }

    /// <summary>Em telas pequenas / escala alta, encolhe a janela para caber na área de trabalho.</summary>
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var area = SystemParameters.WorkArea;
        const double shadowAllowance = 24; // a margem transparente da sombra pode sair da tela
        double maxW = area.Width + shadowAllowance, maxH = area.Height + shadowAllowance;
        MinWidth = Math.Min(MinWidth, maxW);
        MinHeight = Math.Min(MinHeight, maxH);
        if (Width > maxW) Width = maxW;
        if (Height > maxH) Height = maxH;
        Left = area.Left + (area.Width - Width) / 2;
        Top = area.Top + (area.Height - Height) / 2;
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        if (GetTemplateChild("PART_Close") is Button close) close.Click += (_, _) => Close();
        if (GetTemplateChild("PART_Minimize") is Button minimize) minimize.Click += (_, _) => WindowState = WindowState.Minimized;
        if (GetTemplateChild("PART_Zoom") is Button zoom) zoom.Click += (_, _) => ToggleZoom();
        if (GetTemplateChild("PART_TitleBar") is FrameworkElement titleBar)
        {
            titleBar.MouseLeftButtonDown += (_, e) =>
            {
                if (e.ClickCount == 2) ToggleZoom();
                else if (e.ButtonState == MouseButtonState.Pressed) DragMove();
            };
        }
    }

    /// <summary>"Zoom" do macOS: preenche a área de trabalho (sem cobrir a barra de tarefas) e volta.</summary>
    void ToggleZoom()
    {
        if (_restoreBounds is Rect r)
        {
            Left = r.Left; Top = r.Top; Width = r.Width; Height = r.Height;
            _restoreBounds = null;
            return;
        }
        _restoreBounds = new Rect(Left, Top, Width, Height);
        var area = SystemParameters.WorkArea;
        const double shadow = 14; // deixa parte da margem de sombra fora da tela
        Left = area.Left - shadow; Top = area.Top - shadow;
        Width = area.Width + 2 * shadow; Height = area.Height + 2 * shadow;
    }

    /// <summary>Traz a janela para frente mesmo quando o app estava em segundo plano.</summary>
    public void ShowAndFocus()
    {
        Show();
        Topmost = true;
        Activate();
        Topmost = false;
        Focus();
    }
}
