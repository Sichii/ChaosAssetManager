using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Data;
using ChaosAssetManager.Extensions;
using ChaosAssetManager.Helpers;
using ChaosAssetManager.Model;
using ChaosAssetManager.ViewModel;
using DALib.Drawing;
using DALib.Utility;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using Graphics = DALib.Drawing.Graphics;

// ReSharper disable SpecifyACultureInStringConversionExplicitly

namespace ChaosAssetManager.Controls;

public sealed partial class EpfEditor : IDisposable, INotifyPropertyChanged
{
    private readonly SKImage BgImage;
    private readonly List<SKPoint> CenterPoints;
    private readonly Palette Palette;
    private readonly Lock Sync;
    private EpfFileViewModel _epfFileViewModel = null!;
    private EpfFrameViewModel? _epfFrameViewModel;
    private int _selectedFrameIndex;

    private Animation? Animation;
    private int CurrentFrameIndex;
    private bool Disposed;

    // ReSharper disable once NotAccessedField.Local
    private Task? ImageAnimationTask;
    private PeriodicTimer? ImageAnimationTimer;

    public int? CurrentCenterX
    {
        get
        {
            if (_epfFrameViewModel is null)
                return null;

            return (int)CenterPoints[SelectedFrameIndex].X;
        }

        set
        {
            if (value is null || _epfFrameViewModel is null)
                return;

            var currentCenterPoint = CenterPoints[SelectedFrameIndex];

            CenterPoints[SelectedFrameIndex] = new SKPoint(value.Value, currentCenterPoint.Y);

            OnPropertyChanged();
        }
    }

    public int? CurrentCenterY
    {
        get
        {
            if (_epfFrameViewModel is null)
                return null;

            return (int)CenterPoints[SelectedFrameIndex].Y;
        }

        set
        {
            if (value is null || _epfFrameViewModel is null)
                return;

            var currentCenterPoint = CenterPoints[SelectedFrameIndex];

            CenterPoints[SelectedFrameIndex] = new SKPoint(currentCenterPoint.X, value.Value);

            OnPropertyChanged();
        }
    }

    public EpfFileViewModel EpfFileViewModel
    {
        get => _epfFileViewModel;
        set => SetField(ref _epfFileViewModel, value);
    }

    public EpfFrameViewModel? EpfFrameViewModel
    {
        get => _epfFrameViewModel;

        set
        {
            SetField(ref _epfFrameViewModel, value);

            if (_epfFrameViewModel is not null)
                RenderFramePreview();
        }
    }

    public int SelectedFrameIndex
    {
        get => _selectedFrameIndex;

        set
        {
            SetField(ref _selectedFrameIndex, value);
            EpfFrameViewModel = value >= 0 ? EpfFileViewModel[value] : null;
            OnPropertyChanged(nameof(CurrentCenterX));
            OnPropertyChanged(nameof(CurrentCenterY));
        }
    }

    public EpfEditor(EpfFile epfImage, Palette palette, List<SKPoint>? centerPoints = null)
    {
        Sync = new Lock();

        InitializeComponent();

        Palette = palette;

        CenterPoints = centerPoints
                       ?? Enumerable.Range(0, epfImage.Count)
                                    .Select(_ => new SKPoint(epfImage.PixelWidth / 2f, epfImage.PixelHeight / 2f))
                                    .ToList();
        FramesListView.ItemsSource = new CollectionView(Enumerable.Range(0, epfImage.Count));
        EpfFileViewModel = new EpfFileViewModel(epfImage);
        BgImage = ChaosAssetManager.Resources.previewbg.ToSkImage();
        SelectedFrameIndex = 0;

        if (CenterPoints.Count < epfImage.Count)
        {
            var lastPoint = CenterPoints.Last();
            var insertCount = epfImage.Count - CenterPoints.Count;

            for (var i = 0; i < insertCount; i++)
                CenterPoints.Add(lastPoint);
        }

        RenderImagePreview();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        using var @lock = Sync.EnterScope();

        Animation?.Dispose();
        BgImage.Dispose();
        Disposed = true;
    }

    #region NotifyPropertyChanged
    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    // ReSharper disable once UnusedMethodReturnValue.Local
    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        OnPropertyChanged(propertyName);

        return true;
    }
    #endregion

    #region Frame Events
    private void FrameApplyBtn_OnClick(object sender, RoutedEventArgs e)
    {
        using var @lock = Sync.EnterScope();

        if (SelectedFrameIndex < 0)
            return;

        var frame = EpfFrameViewModel;

        if (frame == null)
            return;

        var expectedLength = frame.PixelWidth * frame.PixelHeight;

        if (expectedLength > frame.EpfFrame.Data.Length)
        {
            Snackbar.MessageQueue!.Enqueue(
                "The width or height of the image are higher than expected. Width x Height must be less than or equal to the image data length.");

            return;
        }

        Animation!.Frames[SelectedFrameIndex]
                  .Dispose();
        Animation!.Frames[SelectedFrameIndex] = Graphics.RenderImage(frame.EpfFrame, Palette);

        RenderFramePreview();
    }

    private void RenderFramePreview()
    {
        using var @lock = Sync.EnterScope();

        if (SelectedFrameIndex < 0)
            return;

        FrameRenderElement.Redraw();
    }

    private void FrameRenderElement_OnPaint(object? sender, SKPaintGLSurfaceEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(Animation);

        if (SelectedFrameIndex < 0)
            return;

        try
        {
            using var @lock = Sync.EnterScope();

            if (Disposed)
                return;

            var frame = Animation!.Frames[SelectedFrameIndex];
            var canvas = e.Surface.Canvas;
            var dpiScale = (float)DpiHelper.GetDpiScaleFactor();
            var imageScale = 1.5f / dpiScale;
            var centerX = BgImage.Width / 2f / imageScale;
            var centerY = BgImage.Height / 2f / imageScale;

            // sometimes frames can be null
            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            if (frame is null)
                return;

            // draw the background image without additional scaling
            canvas.DrawImage(BgImage, 0, 0);

            var epfFrame = EpfFrameViewModel!.EpfFrame;

            // draw the top image in the center
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
                EpfFileViewModel.PixelWidth,
                EpfFileViewModel.PixelHeight,
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

    #region Image Events
    private void ImageApplyBtn_OnClick(object sender, RoutedEventArgs e)
    {
        using var @lock = Sync.EnterScope();

        RenderImagePreview();
    }

    private async Task AnimateElement()
    {
        ArgumentNullException.ThrowIfNull(Animation);
        ArgumentNullException.ThrowIfNull(ImageAnimationTimer);

        try
        {
            while (true)
            {
                await ImageAnimationTimer.WaitForNextTickAsync();

                using var @lock = Sync.EnterScope();

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
        using var @lock = Sync.EnterScope();

        Animation?.Dispose();

        var transformer = EpfFileViewModel.EpfFile.Select(frame => Graphics.RenderImage(frame, Palette));
        var images = new SKImageCollection(transformer);
        Animation = new Animation(images);
        ImageAnimationTimer = new PeriodicTimer(TimeSpan.FromMilliseconds(Animation.FrameIntervalMs));
        CurrentFrameIndex = 0;
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

    private void ImageRenderElement_OnPaint(object? sender, SKPaintGLSurfaceEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(Animation);

        try
        {
            using var @lock = Sync.EnterScope();

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