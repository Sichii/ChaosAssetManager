using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Chaos.Common.Synchronization;
using ChaosAssetManager.Helpers;
using ChaosAssetManager.Model;
using DALib.Definitions;
using DALib.Drawing;
using DALib.Utility;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using Graphics = DALib.Drawing.Graphics;

namespace ChaosAssetManager.Controls;

public sealed partial class EfaEditor : IDisposable
{
    private readonly SKImage BgImage;
    private readonly EfaFile EfaImage;
    private readonly AutoReleasingMonitor Sync;
    private Animation? Animation;
    private int CurrentFrameIndex;
    private bool Disposed;

    // ReSharper disable once NotAccessedField.Local
    private Task? ImageAnimationTask;
    private PeriodicTimer? ImageAnimationTimer;
    private int SelectedFrameIndex;

    public EfaEditor(EfaFile efaImage)
    {
        Sync = new AutoReleasingMonitor();
        EfaImage = efaImage;
        BgImage = ChaosAssetManager.Resources.previewbg.ToSKImage();
        SelectedFrameIndex = -1;

        InitializeComponent();

        BlendingTypeCmbx.Text = EfaImage.BlendingType.ToString();
        FrameInvervalMsTbox.Text = EfaImage.FrameIntervalMs.ToString();
        FramesListView.ItemsSource = new CollectionView(Enumerable.Range(0, EfaImage.Count));

        RenderImagePreview();
        RenderFramePreview();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        using var @lock = Sync.Enter();

        Animation?.Dispose();
        BgImage.Dispose();
        Disposed = true;
    }

    #region Shared
    private void IntegerValidation_OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        using var @lock = Sync.Enter();

