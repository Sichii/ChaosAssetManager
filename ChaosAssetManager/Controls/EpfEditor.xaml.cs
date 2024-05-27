using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Chaos.Common.Synchronization;
using ChaosAssetManager.Helpers;
using ChaosAssetManager.Model;
using DALib.Drawing;
using DALib.Utility;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using Graphics = DALib.Drawing.Graphics;

// ReSharper disable SpecifyACultureInStringConversionExplicitly

namespace ChaosAssetManager.Controls;

public sealed partial class EpfEditor : IDisposable
{
    private readonly SKImage BgImage;
    private readonly List<SKPoint> CenterPoints;
    private readonly EpfFile EpfImage;
    private readonly Palette Palette;
    private readonly AutoReleasingMonitor Sync;
    private Animation? Animation;
    private int CurrentFrameIndex;
    private bool Disposed;

    // ReSharper disable once NotAccessedField.Local
    private Task? ImageAnimationTask;
    private PeriodicTimer? ImageAnimationTimer;
    private int SelectedFrameIndex;

    public EpfEditor(EpfFile epfImage, Palette palette, List<SKPoint>? centerPoints = null)
    {
        Sync = new AutoReleasingMonitor();
        EpfImage = epfImage;
        Palette = palette;

        CenterPoints = centerPoints
                       ?? Enumerable.Range(0, EpfImage.Count)
                                    .Select(_ => new SKPoint(EpfImage.PixelWidth / 2f, EpfImage.PixelHeight / 2f))
                                    .ToList();
        BgImage = ChaosAssetManager.Resources.previewbg.ToSKImage();
        SelectedFrameIndex = -1;

        if (CenterPoints.Count < EpfImage.Count)
        {
            var lastPoint = CenterPoints.Last();
            var insertCount = EpfImage.Count - CenterPoints.Count;

            for (var i = 0; i < insertCount; i++)
                CenterPoints.Add(lastPoint);
        }

        InitializeComponent();

        PixelWidthTbox.Text = EpfImage.PixelWidth.ToString();
        PixelHeightTbox.Text = EpfImage.PixelHeight.ToString();
        FramesListView.ItemsSource = new CollectionView(Enumerable.Range(0, EpfImage.Count));

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
    private void CenterXTbox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        using var @lock = Sync.Enter();

        if (SelectedFrameIndex < 0)
            return;

        if (!short.TryParse(CenterXTbox.Text, out var newCenterX))
            return;

        var currentCenter = CenterPoints[SelectedFrameIndex];
        currentCenter.X = newCenterX;
        CenterPoints[SelectedFrameIndex] = currentCenter;
    }

    private void CenterYTbox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        using var @lock = Sync.Enter();

        if (SelectedFrameIndex < 0)
            return;

        if (!short.TryParse(CenterYTbox.Text, out var newCenterX))
            return;

