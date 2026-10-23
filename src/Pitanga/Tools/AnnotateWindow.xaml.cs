using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ImageMagick;
using Pitanga.Core;
using Pitanga.ImageOps;
using Pitanga.UI;
using Rectangle = System.Windows.Shapes.Rectangle;

namespace Pitanga.Tools;

public enum AnnotateMode { Annotate, Redact }

/// <summary>ANNOTATE (caneta / marca-texto) e REDACT (tarjas pretas ou pixelização) na mesma superfície.</summary>
public partial class AnnotateWindow : MacWindow
{
    static readonly Color[] Palette =
    {
        Color.FromRgb(0xFF, 0x3B, 0x30), Color.FromRgb(0xFF, 0x95, 0x00), Color.FromRgb(0xFF, 0xCC, 0x00),
        Color.FromRgb(0x34, 0xC7, 0x59), Color.FromRgb(0x00, 0x7A, 0xFF), Color.FromRgb(0x1C, 0x1C, 0x1E), Colors.White,
    };

    readonly string _path;
    readonly AnnotateMode _mode;
    readonly LoadedImage _preview;
    readonly Stack<Rectangle> _redactions = new();
    BitmapSource? _pixelated;
    Color _color = Palette[0];
    double _sizeFactor = 0.008;
    bool _highlighter, _pixelate, _ready;
    Rectangle? _drawing;
    Point _start;

    public AnnotateWindow(string path, AnnotateMode mode)
    {
        _path = path;
        _mode = mode;
        InitializeComponent();
        Title = $"{(mode == AnnotateMode.Annotate ? "Annotate" : "Redact")} — {Path.GetFileName(path)}";

        _preview = ImageIO.Load(path, maxSide: 2400);
        Surface.Width = _preview.Width;
        Surface.Height = _preview.Height;
        SurfaceImage.Source = _preview.Bitmap;

        if (mode == AnnotateMode.Annotate)
        {
            RedactLayer.IsHitTestVisible = false;
            BuildColorSwatches();
            ApplyPen();
        }
        else
        {
            AnnotateBar.Visibility = Visibility.Collapsed;
            RedactBar.Visibility = Visibility.Visible;
            Ink.EditingMode = InkCanvasEditingMode.None;
            Ink.IsHitTestVisible = false;
            RedactLayer.Cursor = Cursors.Cross;
        }
        _ready = true;
    }

    // ------------------------------------------------------------ anotação