        foreach (var c in e.Text)
            if (!char.IsDigit(c))
            {
                e.Handled = true;

                return;
            }
    }
    #endregion

    #region FrameRender
    private void RenderFramePreview()
    {
        using var @lock = Sync.Enter();

        if (SelectedFrameIndex < 0)
            return;

        FrameRenderElement.Redraw();
    }
    #endregion

    #region FrameTab Events
    private void FrameApplyBtn_OnClick(object sender, RoutedEventArgs e)
    {
        using var @lock = Sync.Enter();

        if (SelectedFrameIndex < 0)
            return;

        var frame = EfaImage[SelectedFrameIndex];

        if (frame.FramePixelWidth > frame.ImagePixelWidth)
        {
            Snackbar.MessageQueue!.Enqueue("Frame width cannot be greater than image width");

            return;
        }

        if (frame.FramePixelHeight > frame.ImagePixelHeight)
        {
            Snackbar.MessageQueue!.Enqueue("Frame height cannot be greater than image height");

            return;
        }

        //re-render selected frame
        var selectedFrame = EfaImage[SelectedFrameIndex];

        Animation!.Frames[SelectedFrameIndex]
                  .Dispose();
        Animation!.Frames[SelectedFrameIndex] = Graphics.RenderImage(selectedFrame, EfaImage.BlendingType);

        RenderFramePreview();
    }

    private void FramesListView_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        using var @lock = Sync.Enter();

        SelectedFrameIndex = FramesListView.SelectedIndex;

        if (SelectedFrameIndex < 0)
            return;

        try
        {
            var frame = EfaImage[SelectedFrameIndex];

            LeftTbox.Text = frame.Left.ToString();
            TopTbox.Text = frame.Top.ToString();
            CenterXTbox.Text = frame.CenterX.ToString();
            CenterYTbox.Text = frame.CenterY.ToString();
            FramePixelWidthTbox.Text = frame.FramePixelWidth.ToString();
            FramePixelHeightTbox.Text = frame.FramePixelHeight.ToString();
            ImagePixelWidthTbox.Text = frame.ImagePixelWidth.ToString();
            ImagePixelHeightTbox.Text = frame.ImagePixelHeight.ToString();

            RenderFramePreview();
        } catch
        {
            //ignored
        }
    }

    private void LeftTbox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        using var @lock = Sync.Enter();

        if (SelectedFrameIndex < 0)
            return;

        if (!short.TryParse(LeftTbox.Text, out var newLeft))
            return;

        var frame = EfaImage[SelectedFrameIndex];
        frame.Left = newLeft;
    }

    private void TopTbox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        using var @lock = Sync.Enter();

        if (SelectedFrameIndex < 0)
            return;

        if (!short.TryParse(TopTbox.Text, out var newTop))
            return;

        var frame = EfaImage[SelectedFrameIndex];
        frame.Top = newTop;
    }

    private void CenterXTbox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        using var @lock = Sync.Enter();

        if (SelectedFrameIndex < 0)
            return;

        if (!short.TryParse(CenterXTbox.Text, out var newCenterX))
            return;

        var frame = EfaImage[SelectedFrameIndex];
        frame.CenterX = newCenterX;
    }

    private void CenterYTbox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        using var @lock = Sync.Enter();

        if (SelectedFrameIndex < 0)
            return;

        if (!short.TryParse(CenterYTbox.Text, out var newCenterY))
            return;

        var frame = EfaImage[SelectedFrameIndex];
        frame.CenterY = newCenterY;
    }

    private void FramePixelWidthTbox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        using var @lock = Sync.Enter();

        if (SelectedFrameIndex < 0)
            return;

        if (!short.TryParse(FramePixelWidthTbox.Text, out var newFramePixelWidth))
            return;

        var frame = EfaImage[SelectedFrameIndex];
        frame.FramePixelWidth = newFramePixelWidth;
    }

    private void FramePixelHeightTbox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        using var @lock = Sync.Enter();

        if (SelectedFrameIndex < 0)
            return;

        if (!short.TryParse(FramePixelHeightTbox.Text, out var newFramePixelHeight))
            return;

        var frame = EfaImage[SelectedFrameIndex];
        frame.FramePixelHeight = newFramePixelHeight;
    }

    private void ImagePixelWidthTbox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        using var @lock = Sync.Enter();

        if (SelectedFrameIndex < 0)
            return;

        if (!short.TryParse(ImagePixelWidthTbox.Text, out var newImagePixelWidth))
            return;

        var frame = EfaImage[SelectedFrameIndex];
        frame.ImagePixelWidth = newImagePixelWidth;
    }

    private void ImagePixelHeightTbox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        using var @lock = Sync.Enter();

        if (SelectedFrameIndex < 0)
            return;

        if (!short.TryParse(ImagePixelHeightTbox.Text, out var newImagePixelHeight))
            return;

        var frame = EfaImage[SelectedFrameIndex];
        frame.ImagePixelHeight = newImagePixelHeight;
    }

    private void FrameRenderElement_OnPaint(object? sender, SKPaintSurfaceEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(Animation);

        if (SelectedFrameIndex < 0)
            return;

        try
        {
            using var @lock = Sync.Enter();

            if (Disposed)
                return;

            var frame = Animation!.Frames[SelectedFrameIndex];
            var canvas = e.Surface.Canvas;
            var dpiScale = (float)DpiHelper.GetDpiScaleFactor();
            var imageScale = 1.5f / dpiScale;
            var centerX = BgImage.Width / 2f / imageScale;
            var centerY = BgImage.Height / 2f / imageScale;

            // Sometimes frames can be null
            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            if (frame is null)
                return;

            // Draw the background image without additional scaling
            canvas.DrawImage(BgImage, 0, 0);

            // Calculate the position for the top image
            var efaFrame = EfaImage[SelectedFrameIndex];

            // Draw the top image in the center
            using var paint = new SKPaint();
            paint.BlendMode = SKBlendMode.SrcATop;

            canvas.Scale(
                2.0f * dpiScale,
                2.0f * dpiScale,
                centerX,
                centerY);

            var left = centerX - efaFrame.CenterX - 2;
            var top = centerY - efaFrame.CenterY + 35;

            canvas.DrawImage(
                frame,
                left,
                top,
                paint);

            // Draw the image rectangle
            using var imagePaint = new SKPaint();
            imagePaint.Color = SKColors.Blue;
            imagePaint.Style = SKPaintStyle.Stroke;
            imagePaint.StrokeWidth = 2;

            canvas.DrawRect(
                left,
                top,
                efaFrame.ImagePixelWidth,
                efaFrame.ImagePixelHeight,
                imagePaint);

            // Draw the frame rectangle
            using var framePaint = new SKPaint();
            framePaint.Color = SKColors.Red;
            framePaint.Style = SKPaintStyle.Stroke;
            framePaint.StrokeWidth = 1;

            canvas.DrawRect(
                left + efaFrame.Left,
                top + efaFrame.Top,
                efaFrame.FramePixelWidth,
                efaFrame.FramePixelHeight,
                framePaint);

            // Draw the center point
            using var centerPaint = new SKPaint();
            centerPaint.Color = SKColors.Fuchsia;
            centerPaint.Style = SKPaintStyle.Fill;

            canvas.DrawCircle(
                left + efaFrame.CenterX,
                top + efaFrame.CenterY,
                2,
                centerPaint);

            // Draw the top left point
            using var topLeftPaint = new SKPaint();
            topLeftPaint.Color = SKColors.Yellow;
            topLeftPaint.Style = SKPaintStyle.Fill;

            canvas.DrawCircle(
                left + efaFrame.Left,
                top + efaFrame.Top,
                2,
                topLeftPaint);
        } catch
        {
            //ignored
        }
    }

    private void FrameRenderElement_OnElementLoaded(object? sender, RoutedEventArgs e)
    {
        try
        {
            var dpiScale = (float)DpiHelper.GetDpiScaleFactor();
            var elementWidth = FrameRenderElement.ActualWidth * dpiScale;
            var elementHeight = FrameRenderElement.ActualHeight * dpiScale;
            var imageScale = 1.5f / dpiScale;

            //calculate the translation to center the image
            var translateX = (float)(elementWidth - BgImage.Width / imageScale) / 2f;
            var translateY = (float)(elementHeight - BgImage.Height / imageScale) / 2f;

            //center the image
            FrameRenderElement.Matrix = SKMatrix.CreateTranslation(translateX, translateY);
            FrameRenderElement.Redraw();
        } catch
        {
            //ignored
        }
    }
    #endregion

    #region ImageRender
    private async Task AnimateElement()
    {
        ArgumentNullException.ThrowIfNull(Animation);
        ArgumentNullException.ThrowIfNull(ImageAnimationTimer);

        try
        {
            while (true)
            {
                await ImageAnimationTimer.WaitForNextTickAsync();

                using var @lock = Sync.Enter();

                if (Disposed)
                    return;

                CurrentFrameIndex = (CurrentFrameIndex + 1) % Animation.Frames.Count;
                ImageRenderElement.Redraw();
            }
        } catch
        {
            //ignored
        }
    }

    private void RenderImagePreview()
    {
        using var @lock = Sync.Enter();

        Animation?.Dispose();

        var transformer = EfaImage.Select(frame => Graphics.RenderImage(frame, EfaImage.BlendingType));
        var images = new SKImageCollection(transformer);
        ImageAnimationTimer = new PeriodicTimer(TimeSpan.FromMilliseconds(EfaImage.FrameIntervalMs));
        Animation = new Animation(images, EfaImage.FrameIntervalMs);
        CurrentFrameIndex = 0;
    }
    #endregion

    #region ImageTab Events
    private void BlendingTypeCmbx_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        using var @lock = Sync.Enter();

        var selectedItem = BlendingTypeCmbx.SelectedItem;

        if (selectedItem is ComboBoxItem cbxItem && Enum.TryParse(cbxItem.Content.ToString(), true, out EfaBlendingType newBlendingType))
            EfaImage.BlendingType = newBlendingType;
    }

    private void ImageApplyBtn_OnClick(object sender, RoutedEventArgs e)
    {
        using var @lock = Sync.Enter();

        RenderImagePreview();
    }

    private void FrameInvervalMsTbox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        using var @lock = Sync.Enter();

        //set frame interval
        if (int.TryParse(FrameInvervalMsTbox.Text, out var newFrameIntervalMs))
            EfaImage.FrameIntervalMs = newFrameIntervalMs;
    }

    private void ImageRenderElement_OnElementLoaded(object? sender, RoutedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(Animation);

        try
        {
            var dpiScale = (float)DpiHelper.GetDpiScaleFactor();
            var elementWidth = ImageRenderElement.ActualWidth * dpiScale;
            var elementHeight = ImageRenderElement.ActualHeight * dpiScale;
            var imageScale = 1.5f / dpiScale;

            //calculate the translation to center the image
            var translateX = (float)(elementWidth - BgImage.Width / imageScale) / 2f;
            var translateY = (float)(elementHeight - BgImage.Height / imageScale) / 2f;

            //center the image
            ImageRenderElement.Matrix = SKMatrix.CreateTranslation(translateX, translateY);
            ImageRenderElement.Redraw();

            if (Animation.Frames.Count > 1)
                ImageAnimationTask ??= AnimateElement();
        } catch
        {
            //ignored
        }
    }

    private void ImageRenderElement_OnPaint(object? sender, SKPaintSurfaceEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(Animation);

        try
        {
            using var @lock = Sync.Enter();

            if (Disposed)
                return;

            var canvas = e.Surface.Canvas;
            var frame = Animation.Frames[CurrentFrameIndex];
            var dpiScale = (float)DpiHelper.GetDpiScaleFactor();
            var imageScale = 1.5f / dpiScale;
            var centerX = BgImage.Width / 2f / imageScale;
            var centerY = BgImage.Height / 2f / imageScale;

            // Sometimes frames can be null
            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            if (frame is null)
                return;

            // Draw the background image without additional scaling
            canvas.DrawImage(BgImage, 0, 0);

            // Calculate the position for the top image
            var efaFrame = EfaImage[CurrentFrameIndex];

            // Draw the top image in the center
            using var paint = new SKPaint();
            paint.BlendMode = SKBlendMode.SrcATop;

            canvas.Scale(
                2.0f * dpiScale,
                2.0f * dpiScale,
                centerX,
                centerY);

            canvas.DrawImage(
                frame,
                centerX - efaFrame.CenterX - 2,
                centerY - efaFrame.CenterY + 35,
                paint);
        } catch
        {
            //ignored
        }
    }
    #endregion
}