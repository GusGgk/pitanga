using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;

namespace Pitanga.ImageOps;

/// <param name="Background">null = transparente.</param>
/// <param name="Padding">Margem em pixels da imagem original.</param>
/// <param name="Corners">Raio do arredondamento, em pixels.</param>
/// <param name="Shadow">Raio de desfoque da sombra, em pixels (0 = sem sombra).</param>
/// <param name="Ratio">0 = automático (imagem + padding); senão largura/altura do canvas.</param>
public sealed record BackgroundOptions(Brush? Background, double Padding, double Corners, double Shadow, double Ratio);

/// <summary>
/// Monta a composição "imagem flutuando sobre fundo" como uma árvore WPF.
/// A MESMA árvore é usada na prévia (dentro de um Viewbox) e na exportação
/// (RenderTargetBitmap em resolução total) — o que você vê é exatamente o que é salvo.
/// </summary>
public static class BackgroundComposer
{
    public static Size CanvasSize(int imageWidth, int imageHeight, BackgroundOptions o)
    {
        double w = imageWidth + 2 * o.Padding, h = imageHeight + 2 * o.Padding;
        if (o.Ratio > 0)
        {
            if (w / h < o.Ratio) w = h * o.Ratio;
            else h = w / o.Ratio;
        }
        return new Size(Math.Round(w), Math.Round(h));
    }

    public static FrameworkElement Build(BitmapSource image, int imageWidth, int imageHeight, BackgroundOptions o, bool preview)
    {
        var size = CanvasSize(imageWidth, imageHeight, o);
        var root = new Grid
        {
            Width = size.Width,
            Height = size.Height,
            ClipToBounds = true,
            Background = o.Background ?? (preview ? Checkerboard(Math.Max(size.Width, size.Height) / 48) : Brushes.Transparent),
        };

        var holder = new Grid
        {
            Width = imageWidth,
            Height = imageHeight,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        if (o.Shadow > 0)
        {
            holder.Effect = new DropShadowEffect
            {
                BlurRadius = o.Shadow,
                ShadowDepth = o.Shadow * 0.3,
                Direction = 270,
                Opacity = 0.35,
                Color = Colors.Black,
                RenderingBias = preview ? RenderingBias.Performance : RenderingBias.Quality,
            };
        }

        var img = new Image { Source = image, Stretch = Stretch.Fill, Width = imageWidth, Height = imageHeight };
        RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
        if (o.Corners > 0)
            img.Clip = new RectangleGeometry(new Rect(0, 0, imageWidth, imageHeight), o.Corners, o.Corners);

        holder.Children.Add(img);
        root.Children.Add(holder);
        return root;
    }

    /// <summary>Renderiza em resolução total. Deve rodar na thread de UI (cria elementos WPF).</summary>
    public static BitmapSource Render(BitmapSource fullImage, int imageWidth, int imageHeight, BackgroundOptions o)
    {
        var size = CanvasSize(imageWidth, imageHeight, o);
        var element = Build(fullImage, imageWidth, imageHeight, o, preview: false);
        element.Measure(size);
        element.Arrange(new Rect(size));
        element.UpdateLayout();

        var rtb = new RenderTargetBitmap((int)size.Width, (int)size.Height, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(element);
        rtb.Freeze();
        return rtb;
    }

    public static Brush Checkerboard(double cell)
    {
        cell = Math.Max(4, cell);
        var light = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
        var dark = new SolidColorBrush(Color.FromRgb(0xE3, 0xE3, 0xE3));
        var group = new DrawingGroup();
        group.Children.Add(new GeometryDrawing(light, null, new RectangleGeometry(new Rect(0, 0, 2, 2))));
        group.Children.Add(new GeometryDrawing(dark, null, new RectangleGeometry(new Rect(0, 0, 1, 1))));
        group.Children.Add(new GeometryDrawing(dark, null, new RectangleGeometry(new Rect(1, 1, 1, 1))));
        var brush = new DrawingBrush(group)
        {
            TileMode = TileMode.Tile,
            Viewport = new Rect(0, 0, cell * 2, cell * 2),
            ViewportUnits = BrushMappingMode.Absolute,
        };
        brush.Freeze();
        return brush;
    }
}
