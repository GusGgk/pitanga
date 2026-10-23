using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Pitanga.Tools;

/// <summary>
/// Imagem + overlay de recorte num único elemento desenhado à mão.
/// O retângulo de recorte é guardado em PIXELS DA IMAGEM ORIGINAL; a prévia pode ser reduzida.
/// </summary>
public sealed class CropCanvas : FrameworkElement
{
    [Flags]
    enum Edge { None = 0, Left = 1, Top = 2, Right = 4, Bottom = 8, Move = 16 }

    const double Pad = 28, HandleHit = 14, MinSize = 16;

    static readonly Brush Dim = Frozen(new SolidColorBrush(Color.FromArgb(0x8C, 0x10, 0x10, 0x10)));
    static readonly Pen Border = Frozen(new Pen(Brushes.White, 1.5));
    static readonly Pen Thirds = Frozen(new Pen(new SolidColorBrush(Color.FromArgb(0x66, 0xFF, 0xFF, 0xFF)), 1));
    static readonly Pen Handle = Frozen(new Pen(Brushes.White, 4) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round });

    BitmapSource? _image;
    double _iw, _ih, _ratio;
    Rect _crop;
    Edge _drag;
    Point _dragStart;
    Rect _startRect;

    public event EventHandler? CropChanged;

    public bool HasImage => _image != null;
    public double Ratio => _ratio;

    public Rect Crop
    {
        get => _crop;
        private set
        {
            _crop = value;
            InvalidateVisual();
            CropChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public CropCanvas()
    {
        Focusable = true;
        ClipToBounds = true;
    }

    public void SetImage(BitmapSource preview, int width, int height)
    {
        _image = preview;
        _iw = width;
        _ih = height;
        Crop = new Rect(0, 0, width, height);
    }

    public void Reset()
    {
        _ratio = 0;
        Crop = new Rect(0, 0, _iw, _ih);
    }

    /// <summary>0 = livre. Senão, o maior retângulo com essa proporção, centrado.</summary>
    public void SetRatio(double ratio)
    {
        _ratio = ratio;
        if (ratio <= 0 || _iw == 0) return;
        double w = _iw, h = w / ratio;
        if (h > _ih) { h = _ih; w = h * ratio; }
        Crop = new Rect((_iw - w) / 2, (_ih - h) / 2, w, h);
    }

    /// <summary>Tamanho exato digitado (mantém o centro; com proporção fixa, o outro lado acompanha).</summary>
    public void SetSize(double width, double height, bool widthDriven)
    {
        if (_ratio > 0)
        {
            if (widthDriven) height = width / _ratio;
            else width = height * _ratio;
        }
        width = Math.Clamp(width, MinSize, _iw);
        height = Math.Clamp(height, MinSize, _ih);
        if (_ratio > 0)
        {
            // re-encaixa se o clamp quebrou a proporção
            if (width / height > _ratio) width = height * _ratio;
            else height = width / _ratio;
        }
        var c = new Point(_crop.X + _crop.Width / 2, _crop.Y + _crop.Height / 2);
        double x = Math.Clamp(c.X - width / 2, 0, _iw - width);
        double y = Math.Clamp(c.Y - height / 2, 0, _ih - height);
        Crop = new Rect(x, y, width, height);
    }

    // ------------------------------------------------------------ geometria

    Rect ImageRect
    {
        get
        {
            if (_iw == 0) return Rect.Empty;
            double aw = Math.Max(1, ActualWidth - 2 * Pad), ah = Math.Max(1, ActualHeight - 2 * Pad);
            double s = Math.Min(aw / _iw, ah / _ih);
            double w = _iw * s, h = _ih * s;
            return new Rect((ActualWidth - w) / 2, (ActualHeight - h) / 2, w, h);
        }
    }

    double Scale => ImageRect.Width / _iw;

    Point ToImage(Point p)
    {
        var r = ImageRect;
        return new Point((p.X - r.X) / Scale, (p.Y - r.Y) / Scale);
    }

    Rect ToView(Rect c)
    {
        var r = ImageRect;
        double s = Scale;
        return new Rect(r.X + c.X * s, r.Y + c.Y * s, c.Width * s, c.Height * s);
    }

    // ------------------------------------------------------------ desenho

    protected override void OnRender(DrawingContext dc)
    {
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(RenderSize)); // área inteira recebe mouse
        if (_image == null) return;

        var ir = ImageRect;
        dc.DrawImage(_image, ir);

        var cr = ToView(_crop);
        dc.DrawGeometry(Dim, null, new CombinedGeometry(GeometryCombineMode.Exclude, new RectangleGeometry(ir), new RectangleGeometry(cr)));

        for (int i = 1; i <= 2; i++)
        {
            double x = cr.X + cr.Width * i / 3, y = cr.Y + cr.Height * i / 3;
            dc.DrawLine(Thirds, new Point(x, cr.Top), new Point(x, cr.Bottom));
            dc.DrawLine(Thirds, new Point(cr.Left, y), new Point(cr.Right, y));
        }
        dc.DrawRectangle(null, Border, cr);

        // Alças em "L" nos cantos e barras no meio das bordas
        double len = Math.Min(20, Math.Min(cr.Width, cr.Height) / 3);
        var g = new StreamGeometry();
        using (var ctx = g.Open())
        {
            void Poly(params Point[] pts)
            {
                ctx.BeginFigure(pts[0], false, false);
                for (int i = 1; i < pts.Length; i++) ctx.LineTo(pts[i], true, true);
            }
            Poly(new(cr.Left, cr.Top + len), cr.TopLeft, new(cr.Left + len, cr.Top));
            Poly(new(cr.Right - len, cr.Top), cr.TopRight, new(cr.Right, cr.Top + len));
            Poly(new(cr.Right, cr.Bottom - len), cr.BottomRight, new(cr.Right - len, cr.Bottom));
            Poly(new(cr.Left + len, cr.Bottom), cr.BottomLeft, new(cr.Left, cr.Bottom - len));
            double mx = cr.X + cr.Width / 2, my = cr.Y + cr.Height / 2, half = len / 2;
            Poly(new(mx - half, cr.Top), new(mx + half, cr.Top));
            Poly(new(mx - half, cr.Bottom), new(mx + half, cr.Bottom));
            Poly(new(cr.Left, my - half), new(cr.Left, my + half));
            Poly(new(cr.Right, my - half), new(cr.Right, my + half));
        }
        g.Freeze();
        dc.DrawGeometry(null, Handle, g);
    }

    // ------------------------------------------------------------ interação

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        if (_image == null) return;
        var p = e.GetPosition(this);
        _drag = HitEdge(p);
        if (_drag == Edge.None) return;
        _dragStart = ToImage(p);
        _startRect = _crop;
        CaptureMouse();
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (_image == null) return;
        var p = e.GetPosition(this);
        if (_drag == Edge.None)
        {
            Cursor = CursorFor(HitEdge(p));
            return;
        }

        var ip = ToImage(p);
        if (_drag == Edge.Move)
        {
            var r = _startRect;
            r.X = Math.Clamp(r.X + ip.X - _dragStart.X, 0, _iw - r.Width);
            r.Y = Math.Clamp(r.Y + ip.Y - _dragStart.Y, 0, _ih - r.Height);
            Crop = r;
        }
        else
        {
            Crop = Resize(new Point(Math.Clamp(ip.X, 0, _iw), Math.Clamp(ip.Y, 0, _ih)));
        }
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        if (_drag == Edge.None) return;
        _drag = Edge.None;
        ReleaseMouseCapture();
    }

    Rect Resize(Point p)
    {
        var r = _startRect;
        bool left = _drag.HasFlag(Edge.Left), right = _drag.HasFlag(Edge.Right);
        bool top = _drag.HasFlag(Edge.Top), bottom = _drag.HasFlag(Edge.Bottom);
        double x1 = r.Left, x2 = r.Right, y1 = r.Top, y2 = r.Bottom;

        if (left) x1 = Math.Min(p.X, x2 - MinSize);
        if (right) x2 = Math.Max(p.X, x1 + MinSize);
        if (top) y1 = Math.Min(p.Y, y2 - MinSize);
        if (bottom) y2 = Math.Max(p.Y, y1 + MinSize);

        if (_ratio > 0)
        {
            double w = x2 - x1, h = y2 - y1;
            bool horizontal = left || right, vertical = top || bottom;

            if (horizontal && vertical)
            {
                // Canto: encolhe o lado "sobrando" — o canto oposto fica ancorado e nunca sai da imagem.
                if (w / h > _ratio) w = h * _ratio; else h = w / _ratio;
                if (left) x1 = x2 - w; else x2 = x1 + w;
                if (top) y1 = y2 - h; else y2 = y1 + h;
            }
            else if (horizontal)
            {
                double cy = (r.Top + r.Bottom) / 2;
                double maxH = 2 * Math.Min(cy, _ih - cy);
                h = w / _ratio;
                if (h > maxH) { h = maxH; w = h * _ratio; if (left) x1 = x2 - w; else x2 = x1 + w; }
                y1 = cy - h / 2; y2 = cy + h / 2;
            }
            else
            {
                double cx = (r.Left + r.Right) / 2;
                double maxW = 2 * Math.Min(cx, _iw - cx);
                w = h * _ratio;
                if (w > maxW) { w = maxW; h = w / _ratio; if (top) y1 = y2 - h; else y2 = y1 + h; }
                x1 = cx - w / 2; x2 = cx + w / 2;
            }
        }
        return new Rect(new Point(x1, y1), new Point(x2, y2));
    }

    Edge HitEdge(Point p)
    {
        var r = ToView(_crop);
        double t = HandleHit;
        bool inX = p.X >= r.Left - t && p.X <= r.Right + t;
        bool inY = p.Y >= r.Top - t && p.Y <= r.Bottom + t;
        var e = Edge.None;
        if (inY && Math.Abs(p.X - r.Left) <= t) e |= Edge.Left;
        else if (inY && Math.Abs(p.X - r.Right) <= t) e |= Edge.Right;
        if (inX && Math.Abs(p.Y - r.Top) <= t) e |= Edge.Top;
        else if (inX && Math.Abs(p.Y - r.Bottom) <= t) e |= Edge.Bottom;
        if (e == Edge.None && r.Contains(p)) e = Edge.Move;
        return e;
    }

    static Cursor CursorFor(Edge e) => e switch
    {
        (Edge.Left | Edge.Top) or (Edge.Right | Edge.Bottom) => Cursors.SizeNWSE,
        (Edge.Right | Edge.Top) or (Edge.Left | Edge.Bottom) => Cursors.SizeNESW,
        Edge.Left or Edge.Right => Cursors.SizeWE,
        Edge.Top or Edge.Bottom => Cursors.SizeNS,
        Edge.Move => Cursors.SizeAll,
        _ => Cursors.Arrow,
    };

    static T Frozen<T>(T f) where T : Freezable
    {
        f.Freeze();
        return f;
    }
}
