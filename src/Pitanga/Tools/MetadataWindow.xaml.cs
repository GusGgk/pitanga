using System.Collections;
using System.Windows;
using System.Windows.Media;
using ImageMagick;
using Pitanga.Core;
using Pitanga.ImageOps;
using Pitanga.UI;

namespace Pitanga.Tools;

public sealed record MetadataRow(string Key, string Value);

public partial class MetadataWindow : MacWindow
{
    readonly string _path;

    public MetadataWindow(string path)
    {
        _path = path;
        InitializeComponent();
        Title = $"Metadata — {Path.GetFileName(path)}";
        FileName.Text = Path.GetFileName(path);

        var rows = new List<MetadataRow>();
        bool hasGps = false;
        using (var img = new MagickImage(path))
        {
            var size = new FileInfo(path).Length;
            Summary.Text = $"{img.Format} · {img.Width} × {img.Height} · {OutputPaths.HumanSize(size)}";

            rows.Add(new("File", path));
            rows.Add(new("Format", img.Format.ToString()));
            rows.Add(new("Dimensions", $"{img.Width} × {img.Height} px"));
            rows.Add(new("File size", $"{OutputPaths.HumanSize(size)} ({size:N0} bytes)"));
            rows.Add(new("Color space", img.ColorSpace.ToString()));
            rows.Add(new("Bit depth", $"{img.Depth} bits/channel"));
            rows.Add(new("Resolution", $"{img.Density.X:0.#} × {img.Density.Y:0.#} {img.Density.Units}"));
            rows.Add(new("Has transparency", img.HasAlpha ? "Yes" : "No"));
            if (img.GetColorProfile() is { } icc) rows.Add(new("Color profile", icc.Description ?? icc.Model ?? "Embedded ICC"));
            if (img.GetXmpProfile() is not null) rows.Add(new("XMP", "Present"));
            if (img.GetIptcProfile() is not null) rows.Add(new("IPTC", "Present"));

            if (img.GetExifProfile() is { } exif)
            {
                foreach (var value in exif.Values.OrderBy(v => v.Tag.ToString()))
                {
                    var tag = value.Tag.ToString() ?? "?";
                    if (tag.StartsWith("GPS", StringComparison.OrdinalIgnoreCase)) hasGps = true;
                    rows.Add(new(tag, Describe(value.GetValue())));
                }
            }
        }

        Rows.ItemsSource = rows;
        GpsBadge.Visibility = hasGps ? Visibility.Visible : Visibility.Collapsed;

        var thumb = ImageIO.TryLoadThumbnail(path, 144);
        if (thumb != null) Thumb.Background = new ImageBrush(thumb) { Stretch = Stretch.UniformToFill };
    }

    static string Describe(object? value)
    {
        string text = value switch
        {
            null => "",
            byte[] bytes => $"{bytes.Length} bytes",
            string s => s,
            IEnumerable items => string.Join(", ", items.Cast<object?>().Take(8).Select(i => i?.ToString())),
            _ => value.ToString() ?? "",
        };
        return text.Length > 300 ? text[..300] + "…" : text.Trim('\0', ' ');
    }

    void Close_Click(object sender, RoutedEventArgs e) => Close();

    async void Strip_Click(object sender, RoutedEventArgs e)
    {
        StripButton.IsEnabled = false;
        try
        {
            var output = await Task.Run(() =>
            {
                using var img = new MagickImage(_path);
                img.AutoOrient(); // a orientação vive no EXIF: aplica nos pixels antes de removê-lo
                img.Strip();
                return ImageIO.WriteWithFallback(img, OutputPaths.WithSuffix(_path, "_NoMetadata"));
            });
            Toast.Show($"Saved {Path.GetFileName(output)}", "All metadata removed", ToastKind.Success, output);
            Close();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "metadata");
            Toast.Error(ex, "Could not remove metadata");
            StripButton.IsEnabled = true;
        }
    }
}
