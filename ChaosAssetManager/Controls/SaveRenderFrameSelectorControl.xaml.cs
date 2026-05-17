using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ChaosAssetManager.Model;
using MaterialDesignThemes.Wpf;
using SkiaSharp;
using SkiaSharp.Views.WPF;
using Border = System.Windows.Controls.Border;
using Brushes = System.Windows.Media.Brushes;
using CheckBox = System.Windows.Controls.CheckBox;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Image = System.Windows.Controls.Image;
using Orientation = System.Windows.Controls.Orientation;

namespace ChaosAssetManager.Controls;

public sealed partial class SaveRenderFrameSelectorControl
{
    private const int THUMBNAIL_SIZE = 96;
    private const int CHECKER_CELL_SIZE = 8;

    private static readonly SKColor CheckerColorA = new(0x55, 0x55, 0x55);
    private static readonly SKColor CheckerColorB = new(0x33, 0x33, 0x33);

    private readonly List<CheckBox> FrameCheckBoxes = [];

    public SaveRenderFrameSelectorControl(Animation animation, string nameHint)
    {
        InitializeComponent();

        HeaderText.Text = $"Save Render — {nameHint} ({animation.Frames.Count} frames)";

        for (var i = 0; i < animation.Frames.Count; i++)
        {
            var card = BuildFrameCard(animation.Frames[i], i);
            FramesList.Items.Add(card);
        }
    }

    /// <summary>
    ///     Hosts this control as a Material Design dialog and returns the selected frame indices
    ///     in ascending order, or null if the user cancelled.
    /// </summary>
    public async Task<IReadOnlyList<int>?> ShowAsync()
    {
        var result = await DialogHost.Show(this, "RootDialog");

        if (result is IReadOnlyList<int> indices)
            return indices;

        return null;
    }

    private FrameworkElement BuildFrameCard(SKImage frame, int frameIndex)
    {
        using var thumbnailBitmap = RenderThumbnail(frame);

        var image = new Image
        {
            Source = thumbnailBitmap.ToWriteableBitmap(),
            Width = THUMBNAIL_SIZE,
            Height = THUMBNAIL_SIZE,
            Stretch = Stretch.None,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        var label = new TextBlock
        {
            Text = $"Frame {frameIndex}",
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 4, 0, 0),
            FontSize = 12
        };

        var checkBox = new CheckBox
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 4, 0, 0),
            IsChecked = false,
            Tag = frameIndex
        };
        checkBox.Checked += FrameCheckBox_OnChanged;
        checkBox.Unchecked += FrameCheckBox_OnChanged;
        FrameCheckBoxes.Add(checkBox);

        var stack = new StackPanel
        {
            Orientation = Orientation.Vertical,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        stack.Children.Add(image);
        stack.Children.Add(label);
        stack.Children.Add(checkBox);

        return new Border
        {
            Margin = new Thickness(4),
            Padding = new Thickness(6),
            BorderBrush = Brushes.Gray,
            BorderThickness = new Thickness(1),
            Child = stack
        };
    }

    private static SKBitmap RenderThumbnail(SKImage frame)
    {
        var bitmap = new SKBitmap(THUMBNAIL_SIZE, THUMBNAIL_SIZE, SKColorType.Bgra8888, SKAlphaType.Premul);

        using var canvas = new SKCanvas(bitmap);
        DrawCheckerBackground(canvas);

        var scale = Math.Min(
            THUMBNAIL_SIZE / (float)frame.Width,
            THUMBNAIL_SIZE / (float)frame.Height);

        if (scale > 1f)
            scale = 1f;

        var drawWidth = frame.Width * scale;
        var drawHeight = frame.Height * scale;
        var offsetX = (THUMBNAIL_SIZE - drawWidth) / 2f;
        var offsetY = (THUMBNAIL_SIZE - drawHeight) / 2f;

        var destRect = new SKRect(
            offsetX,
            offsetY,
            offsetX + drawWidth,
            offsetY + drawHeight);

        canvas.DrawImage(frame, destRect);

        return bitmap;
    }

    private static void DrawCheckerBackground(SKCanvas canvas)
    {
        using var paintA = new SKPaint { Color = CheckerColorA };
        using var paintB = new SKPaint { Color = CheckerColorB };

        for (var y = 0; y < THUMBNAIL_SIZE; y += CHECKER_CELL_SIZE)
        for (var x = 0; x < THUMBNAIL_SIZE; x += CHECKER_CELL_SIZE)
        {
            var useA = ((x / CHECKER_CELL_SIZE) + (y / CHECKER_CELL_SIZE)) % 2 == 0;
            var paint = useA ? paintA : paintB;

            canvas.DrawRect(
                x,
                y,
                CHECKER_CELL_SIZE,
                CHECKER_CELL_SIZE,
                paint);
        }
    }

    private void FrameCheckBox_OnChanged(object sender, RoutedEventArgs e) => UpdateSaveButton();

    private void UpdateSaveButton()
    {
        var count = FrameCheckBoxes.Count(cb => cb.IsChecked == true);
        SaveBtn.Content = $"Save ({count})";
        SaveBtn.IsEnabled = count > 0;
    }

    private void SelectAllBtn_OnClick(object sender, RoutedEventArgs e)
    {
        foreach (var cb in FrameCheckBoxes)
            cb.IsChecked = true;
    }

    private void SelectNoneBtn_OnClick(object sender, RoutedEventArgs e)
    {
        foreach (var cb in FrameCheckBoxes)
            cb.IsChecked = false;
    }

    private void CancelBtn_OnClick(object sender, RoutedEventArgs e) => DialogHost.CloseDialogCommand.Execute(null, this);

    private void SaveBtn_OnClick(object sender, RoutedEventArgs e)
    {
        var selected = FrameCheckBoxes
                       .Where(cb => cb.IsChecked == true)
                       .Select(cb => (int)cb.Tag)
                       .OrderBy(i => i)
                       .ToList();

        DialogHost.CloseDialogCommand.Execute(selected, this);
    }
}
