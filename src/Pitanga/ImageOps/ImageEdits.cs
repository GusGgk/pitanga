using ImageMagick;

namespace Pitanga.ImageOps;

/// <param name="Rotation">0, 90, 180 ou 270 (horário).</param>
/// <param name="Brightness">-100..100</param>
/// <param name="Contrast">-100..100</param>
/// <param name="Saturation">-100..100 (0 = original)</param>
public sealed record EditState(int Rotation, bool FlipH, bool FlipV, int Brightness, int Contrast, int Saturation)
{
    public static EditState Identity { get; } = new(0, false, false, 0, 0, 0);
}

public static class ImageEdits
{
    /// <summary>Mesma função para a prévia (imagem reduzida) e para o arquivo final (resolução total).</summary>
    public static void Apply(IMagickImage<byte> img, EditState s)
    {
        if (s.Rotation % 360 != 0) img.Rotate(s.Rotation);
        if (s.FlipH) img.Flop();
        if (s.FlipV) img.Flip();
        if (s.Brightness != 0 || s.Contrast != 0)
            img.BrightnessContrast(new Percentage(s.Brightness), new Percentage(s.Contrast));
        if (s.Saturation != 0)
            img.Modulate(new Percentage(100), new Percentage(100 + s.Saturation), new Percentage(100));
    }
}
