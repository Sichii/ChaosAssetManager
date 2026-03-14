using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ChaosAssetManager.Helpers;
using ChaosAssetManager.Model;
using DALib.Drawing;
using MaterialDesignThemes.Wpf;
using SkiaSharp;
using SkiaSharp.Views.WPF;
using Button = System.Windows.Controls.Button;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Image = System.Windows.Controls.Image;
using Orientation = System.Windows.Controls.Orientation;

namespace ChaosAssetManager.Controls.HeaEditorControls;

// ReSharper disable once ClassCanBeSealed.Global
public partial class LightPrefabPickerEntry
{
    private LightPrefab? Prefab => DataContext as LightPrefab;

    public LightPrefabPickerEntry() => InitializeComponent();

    // ReSharper disable once AsyncVoidEventHandlerMethod
    private async void DeleteBtn_OnClick(object sender, RoutedEventArgs e)
    {
        if (Prefab is null)
            return;

        e.Handled = true;

        var dialogContent = new StackPanel();

        dialogContent.Children.Add(
            new TextBlock
            {
                Text = $"Delete prefab '{Prefab.Id}'?",
                Margin = new Thickness(
                    16,
                    16,
                    16,
                    0)
            });

        dialogContent.Children.Add(
            new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(16),
                Children =
                {
                    new Button
                    {
                        Content = "Cancel",
                        Margin = new Thickness(
                            0,
                            0,
                            8,
                            0),
                        Command = DialogHost.CloseDialogCommand,
                        CommandParameter = false
                    },
                    new Button
                    {
                        Content = "Delete",
                        Style = (Style)FindResource("MaterialDesignFlatButton"),
                        Command = DialogHost.CloseDialogCommand,
                        CommandParameter = true
                    }
                }
            });

        var result = await DialogHost.Show(dialogContent, "RootDialog");

        if (result is not true)
            return;

        LightPrefabRepository.Instance.Delete(Prefab.Id);

        //notify parent to refresh
        HeaEditorControl.Instance?.LoadPrefabsFromRepository();
    }

    private void LightPrefabPickerEntry_OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (Prefab is null)
        {
            ContentControl.Content = null;

            return;
        }

        IdText.Text = Prefab.Id;
        SizeText.Text = $"{Prefab.Width}x{Prefab.Height}";

        //render a grayscale preview of the prefab
        var previewSize = 48;
        var bitmap = RenderPrefabPreview(Prefab, previewSize);

        if (bitmap is not null)
        {
            var image = new Image
            {
                Source = bitmap.ToWriteableBitmap(),
                Width = previewSize,
                Height = previewSize,
                Stretch = Stretch.Uniform
            };

            ContentControl.Content = image;
            bitmap.Dispose();
        }
    }

    private static SKBitmap? RenderPrefabPreview(LightPrefab prefab, int targetSize)
    {
        if ((prefab.Width == 0) || (prefab.Height == 0))
            return null;

        var bitmap = new SKBitmap(
            prefab.Width,
            prefab.Height,
            SKColorType.Bgra8888,
            SKAlphaType.Unpremul);

        using var pixMap = bitmap.PeekPixels();
        var pixelBuffer = pixMap.GetPixelSpan<SKColor>();

        for (var y = 0; y < prefab.Height; y++)
            for (var x = 0; x < prefab.Width; x++)
            {
                var value = prefab.Data[y * prefab.Width + x];

                if (value == 0)
                {
                    pixelBuffer[y * prefab.Width + x] = SKColors.Black;

                    continue;
                }

                //map light value to grayscale (brighter = more light)
                var gray = (byte)(value * 255 / HeaFile.MAX_LIGHT_VALUE);

                pixelBuffer[y * prefab.Width + x] = new SKColor(
                    gray,
                    gray,
                    gray,
                    255);
            }

        //scale to target size
        var scale = Math.Min((float)targetSize / prefab.Width, (float)targetSize / prefab.Height);
        var scaledW = (int)(prefab.Width * scale);
        var scaledH = (int)(prefab.Height * scale);

        var scaled = bitmap.Resize(new SKImageInfo(scaledW, scaledH), new SKSamplingOptions(SKFilterMode.Linear));
        bitmap.Dispose();

        return scaled;
    }
}