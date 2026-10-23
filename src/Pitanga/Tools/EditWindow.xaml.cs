using System.Windows;
using System.Windows.Threading;
using ImageMagick;
using Pitanga.Core;
using Pitanga.ImageOps;
using Pitanga.UI;

namespace Pitanga.Tools;

public partial class EditWindow : MacWindow
{
    readonly string _path;
    readonly DispatcherTimer _debounce = new() { Interval = TimeSpan.FromMilliseconds(60) };
    MagickImage? _preview; // cópia reduzida: os ajustes são reaplicados nela a cada mudança
    int _rotation;
    bool _flipH, _flipV;

    public EditWindow(string path)
    {
        _path = path;
        InitializeComponent();
        Title = $"Edit — {Path.GetFileName(path)}";

        var img = new MagickImage(path);
        img.AutoOrient();
        if (Math.Max(img.Width, img.Height) > 1400) img.Resize(new MagickGeometry(1400, 1400));
        _preview = img;

        _debounce.Tick += (_, _) =>
        {
            _debounce.Stop();
            RenderPreview();
        };
        Closed += (_, _) => _preview?.Dispose();
        RenderPreview();
    }

    EditState State => new(
        _rotation, _flipH, _flipV,
        (int)Math.Round(BrightnessSlider.Value),
        (int)Math.Round(ContrastSlider.Value),
        (int)Math.Round(SaturationSlider.Value));

    void RenderPreview()
    {
        if (_preview is null) return;
        using var copy = new MagickImage(_preview);
        ImageEdits.Apply(copy, State);
        PreviewImage.Source = ImageIO.ToBitmapSource(copy);
    }

    void Slider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_preview is null) return; // disparado durante InitializeComponent
        BrightnessValue.Text = Signed(BrightnessSlider.Value);
        ContrastValue.Text = Signed(ContrastSlider.Value);
        SaturationValue.Text = Signed(SaturationSlider.Value);
        _debounce.Stop();
        _debounce.Start();
    }

    static string Signed(double v) => Math.Round(v) switch { > 0 and var p => $"+{p:0}", var n => $"{n:0}" };

    void RotateLeft_Click(object sender, RoutedEventArgs e) { _rotation = (_rotation + 270) % 360; RenderPreview(); }
    void RotateRight_Click(object sender, RoutedEventArgs e) { _rotation = (_rotation + 90) % 360; RenderPreview(); }
    void FlipH_Click(object sender, RoutedEventArgs e) { _flipH = !_flipH; RenderPreview(); }
    void FlipV_Click(object sender, RoutedEventArgs e) { _flipV = !_flipV; RenderPreview(); }

    void Reset_Click(object sender, RoutedEventArgs e)
    {
        _rotation = 0;
        _flipH = _flipV = false;
        BrightnessSlider.Value = ContrastSlider.Value = SaturationSlider.Value = 0;
        RenderPreview();
    }

    async void Apply_Click(object sender, RoutedEventArgs e)
    {
        ApplyButton.IsEnabled = false;
        var state = State;
        try
        {
            var output = await Task.Run(() =>
            {
                using var img = new MagickImage(_path);
                img.AutoOrient();
                ImageEdits.Apply(img, state);
                return ImageIO.WriteWithFallback(img, OutputPaths.WithSuffix(_path, "_Edited"));
            });
            Toast.Show($"Saved {Path.GetFileName(output)}", "", ToastKind.Success, output);
            Close();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "edit");
            Toast.Error(ex, "Edit failed");
            ApplyButton.IsEnabled = true;
        }
    }
}
