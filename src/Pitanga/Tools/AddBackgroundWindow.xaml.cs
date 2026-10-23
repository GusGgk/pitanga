using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Pitanga.Core;
using Pitanga.ImageOps;
using Pitanga.UI;

namespace Pitanga.Tools;

public partial class AddBackgroundWindow : MacWindow
{
    const int DefaultSwatch = 6;

    readonly string _path;
    readonly LoadedImage _preview;
    readonly int _maxSide, _minSide;
    readonly List<RadioButton> _swatches = new();
    RadioButton? _customSwatch;
    Brush? _background;
    double _ratio;
    Color _custom = Color.FromRgb(0xFF, 0x8A, 0x5B);
    bool _ready, _syncingColor;

    public AddBackgroundWindow(string path)
    {
        _path = path;
        InitializeComponent();
        Title = $"Add Background — {Path.GetFileName(path)}";

        _preview = ImageIO.Load(path, maxSide: 1600);
        _maxSide = Math.Max(_preview.Width, _preview.Height);
        _minSide = Math.Min(_preview.Width, _preview.Height);

        // Faixas proporcionais ao tamanho da imagem (padrões ≈ 144 / 12 / 54 px para uma foto de 2550 px)
        PaddingSlider.Maximum = Math.Round(_maxSide * 0.3);
        CornersSlider.Maximum = Math.Round(_minSide * 0.25);
        ShadowSlider.Maximum = Math.Max(10, Math.Round(_maxSide * 0.08));

        BuildSwatches();
        ApplyDefaults();
        _ready = true;
        Rebuild();
    }

    // ------------------------------------------------------------ opções

    BackgroundOptions CurrentOptions() => new(
        _background,
        Math.Round(PaddingSlider.Value),
        Math.Round(CornersSlider.Value),
        Math.Round(ShadowSlider.Value),
        _ratio);

    void ApplyDefaults()
    {
        bool wasReady = _ready;
        _ready = false;
        PaddingSlider.Value = Math.Round(_maxSide * 0.056);
        CornersSlider.Value = Math.Max(4, Math.Round(_maxSide * 0.0047));
        ShadowSlider.Value = Math.Round(_maxSide * 0.021);
        AutoRatio.IsChecked = true;
        _ratio = 0;
        _swatches[DefaultSwatch].IsChecked = true;
        _ready = wasReady;
    }

    void Rebuild()
    {
        if (!_ready) return;
        var o = CurrentOptions();
        PreviewBox.Child = BackgroundComposer.Build(_preview.Bitmap, _preview.Width, _preview.Height, o, preview: true);
        PaddingValue.Text = $"{o.Padding:0}px";
        CornersValue.Text = $"{o.Corners:0}px";
        ShadowValue.Text = $"{o.Shadow:0}px";
    }

    void Option_Changed(object sender, RoutedPropertyChangedEventArgs<double> e) => Rebuild();

    void Ratio_Checked(object sender, RoutedEventArgs e)
    {
        _ratio = double.Parse((string)((RadioButton)sender).Tag, CultureInfo.InvariantCulture);
        Rebuild();
    }

    void Reset_Click(object sender, RoutedEventArgs e)
    {
        ApplyDefaults();
        Rebuild();
    }

