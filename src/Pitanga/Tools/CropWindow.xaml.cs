using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Pitanga.Core;
using Pitanga.ImageOps;
using Pitanga.UI;

namespace Pitanga.Tools;

public partial class CropWindow : MacWindow
{
    readonly string _path;
    bool _syncing;

    public CropWindow(string path)
    {
        _path = path;
        InitializeComponent();
        Title = $"Crop — {Path.GetFileName(path)}";

        var image = ImageIO.Load(path, maxSide: 2400);
        CropArea.CropChanged += (_, _) => SyncFields();
        CropArea.SetImage(image.Bitmap, image.Width, image.Height);
    }

    void SyncFields()
    {
        _syncing = true;
        WidthBox.Text = Math.Round(CropArea.Crop.Width).ToString(CultureInfo.InvariantCulture);
        HeightBox.Text = Math.Round(CropArea.Crop.Height).ToString(CultureInfo.InvariantCulture);
        _syncing = false;
    }

    void Ratio_Checked(object sender, RoutedEventArgs e)
    {
        if (CropArea is null || !CropArea.HasImage) return;
        var tag = (string)((RadioButton)sender).Tag;
        CropArea.SetRatio(double.Parse(tag, CultureInfo.InvariantCulture));
    }

    void Size_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        Commit(sender);
        e.Handled = true; // Enter no campo não dispara o Apply
    }

    void Size_LostFocus(object sender, KeyboardFocusChangedEventArgs e) => Commit(sender);

    void Commit(object sender)
    {
        if (_syncing) return;
        if (double.TryParse(WidthBox.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var w) &&
            double.TryParse(HeightBox.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var h) && w > 0 && h > 0)
        {
            CropArea.SetSize(w, h, widthDriven: ReferenceEquals(sender, WidthBox));
        }
        SyncFields();
    }

    void Reset_Click(object sender, RoutedEventArgs e)
    {
        FreeRatio.IsChecked = true;
        CropArea.Reset();
    }

    async void Apply_Click(object sender, RoutedEventArgs e)
    {
        ApplyButton.IsEnabled = false;
        var crop = CropArea.Crop;
        try
        {
            var output = await Task.Run(() => ImageIO.CropToFile(_path, crop));
            Toast.Show($"Saved {Path.GetFileName(output)}", $"{Math.Round(crop.Width)} × {Math.Round(crop.Height)}", ToastKind.Success, output);
            Close();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "crop");
            Toast.Error(ex, "Crop failed");
            ApplyButton.IsEnabled = true;
        }
    }
}
