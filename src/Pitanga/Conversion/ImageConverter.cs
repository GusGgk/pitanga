using ImageMagick;
using Pitanga.Core;
using Pitanga.ImageOps;

namespace Pitanga.Conversion;

public static class ImageConverter
{
    static readonly Dictionary<string, string> Extensions = new()
    {
        ["JPG"] = ".jpg", ["PNG"] = ".png", ["WEBP"] = ".webp", ["HEIC"] = ".heic", ["AVIF"] = ".avif",
        ["BMP"] = ".bmp", ["TIFF"] = ".tiff", ["GIF"] = ".gif", ["PDF"] = ".pdf",
    };

    public static string Convert(string source, string target)
    {
        if (target == "DOCX")
            return DocxWriter.FromImages(new[] { source }, OutputPaths.ChangeExtension(source, ".docx"));

        if (!Extensions.TryGetValue(target, out var ext))
            throw new NotSupportedException($"Image → {target} is not supported.");

        using var img = new MagickImage(source); // primeiro frame em GIFs animados
        img.AutoOrient();
        if (target == "TIFF") img.Settings.Compression = CompressionMethod.LZW;
        if (target == "PDF") PrepareForPdf(img);

        return ImageIO.WriteWithFallback(img, OutputPaths.ChangeExtension(source, ext));
    }

    /// <summary>Várias imagens + "PDF" = um único PDF com uma página por imagem.</summary>
    public static string ToCombinedPdf(IReadOnlyList<string> sources)
    {
        var output = OutputPaths.WithSuffix(sources[0], "_Combined", ".pdf");
        using var collection = new MagickImageCollection();
        foreach (var source in sources.OrderBy(Path.GetFileName, StringComparer.CurrentCultureIgnoreCase))
        {
            var img = new MagickImage(source);
            img.AutoOrient();
            PrepareForPdf(img);
            collection.Add(img); // a coleção passa a ser dona da imagem
        }
        collection.Write(output, MagickFormat.Pdf);
        return output;
    }

    static void PrepareForPdf(IMagickImage<byte> img)
    {
        if (img.HasAlpha)
        {
            img.BackgroundColor = MagickColors.White;
            img.Alpha(AlphaOption.Remove);
        }
        img.Quality = 90;
        // 150 dpi: fotos grandes viram páginas de tamanho razoável em vez de "pôsteres" de 72 dpi.
        img.Density = new Density(150, 150);
    }
}
