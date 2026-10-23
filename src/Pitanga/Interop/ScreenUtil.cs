using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Pitanga.Interop;

internal static class ScreenUtil
{
    /// <summary>Fator de escala (DPI/96) do monitor que contém o ponto físico.</summary>
    public static double ScaleAt(NativeMethods.POINT pt)
    {
        try
        {
            var monitor = NativeMethods.MonitorFromPoint(pt, NativeMethods.MONITOR_DEFAULTTONEAREST);
            if (NativeMethods.GetDpiForMonitor(monitor, NativeMethods.MDT_EFFECTIVE_DPI, out var dpiX, out _) == 0)
                return dpiX / 96.0;
        }
        catch (DllNotFoundException) { }
        return 1.0;
    }

    // Buffers reaproveitados entre aberturas do menu: abrir o menu não aloca nada novo.
    static WriteableBitmap? _bitmap;
    static byte[]? _pixels, _scratch;

    /// <summary>
    /// Captura a região atrás do menu e já a devolve desfocada (efeito "vidro").
    /// O desfoque é calculado UMA vez aqui, em vez de um BlurEffect recalculado a cada quadro da
    /// animação — o que permite renderizar o menu em software (sem GPU, ~70 MB a menos de RAM).
    /// </summary>
    public static BitmapSource? CaptureBlurred(int x, int y, int width, int height, int blurRadius)
    {
        IntPtr screen = IntPtr.Zero, memory = IntPtr.Zero, dib = IntPtr.Zero, previous = IntPtr.Zero;
        try
        {
            screen = GetDC(IntPtr.Zero);
            memory = CreateCompatibleDC(screen);
            var info = new BITMAPINFOHEADER
            {
                biSize = Marshal.SizeOf<BITMAPINFOHEADER>(),
                biWidth = width,
                biHeight = -height, // top-down
                biPlanes = 1,
                biBitCount = 32,
            };
            dib = CreateDIBSection(screen, ref info, 0, out var bits, IntPtr.Zero, 0);
            if (dib == IntPtr.Zero) return null;
            previous = SelectObject(memory, dib);
            if (!BitBlt(memory, 0, 0, width, height, screen, x, y, SRCCOPY)) return null;

            int stride = width * 4, length = stride * height;
            if (_pixels?.Length != length)
            {
                _pixels = new byte[length];
                _scratch = new byte[length];
            }
            Marshal.Copy(bits, _pixels, 0, length);
            for (int pass = 0; pass < 3; pass++) // 3 caixas ≈ gaussiana
            {
                BoxBlurHorizontal(_pixels, _scratch!, width, height, blurRadius);
                BoxBlurVertical(_scratch!, _pixels, width, height, blurRadius);
            }

            if (_bitmap == null || _bitmap.PixelWidth != width || _bitmap.PixelHeight != height)
                _bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgr32, null);
            _bitmap.WritePixels(new Int32Rect(0, 0, width, height), _pixels, stride, 0);
            return _bitmap;
        }
        catch
        {
            return null; // sem backdrop o menu continua funcional (apenas sem vidro)
        }
        finally
        {
            if (previous != IntPtr.Zero) SelectObject(memory, previous);
            if (dib != IntPtr.Zero) DeleteObject(dib);
            if (memory != IntPtr.Zero) DeleteDC(memory);
            if (screen != IntPtr.Zero) ReleaseDC(IntPtr.Zero, screen);
        }
    }

    static void BoxBlurHorizontal(byte[] src, byte[] dst, int w, int h, int r)
    {
        int div = 2 * r + 1;
        for (int y = 0; y < h; y++)
        {
            int row = y * w * 4;
            for (int c = 0; c < 3; c++)
            {
                int sum = 0;
                for (int i = -r; i <= r; i++) sum += src[row + Math.Clamp(i, 0, w - 1) * 4 + c];
                for (int x = 0; x < w; x++)
                {
                    dst[row + x * 4 + c] = (byte)(sum / div);
                    sum += src[row + Math.Min(x + r + 1, w - 1) * 4 + c] - src[row + Math.Max(x - r, 0) * 4 + c];
                }
            }
        }
    }

    static void BoxBlurVertical(byte[] src, byte[] dst, int w, int h, int r)
    {
        int div = 2 * r + 1, stride = w * 4;
        for (int x = 0; x < w; x++)
        {
            int col = x * 4;
            for (int c = 0; c < 3; c++)
            {
                int sum = 0;
                for (int i = -r; i <= r; i++) sum += src[Math.Clamp(i, 0, h - 1) * stride + col + c];
                for (int y = 0; y < h; y++)
                {
                    dst[y * stride + col + c] = (byte)(sum / div);
                    sum += src[Math.Min(y + r + 1, h - 1) * stride + col + c] - src[Math.Max(y - r, 0) * stride + col + c];
                }
            }
        }
    }

    // ------------------------------------------------------------ GDI

    const int SRCCOPY = 0x00CC0020;

    [StructLayout(LayoutKind.Sequential)]
    struct BITMAPINFOHEADER
    {
        public int biSize;
        public int biWidth;
        public int biHeight;
        public short biPlanes;
        public short biBitCount;
        public int biCompression;
        public int biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public int biClrUsed;
        public int biClrImportant;
    }

    [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr hWnd);
    [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
    [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr hdc);
    [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr hdc);
    [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr hdc, IntPtr h);
    [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr ho);
    [DllImport("gdi32.dll")]
    static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFOHEADER pbmi, uint usage, out IntPtr ppvBits, IntPtr hSection, uint offset);
    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool BitBlt(IntPtr hdc, int x, int y, int cx, int cy, IntPtr hdcSrc, int x1, int y1, int rop);
}
