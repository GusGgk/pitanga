using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ImageMagick;
using Pitanga.Core;

namespace Pitanga.ImageOps;

/// <param name="Bitmap">Bitmap (possivelmente reduzido) pronto para o WPF, congelado.</param>
/// <param name="Width">Largura ORIGINAL em pixels (já com a orientação EXIF aplicada).</param>
/// <param name="Height">Altura ORIGINAL em pixels.</param>
public sealed record LoadedImage(BitmapSource Bitmap, int Width, int Height);

/// <summary>
/// Ponte ImageMagick ⇄ WPF. Toda leitura passa pelo Magick (suporta HEIC/AVIF/WEBP sem codecs
/// da Store e aplica a orientação EXIF), e a conversão de pixels é uma cópia BGRA direta.
/// </summary>
public static class ImageIO
{
    public static LoadedImage Load(string path, int maxSide = 0)
    {
        using var img = new MagickImage(path);
        img.AutoOrient();
        int w = (int)img.Width, h = (int)img.Height;
        if (maxSide > 0 && Math.Max(w, h) > maxSide)
            img.Resize(new MagickGeometry((uint)maxSide, (uint)maxSide)); // "fit", mantém proporção
        return new LoadedImage(ToBitmapSource(img), w, h);
    }

    public static BitmapSource ToBitmapSource(IMagickImage<byte> img)
    {
        if (img.ColorSpace == ColorSpace.CMYK) img.ColorSpace = ColorSpace.sRGB;
        using var pixels = img.GetPixelsUnsafe();
        var data = pixels.ToByteArray(PixelMapping.BGRA) ?? throw new InvalidOperationException("Could not read image pixels.");
        int w = (int)img.Width, h = (int)img.Height;
        var bmp = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, data, w * 4);
        bmp.Freeze();
        return bmp;
    }

    /// <summary>Miniatura rápida (WIC do Windows; opcionalmente ImageMagick para HEIC/AVIF sem codec).</summary>
    public static BitmapSource? TryLoadThumbnail(string path, int size, bool allowMagickFallback = true)
    {
        try
        {
            var bi = new BitmapImage();
            bi.BeginInit();
            bi.CacheOption = BitmapCacheOption.OnLoad;
            bi.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            bi.DecodePixelWidth = size;
            bi.UriSource = new Uri(path);
            bi.EndInit();
            bi.Freeze();
            return bi;
        }
        catch { /* formato sem codec WIC (ex.: HEIC sem extensão da Store) */ }

        return allowMagickFallback ? MagickThumbnail(path, size) : null;
    }

    // Método separado: o JIT só carrega o ImageMagick se este caminho for de fato usado.
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    static BitmapSource? MagickThumbnail(string path, int size)
    {
        try
        {
            using var img = new MagickImage(path);
            img.AutoOrient();
            img.Thumbnail((uint)size, (uint)size);
            return ToBitmapSource(img);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Salva um bitmap WPF no formato indicado pela extensão. Retorna o caminho final.</summary>
    public static string Save(BitmapSource source, string path, bool stripMetadata = false)
    {
        BitmapSource bgra = source.Format == PixelFormats.Bgra32
            ? source
            : new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0); // também "despremultiplica" Pbgra32
        int w = bgra.PixelWidth, h = bgra.PixelHeight, stride = w * 4;
        var bytes = new byte[stride * h];
        bgra.CopyPixels(bytes, stride, 0);

        using var img = new MagickImage();
        img.ReadPixels(bytes, new PixelReadSettings((uint)w, (uint)h, StorageType.Char, PixelMapping.BGRA));
        if (stripMetadata) img.Strip();
        return WriteWithFallback(img, path);
    }

    /// <summary>
    /// Escreve no formato da extensão. Se o encoder não estiver disponível (ex.: HEIC em algumas builds),
    /// cai para PNG em vez de falhar.
    /// </summary>
    public static string WriteWithFallback(IMagickImage<byte> img, string path)
    {
        var ext = Path.GetExtension(path);
        var format = FormatFor(ext);

        if (!FileKinds.SupportsAlpha(ext) && img.HasAlpha)
        {
            img.BackgroundColor = MagickColors.White;
            img.Alpha(AlphaOption.Remove);
        }
        if (img.Quality == 0 && format is MagickFormat.Jpeg or MagickFormat.WebP or MagickFormat.Heic or MagickFormat.Avif)
            img.Quality = 92;

        try
        {
            img.Write(path, format);
            return path;
        }
        catch (MagickException) when (format != MagickFormat.Png)
        {
            TryDelete(path);
            var png = OutputPaths.Unique(Path.ChangeExtension(path, ".png"));
            img.Write(png, MagickFormat.Png);
            return png;
        }
    }

    public static string CropToFile(string path, Rect crop)
    {
        using var img = new MagickImage(path);
        img.AutoOrient();
        int iw = (int)img.Width, ih = (int)img.Height;
        int x = Math.Clamp((int)Math.Round(crop.X), 0, iw - 1);
        int y = Math.Clamp((int)Math.Round(crop.Y), 0, ih - 1);
        int w = Math.Clamp((int)Math.Round(crop.Width), 1, iw - x);
        int h = Math.Clamp((int)Math.Round(crop.Height), 1, ih - y);

        img.Crop(new MagickGeometry(x, y, (uint)w, (uint)h));
        img.ResetPage();
        return WriteWithFallback(img, OutputPaths.WithSuffix(path, "_Cropped"));
    }

    public static MagickFormat FormatFor(string extension) => extension.ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" or ".jfif" => MagickFormat.Jpeg,
        ".webp" => MagickFormat.WebP,
        ".heic" or ".heif" => MagickFormat.Heic,
        ".avif" => MagickFormat.Avif,
        ".bmp" => MagickFormat.Bmp,
        ".tif" or ".tiff" => MagickFormat.Tiff,
        ".gif" => MagickFormat.Gif,
        ".ico" => MagickFormat.Ico,
        ".pdf" => MagickFormat.Pdf,
        _ => MagickFormat.Png,
    };

    static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }
}
