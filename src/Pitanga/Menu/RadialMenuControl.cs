using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Pitanga.Menu;

/// <summary>
/// Desenha o anel de fatias diretamente no DrawingContext (sem um elemento WPF por fatia):
/// é a forma mais barata de redesenhar a 60 fps enquanto o cursor percorre o menu.
/// A fatia 0 fica centrada no topo; as demais seguem em sentido horário.
/// </summary>
public sealed class RadialMenuControl : FrameworkElement
{
    IReadOnlyList<RadialItem> _items = Array.Empty<RadialItem>();
    int _hot = -1, _previousHot = -1;

    public double OuterRadius { get; set; } = 150;
    public double InnerRadius { get; set; } = 56;

    static readonly Brush SliceA = Frozen(new SolidColorBrush(Color.FromArgb(0xD0, 0xFF, 0xD9, 0xAE)));  // pêssego
    static readonly Brush SliceB = Frozen(new SolidColorBrush(Color.FromArgb(0xD0, 0xFF, 0xE9, 0xB0)));  // amarelo manteiga
    static readonly Brush SliceHot = Frozen(new LinearGradientBrush(Color.FromRgb(0xFF, 0xB0, 0x5E), Color.FromRgb(0xFF, 0x7A, 0x45), 90));
    static readonly Pen Separator = Frozen(new Pen(new SolidColorBrush(Color.FromArgb(0xD9, 0xFF, 0xFF, 0xFF)), 1.5));
    static readonly Color Ink = Color.FromRgb(0x5B, 0x34, 0x16);
    static readonly Color HotInk = Colors.White;
    static readonly Typeface LabelFace = new(new FontFamily("Segoe UI Variable Display, Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
    static readonly Typeface FormatFace = new(new FontFamily("Segoe UI Variable Display, Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);

    public static readonly DependencyProperty HotProgressProperty = DependencyProperty.Register(
        nameof(HotProgress), typeof(double), typeof(RadialMenuControl),
        new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>0→1 durante a transição de destaque entre fatias.</summary>
    public double HotProgress
    {
        get => (double)GetValue(HotProgressProperty);
        set => SetValue(HotProgressProperty, value);
    }

    public IReadOnlyList<RadialItem> Items
    {
        get => _items;
        set
        {
            _items = value;
            _hot = _previousHot = -1;
            InvalidateVisual();
        }
    }

    public int HotIndex
    {
        get => _hot;
        set
        {
            if (_hot == value) return;
            _previousHot = _hot;
            _hot = value;
            BeginAnimation(HotProgressProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(140))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            });
        }
    }

    Point Center => new(ActualWidth / 2, ActualHeight / 2);

    /// <summary>Índice da fatia sob o ponto, ou -1 no miolo (zona de cancelamento).
    /// Fora do raio externo continua valendo a direção — seleção estilo "pie menu".</summary>
    public int HitTestIndex(Point p)
    {
        int n = _items.Count;
        if (n == 0) return -1;
        var c = Center;
        double dx = p.X - c.X, dy = p.Y - c.Y;
        if (dx * dx + dy * dy < InnerRadius * InnerRadius) return -1;

        double fromTop = (Math.Atan2(dy, dx) * 180 / Math.PI + 90 + 360) % 360; // 0 = topo, horário
        double sweep = 360.0 / n;
        return (int)((fromTop + sweep / 2) % 360 / sweep) % n;
    }

    protected override void OnRender(DrawingContext dc)
    {
        int n = _items.Count;
        if (n == 0) return;

        var c = Center;
        double sweep = 360.0 / n;
        double pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;

        for (int i = 0; i < n; i++)
        {
            double t = i == _hot ? HotProgress : i == _previousHot ? 1 - HotProgress : 0;
            double mid = -90 + i * sweep;
            double grow = 6 * t;

            var slice = n == 1
                ? Ring(c, InnerRadius, OuterRadius + grow)
                : Sector(c, InnerRadius, OuterRadius + grow, mid - sweep / 2, mid + sweep / 2);

            dc.DrawGeometry(i % 2 == 0 ? SliceA : SliceB, Separator, slice);
            if (t > 0)
            {
                dc.PushOpacity(t);
                dc.DrawGeometry(SliceHot, null, slice);
                dc.Pop();
            }

            var labelAt = Polar(c, (InnerRadius + OuterRadius) / 2 + grow / 2, n == 1 ? -90 : mid);
            DrawLabel(dc, _items[i], labelAt, Lerp(Ink, HotInk, t), n, pixelsPerDip);
        }
    }

    static void DrawLabel(DrawingContext dc, RadialItem item, Point at, Color color, int count, double pixelsPerDip)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();

        if (item.Icon != null && Icons.TryGet(item.Icon, out var icon))
        {
            const double iconSize = 22, gap = 3;
            var text = Text(item.Label, 10, LabelFace, brush, pixelsPerDip);
            double top = at.Y - (iconSize + gap + text.Height) / 2;

            var pen = new Pen(brush, 1.9) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
            pen.Freeze();
            dc.PushTransform(new TranslateTransform(at.X - iconSize / 2, top));
            dc.PushTransform(new ScaleTransform(iconSize / 24, iconSize / 24));
            dc.DrawGeometry(null, pen, icon);
            dc.Pop();
            dc.Pop();
            dc.DrawText(text, new Point(at.X - text.Width / 2, top + iconSize + gap));
        }
        else
        {
            var text = Text(item.Label, count > 6 ? 13 : 15, FormatFace, brush, pixelsPerDip);
            dc.DrawText(text, new Point(at.X - text.Width / 2, at.Y - text.Height / 2));
        }
    }

    static FormattedText Text(string s, double size, Typeface face, Brush brush, double pixelsPerDip) =>
        new(s, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, face, size, brush, pixelsPerDip);

    static Point Polar(Point c, double r, double degrees)
    {
        double a = degrees * Math.PI / 180;
        return new Point(c.X + r * Math.Cos(a), c.Y + r * Math.Sin(a));
    }

    static Geometry Sector(Point c, double r1, double r2, double a0, double a1)
    {
        bool large = a1 - a0 > 180;
        var figure = new PathFigure { StartPoint = Polar(c, r2, a0), IsClosed = true };
        figure.Segments.Add(new ArcSegment(Polar(c, r2, a1), new Size(r2, r2), 0, large, SweepDirection.Clockwise, true));
        figure.Segments.Add(new LineSegment(Polar(c, r1, a1), true));
        figure.Segments.Add(new ArcSegment(Polar(c, r1, a0), new Size(r1, r1), 0, large, SweepDirection.Counterclockwise, true));
        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        geometry.Freeze();
        return geometry;
    }

    static Geometry Ring(Point c, double r1, double r2)
    {
        var g = new CombinedGeometry(GeometryCombineMode.Exclude, new EllipseGeometry(c, r2, r2), new EllipseGeometry(c, r1, r1));
        g.Freeze();
        return g;
    }

    static Color Lerp(Color a, Color b, double t) => Color.FromArgb(
        (byte)(a.A + (b.A - a.A) * t), (byte)(a.R + (b.R - a.R) * t),
        (byte)(a.G + (b.G - a.G) * t), (byte)(a.B + (b.B - a.B) * t));

    static T Frozen<T>(T freezable) where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }
}