        var currentCenter = CenterPoints[SelectedFrameIndex];
        currentCenter.Y = newCenterX;
        CenterPoints[SelectedFrameIndex] = currentCenter;
    }

    private void FrameApplyBtn_OnClick(object sender, RoutedEventArgs e)
    {
        using var @lock = Sync.Enter();

        if (SelectedFrameIndex < 0)
            return;

        var frame = EpfImage[SelectedFrameIndex];
        var expectedLength = frame.PixelWidth * frame.PixelHeight * 2;

        if (expectedLength > frame.Data.Length)
        {
            Snackbar.MessageQueue!.Enqueue(
                "The width or height of the image are higher than expected. Width x Height must be less than or equal to the image data length.");

            return;
        }

        //re-render selected frame
        var selectedFrame = EpfImage[SelectedFrameIndex];

        Animation!.Frames[SelectedFrameIndex]
                  .Dispose();
        Animation!.Frames[SelectedFrameIndex] = Graphics.RenderImage(selectedFrame, Palette);

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
            var frame = EpfImage[SelectedFrameIndex];

            LeftTbox.Text = frame.Left.ToString();
            TopTbox.Text = frame.Top.ToString();
            RightTbox.Text = frame.Right.ToString();
            BottomTbox.Text = frame.Bottom.ToString();
            CenterXTbox.Text = ((short)CenterPoints[SelectedFrameIndex].X).ToString();
            CenterYTbox.Text = ((short)CenterPoints[SelectedFrameIndex].Y).ToString();

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

        var frame = EpfImage[SelectedFrameIndex];
        frame.Left = newLeft;
    }

    private void TopTbox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        using var @lock = Sync.Enter();

        if (SelectedFrameIndex < 0)
            return;

        if (!short.TryParse(TopTbox.Text, out var newtop))
            return;

        var frame = EpfImage[SelectedFrameIndex];
        frame.Top = newtop;
    }

    private void RightTbox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        using var @lock = Sync.Enter();

        if (SelectedFrameIndex < 0)
            return;

        if (!short.TryParse(RightTbox.Text, out var newRight))
            return;

        var frame = EpfImage[SelectedFrameIndex];
        frame.Right = newRight;
    }

    private void BottomTbox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        using var @lock = Sync.Enter();

        if (SelectedFrameIndex < 0)
            return;

        if (!short.TryParse(BottomTbox.Text, out var newBottom))
            return;

        var frame = EpfImage[SelectedFrameIndex];
        frame.Bottom = newBottom;
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
            var epfFrame = EpfImage[SelectedFrameIndex];

            // Draw the top image in the center
            using var paint = new SKPaint();
            paint.BlendMode = SKBlendMode.SrcATop;

            canvas.Scale(
                2.0f * dpiScale,
                2.0f * dpiScale,
                centerX,
                centerY);

            var frameCenterX = CenterPoints[SelectedFrameIndex].X;
            var frameCenterY = CenterPoints[SelectedFrameIndex].Y;
            var left = centerX - frameCenterX - 2.17f;
            var top = centerY - frameCenterY + 33.66f;

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
                EpfImage.PixelWidth,
                EpfImage.PixelHeight,
                imagePaint);

            // Draw the center point
            using var centerPaint = new SKPaint();
            centerPaint.Color = SKColors.Fuchsia;
            centerPaint.Style = SKPaintStyle.Fill;

            canvas.DrawCircle(
                left + frameCenterX,
                top + frameCenterY,
                2,
                centerPaint);

            // Draw the top left point
            using var topLeftPaint = new SKPaint();
            topLeftPaint.Color = SKColors.Yellow;
            topLeftPaint.Style = SKPaintStyle.Fill;

            canvas.DrawCircle(
                left + epfFrame.Left,
                top + epfFrame.Top,
                2,
                topLeftPaint);

            // Draw image bytes rect
            using var imageBytesPaint = new SKPaint();
            imageBytesPaint.Color = SKColors.Yellow;
            imageBytesPaint.Style = SKPaintStyle.Stroke;
            imageBytesPaint.StrokeWidth = 1;

            canvas.DrawRect(
                left + epfFrame.Left,
                top + epfFrame.Top,
                epfFrame.PixelWidth,
                epfFrame.PixelHeight,
                imageBytesPaint);
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

        var transformer = EpfImage.Select(frame => Graphics.RenderImage(frame, Palette));
        var images = new SKImageCollection(transformer);
        Animation = new Animation(images);
        ImageAnimationTimer = new PeriodicTimer(TimeSpan.FromMilliseconds(Animation.FrameIntervalMs));
        CurrentFrameIndex = 0;
    }
    #endregion

    #region ImageTab Events
    private void ImageApplyBtn_OnClick(object sender, RoutedEventArgs e)
    {
        using var @lock = Sync.Enter();

        RenderImagePreview();
    }

    private void PixelWidthTbox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        using var @lock = Sync.Enter();

        //set frame interval
        if (short.TryParse(PixelWidthTbox.Text, out var newPixelWidth))
            EpfImage.PixelWidth = newPixelWidth;
    }

    private void PixelHeightTbox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        using var @lock = Sync.Enter();

        //set frame interval
        if (short.TryParse(PixelHeightTbox.Text, out var newPixelHeight))
            EpfImage.PixelHeight = newPixelHeight;
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
            var epfFrame = EpfImage[CurrentFrameIndex];
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

            var frameCenterX = CenterPoints[CurrentFrameIndex].X;
            var frameCenterY = CenterPoints[CurrentFrameIndex].Y;
            var left = centerX - frameCenterX - 2.17f;
            var top = centerY - frameCenterY + 33.66f;

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
                left,
                top,
                paint);
        } catch
        {
            //ignored
        }
    }
    #endregion
}