    void BuildColorSwatches()
    {
        foreach (var color in Palette)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            var swatch = new RadioButton
            {
                Style = (Style)FindResource("Bubble"),
                GroupName = "inkcolor",
                Tag = brush,
                Margin = new Thickness(0, 0, 4, 0),
                IsChecked = color == _color,
            };
            swatch.Checked += (_, _) => { _color = color; ApplyPen(); };
            ColorSwatches.Children.Add(swatch);
        }
    }

    void ApplyPen()
    {
        double size = Math.Max(2, Math.Max(_preview.Width, _preview.Height) * _sizeFactor);
        if (_highlighter) size *= 3;
        Ink.DefaultDrawingAttributes = new DrawingAttributes
        {
            Color = _color,
            Width = size,
            Height = size,
            FitToCurve = true,
            IsHighlighter = _highlighter,
            StylusTip = _highlighter ? StylusTip.Rectangle : StylusTip.Ellipse,
        };
    }

    void Tool_Checked(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        var tool = (string)((RadioButton)sender).Tag;
        Ink.EditingMode = tool == "eraser" ? InkCanvasEditingMode.EraseByStroke : InkCanvasEditingMode.Ink;
        _highlighter = tool == "highlighter";
        ApplyPen();
    }

    void Size_Checked(object sender, RoutedEventArgs e)
    {
        _sizeFactor = double.Parse((string)((RadioButton)sender).Tag, CultureInfo.InvariantCulture);
        if (_ready) ApplyPen();
    }

    // ------------------------------------------------------------ redação

    void RedactStyle_Checked(object sender, RoutedEventArgs e)
    {
        _pixelate = (string)((RadioButton)sender).Tag == "pixelate";
        if (_pixelate && _pixelated is null && _ready) _pixelated = BuildPixelated();
    }

    BitmapSource BuildPixelated()
    {
        using var img = new MagickImage(_path);
        img.AutoOrient();
        img.Resize(new MagickGeometry((uint)_preview.Bitmap.PixelWidth, (uint)_preview.Bitmap.PixelHeight) { IgnoreAspectRatio = true });
        int w = (int)img.Width, h = (int)img.Height;
        int block = Math.Max(6, Math.Max(w, h) / 60);
        img.Sample(new MagickGeometry((uint)Math.Max(1, w / block), (uint)Math.Max(1, h / block)) { IgnoreAspectRatio = true });
        img.Sample(new MagickGeometry((uint)w, (uint)h) { IgnoreAspectRatio = true }); // "nearest neighbour" → blocos
        return ImageIO.ToBitmapSource(img);
    }

    Brush RedactFill(Rect r)
    {
        if (!_pixelate || _pixelated is null) return Brushes.Black;
        double s = _pixelated.PixelWidth / (double)_preview.Width; // superfície (px originais) → bitmap pixelado
        return new ImageBrush(_pixelated)
        {
            ViewboxUnits = BrushMappingMode.Absolute,
            Viewbox = new Rect(r.X * s, r.Y * s, Math.Max(1, r.Width * s), Math.Max(1, r.Height * s)),
            Stretch = Stretch.Fill,
        };
    }

    void Redact_MouseDown(object sender, MouseButtonEventArgs e)
    {
        _start = e.GetPosition(RedactLayer);
        _drawing = new Rectangle { Fill = Brushes.Black };
        Canvas.SetLeft(_drawing, _start.X);
        Canvas.SetTop(_drawing, _start.Y);
        RedactLayer.Children.Add(_drawing);
        RedactLayer.CaptureMouse();
    }

    void Redact_MouseMove(object sender, MouseEventArgs e)
    {
        if (_drawing is null) return;
        var p = e.GetPosition(RedactLayer);
        p = new Point(Math.Clamp(p.X, 0, _preview.Width), Math.Clamp(p.Y, 0, _preview.Height));
        var r = new Rect(_start, p);
        Canvas.SetLeft(_drawing, r.X);
        Canvas.SetTop(_drawing, r.Y);
        _drawing.Width = r.Width;
        _drawing.Height = r.Height;
        _drawing.Fill = RedactFill(r);
    }

    void Redact_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_drawing is null) return;
        RedactLayer.ReleaseMouseCapture();
        if (_drawing.Width < 4 || _drawing.Height < 4) RedactLayer.Children.Remove(_drawing);
        else _redactions.Push(_drawing);
        _drawing = null;
    }

    // ------------------------------------------------------------ ações

    void Undo_Click(object sender, RoutedEventArgs e)
    {
        if (_mode == AnnotateMode.Annotate)
        {
            if (Ink.Strokes.Count > 0) Ink.Strokes.RemoveAt(Ink.Strokes.Count - 1);
        }
        else if (_redactions.Count > 0)
        {
            RedactLayer.Children.Remove(_redactions.Pop());
        }
    }

    void Clear_Click(object sender, RoutedEventArgs e)
    {
        Ink.Strokes.Clear();
        RedactLayer.Children.Clear();
        _redactions.Clear();
    }

    async void Save_Click(object sender, RoutedEventArgs e)
    {
        SaveButton.IsEnabled = false;
        try
        {
            // Troca a prévia pela imagem em resolução total só durante a captura.
            var full = await Task.Run(() => ImageIO.Load(_path));
            SurfaceImage.Source = full.Bitmap;
            Surface.UpdateLayout();
            var snapshot = Snapshot(Surface, full.Width, full.Height);
            SurfaceImage.Source = _preview.Bitmap;

            bool redact = _mode == AnnotateMode.Redact;
            var target = OutputPaths.WithSuffix(_path, redact ? "_Redacted" : "_Annotated");
            var output = await Task.Run(() => ImageIO.Save(snapshot, target, stripMetadata: redact));

            Toast.Show($"Saved {Path.GetFileName(output)}", redact ? "Metadata removed" : "", ToastKind.Success, output);
            Close();
        }
        catch (Exception ex)
        {
            SurfaceImage.Source = _preview.Bitmap;
            Log.Error(ex, "annotate");
            Toast.Error(ex, "Save failed");
            SaveButton.IsEnabled = true;
        }
    }

    /// <summary>Renderiza o elemento em pixels exatos, ignorando o Viewbox que o envolve.</summary>
    static BitmapSource Snapshot(FrameworkElement element, int width, int height)
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            var brush = new VisualBrush(element)
            {
                Stretch = Stretch.Fill,
                ViewboxUnits = BrushMappingMode.Absolute,
                Viewbox = new Rect(0, 0, width, height),
            };
            dc.DrawRectangle(brush, null, new Rect(0, 0, width, height));
        }
        var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(visual);
        rtb.Freeze();
        return rtb;
    }
}