    async void Save_Click(object sender, RoutedEventArgs e)
    {
        SaveButton.IsEnabled = false;
        try
        {
            var o = CurrentOptions();
            var full = await Task.Run(() => ImageIO.Load(_path));
            var bitmap = BackgroundComposer.Render(full.Bitmap, full.Width, full.Height, o); // thread de UI (WPF)

            // Fundo transparente exige formato com alfa; caso contrário mantém o formato original.
            var ext = Path.GetExtension(_path);
            if (o.Background is null && !FileKinds.SupportsAlpha(ext)) ext = ".png";
            var output = await Task.Run(() => ImageIO.Save(bitmap, OutputPaths.WithSuffix(_path, "_Background", ext)));

            Toast.Show($"Saved {Path.GetFileName(output)}", $"{bitmap.PixelWidth} × {bitmap.PixelHeight}", ToastKind.Success, output);
            Close();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "add background");
            Toast.Error(ex, "Save failed");
            SaveButton.IsEnabled = true;
        }
    }

    // ------------------------------------------------------------ paleta

    void BuildSwatches()
    {
        var palette = new (Brush? Fill, Brush Bubble)[]
        {
            (null, BackgroundComposer.Checkerboard(5)), // transparente
            Solid("#FFFFFF"), Solid("#F2F2F7"), Solid("#1C1C1E"), Solid("#FFD9B5"), Solid("#FFE9A6"),
            Gradient("#FFB36B", "#FF5E3A"), Gradient("#FBD786", "#F7797D"), Gradient("#A1C4FD", "#C2E9FB"),
            Gradient("#D4FC79", "#96E6A1"), Gradient("#C471F5", "#FA71CD"), Gradient("#2C3E50", "#4CA1AF"),
        };

        foreach (var (fill, bubble) in palette)
        {
            var swatch = NewSwatch(bubble);
            swatch.Checked += (_, _) => { _background = fill; Rebuild(); };
            _swatches.Add(swatch);
            Swatches.Children.Add(swatch);
        }

        // Cor customizada: bolha "arco-íris" com "+"
        var rainbow = new LinearGradientBrush(new GradientStopCollection
        {
            new GradientStop(Color.FromRgb(0xFF, 0x5E, 0x5E), 0), new GradientStop(Color.FromRgb(0xFF, 0xD1, 0x5E), 0.33),
            new GradientStop(Color.FromRgb(0x5E, 0xD1, 0xFF), 0.66), new GradientStop(Color.FromRgb(0xB1, 0x5E, 0xFF), 1),
        }, 45);
        rainbow.Freeze();
        _customSwatch = NewSwatch(rainbow);
        _customSwatch.Content = new TextBlock { Text = "+", FontSize = 14, FontWeight = FontWeights.Bold, Foreground = Brushes.White, Margin = new Thickness(0, -2, 0, 0) };
        _customSwatch.Checked += (_, _) => { ApplyCustomColor(); OpenColorPopup(); };
        _customSwatch.PreviewMouseLeftButtonUp += (_, _) => { if (_customSwatch.IsChecked == true) OpenColorPopup(); };
        Swatches.Children.Add(_customSwatch);
    }

    RadioButton NewSwatch(Brush bubble) => new()
    {
        Style = (Style)FindResource("Bubble"),
        GroupName = "bg",
        Tag = bubble,
    };

    static (Brush?, Brush) Solid(string hex)
    {
        var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        b.Freeze();
        return (b, b);
    }

    static (Brush?, Brush) Gradient(string from, string to)
    {
        var b = new LinearGradientBrush((Color)ColorConverter.ConvertFromString(from), (Color)ColorConverter.ConvertFromString(to), 45);
        b.Freeze();
        return (b, b);
    }

    // ------------------------------------------------------------ cor customizada (HSV)

    void OpenColorPopup()
    {
        SyncColorControls();
        ColorPopup.PlacementTarget = _customSwatch;
        ColorPopup.IsOpen = true;
    }

    void ApplyCustomColor()
    {
        var brush = new SolidColorBrush(_custom);
        brush.Freeze();
        _background = brush;
        CustomPreview.Background = brush;
        Rebuild();
    }

    void SyncColorControls()
    {
        _syncingColor = true;
        var (h, s, v) = ToHsv(_custom);
        HueSlider.Value = h;
        SatSlider.Value = s * 100;
        ValSlider.Value = v * 100;
        HexBox.Text = $"#{_custom.R:X2}{_custom.G:X2}{_custom.B:X2}";
        CustomPreview.Background = new SolidColorBrush(_custom);
        _syncingColor = false;
    }

    void Hsv_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_syncingColor || !_ready) return;
        _custom = FromHsv(HueSlider.Value, SatSlider.Value / 100, ValSlider.Value / 100);
        _syncingColor = true;
        HexBox.Text = $"#{_custom.R:X2}{_custom.G:X2}{_custom.B:X2}";
        _syncingColor = false;
        if (_customSwatch?.IsChecked == true) ApplyCustomColor();
    }

    void Hex_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { CommitHex(); e.Handled = true; }
    }

    void Hex_LostFocus(object sender, KeyboardFocusChangedEventArgs e) => CommitHex();

    void CommitHex()
    {
        if (_syncingColor) return;
        try
        {
            var text = HexBox.Text.Trim();
            if (!text.StartsWith('#')) text = "#" + text;
            _custom = (Color)ColorConverter.ConvertFromString(text);
            _custom.A = 255;
            if (_customSwatch?.IsChecked == true) ApplyCustomColor();
        }
        catch (FormatException) { }
        SyncColorControls();
    }

    static (double H, double S, double V) ToHsv(Color c)
    {
        double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
        double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b)), d = max - min;
        double h = d == 0 ? 0 : max == r ? 60 * ((g - b) / d % 6) : max == g ? 60 * ((b - r) / d + 2) : 60 * ((r - g) / d + 4);
        if (h < 0) h += 360;
        return (h, max == 0 ? 0 : d / max, max);
    }

    static Color FromHsv(double h, double s, double v)
    {
        double c = v * s, x = c * (1 - Math.Abs(h / 60 % 2 - 1)), m = v - c;
        var (r, g, b) = (h % 360) switch
        {
            < 60 => (c, x, 0d),
            < 120 => (x, c, 0d),
            < 180 => (0d, c, x),
            < 240 => (0d, x, c),
            < 300 => (x, 0d, c),
            _ => (c, 0d, x),
        };
        return Color.FromRgb((byte)Math.Round((r + m) * 255), (byte)Math.Round((g + m) * 255), (byte)Math.Round((b + m) * 255));
    }
}
