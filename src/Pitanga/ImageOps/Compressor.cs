using ImageMagick;
using Pitanga.Conversion;
using Pitanga.Core;

namespace Pitanga.ImageOps;

public static class Compressor
{
    const uint LossyQuality = 72;

    public static ConversionResult Compress(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        long before = new FileInfo(path).Length;
        string output;

        using (var img = new MagickImage(path))
        {
            img.AutoOrient();
            img.Strip(); // EXIF/ICC/miniaturas costumam ser 10–30% do arquivo

            switch (ext)
            {
                case ".png":
                    // Quantização para paleta de 256 cores (como TinyPNG): perda visual mínima, ganho grande.
                    img.Quantize(new QuantizeSettings { Colors = 256, DitherMethod = DitherMethod.FloydSteinberg });
                    output = OutputPaths.WithSuffix(path, "_Compressed");
                    img.Write(output, MagickFormat.Png);
                    break;

                case ".bmp" or ".tif" or ".tiff":
                    // Formatos sem compressão eficiente: vira JPG (ou PNG se tiver transparência).
                    output = OutputPaths.WithSuffix(path, "_Compressed", img.HasAlpha ? ".png" : ".jpg");
                    img.Quality = LossyQuality;
                    output = ImageIO.WriteWithFallback(img, output);
                    break;

                default: // jpg, webp, avif, heic, gif
                    img.Quality = Math.Min(img.Quality == 0 ? 100u : img.Quality, LossyQuality);
                    output = ImageIO.WriteWithFallback(img, OutputPaths.WithSuffix(path, "_Compressed"));
                    break;
            }
        }

        bool png = output.EndsWith(".png", StringComparison.OrdinalIgnoreCase);
        if (png) new ImageOptimizer { OptimalCompression = true }.LosslessCompress(output);

        // Gradientes suaves podem crescer com a quantização: tenta só a otimização sem perdas do original.
        if (png && ext == ".png" && new FileInfo(output).Length >= before)
        {
            File.Copy(path, output, overwrite: true);
            new ImageOptimizer { OptimalCompression = true }.LosslessCompress(output);
        }

        long after = new FileInfo(output).Length;
        if (after >= before)
        {
            // Nunca entrega um arquivo "comprimido" maior que o original.
            File.Delete(output);
            return new ConversionResult(path, $"Already optimized ({OutputPaths.HumanSize(before)})");
        }
        return new ConversionResult(output,
            $"−{100 - after * 100 / before}%  ({OutputPaths.HumanSize(before)} → {OutputPaths.HumanSize(after)})");
    }
}
