using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Data;
using Chaos.Common.Synchronization;
using ChaosAssetManager.Helpers;
using ChaosAssetManager.Model;
using ChaosAssetManager.ViewModel;
using DALib.Definitions;
using DALib.Drawing;
using DALib.Utility;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using Graphics = DALib.Drawing.Graphics;

namespace ChaosAssetManager.Controls;

public sealed partial class EfaEditor : IDisposable, INotifyPropertyChanged
{
    private readonly SKImage BgImage;
    private readonly AutoReleasingMonitor Sync;
    private EfaFileViewModel _efaFileViewModel = null!;
    private EfaFrameViewModel? _efaFrameViewModel;
    private int _selectedFrameIndex;
    private Animation? Animation;
    private int CurrentFrameIndex;
    private bool Disposed;

    // ReSharper disable once NotAccessedField.Local
    private Task? ImageAnimationTask;
    private PeriodicTimer? ImageAnimationTimer;

    public EfaFileViewModel EfaFileViewModel
    {
        get => _efaFileViewModel;
        set => SetField(ref _efaFileViewModel, value);
    }

    public EfaFrameViewModel? EfaFrameViewModel
    {
        get => _efaFrameViewModel;

        set
        {
            SetField(ref _efaFrameViewModel, value);

            if (_efaFrameViewModel is not null)
                RenderFramePreview();
        }
    }

    public int SelectedFrameIndex
    {
        get => _selectedFrameIndex;

        set
        {
            SetField(ref _selectedFrameIndex, value);
            EfaFrameViewModel = value >= 0 ? EfaFileViewModel[value] : null;
        }
    }

    public EfaEditor(EfaFile efaFile)
    {
        Sync = new AutoReleasingMonitor();

        InitializeComponent();

        BlendingTypeCmbx.ItemsSource = new CollectionView(Enum.GetNames<EfaBlendingType>());
        FramesListView.ItemsSource = new CollectionView(Enumerable.Range(0, efaFile.Count));
        EfaFileViewModel = new EfaFileViewModel(efaFile);
        BgImage = ChaosAssetManager.Resources.previewbg.ToSKImage();
        SelectedFrameIndex = -1;

        RenderImagePreview();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        using var @lock = Sync.Enter();

        Animation?.Dispose();
        BgImage.Dispose();
        Disposed = true;
    }

    #region NotifyPropertyChanged
    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        OnPropertyChanged(propertyName);

        return true;
    }
    #endregion

    #region Frame
    private void RenderFramePreview()
    {
        using var @lock = Sync.Enter();

        if (SelectedFrameIndex < 0)
            return;

        FrameRenderElement.Redraw();
    }

    private void FrameApplyBtn_OnClick(object sender, RoutedEventArgs e)
    {
        using var @lock = Sync.Enter();

        if (SelectedFrameIndex < 0)
            return;

        var frame = EfaFrameViewModel;

        if (frame == null)
            return;

        if ((frame.CenterX > frame.ImagePixelWidth) || (frame.CenterY > frame.ImagePixelHeight))
        {
            Snackbar.MessageQueue!.Enqueue("Center point must lie within the image bounds");

            return;
        }

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

        Animation!.Frames[SelectedFrameIndex]
                  .Dispose();
        Animation!.Frames[SelectedFrameIndex] = Graphics.RenderImage(frame.EfaFrame, EfaFileViewModel.BlendingType);

        RenderFramePreview();
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
            var efaFrame = EfaFrameViewModel!.EfaFrame;

            // Draw the top image in the center
            using var paint = new SKPaint();
            paint.BlendMode = SKBlendMode.SrcATop;

            canvas.Scale(
                2.0f * dpiScale,
                2.0f * dpiScale,
                centerX,
                centerY);

            var left = centerX - efaFrame.CenterX - 2.17f;
            var top = centerY - efaFrame.CenterY + 33.66f;

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

            // Draw image bytes rect
            using var imageBytesPaint = new SKPaint();
            imageBytesPaint.Color = SKColors.Yellow;
            imageBytesPaint.Style = SKPaintStyle.Stroke;
            imageBytesPaint.StrokeWidth = 1;

            canvas.DrawRect(
                left + efaFrame.Left,
                top + efaFrame.Top,
                efaFrame.ByteWidth / 2f + efaFrame.Left,
                (float)efaFrame.ByteCount / efaFrame.ByteWidth + efaFrame.Top,
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

    #region Image
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

        var transformer = EfaFileViewModel.EfaFile.Select(frame => Graphics.RenderImage(frame, EfaFileViewModel.BlendingType));
        var images = new SKImageCollection(transformer);
        ImageAnimationTimer = new PeriodicTimer(TimeSpan.FromMilliseconds(EfaFileViewModel.FrameIntervalMs));
        Animation = new Animation(images, EfaFileViewModel.FrameIntervalMs);
        CurrentFrameIndex = 0;
    }

    private void ImageApplyBtn_OnClick(object sender, RoutedEventArgs e)
    {
        using var @lock = Sync.Enter();

        RenderImagePreview();
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
            var efaFrame = EfaFileViewModel[CurrentFrameIndex].EfaFrame;

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
                centerX - efaFrame.CenterX - 2.17f,
                centerY - efaFrame.CenterY + 33.66f,
                paint);
        } catch
        {
            //ignored
        }
    }
    #endregion
}