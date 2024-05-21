using System.IO;
using System.Windows;
using System.Windows.Input;
using Chaos.Common.Synchronization;
using ChaosAssetManager.Helpers;
using ChaosAssetManager.Model;
using DALib.Data;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using SkiaSharp.Views.WPF;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;

namespace ChaosAssetManager.Controls;

public sealed partial class EntryPreviewControl : IDisposable
{
    private readonly DataArchive Archive;
    private readonly string ArchiveName;
    private readonly string ArchiveRoot;
    private readonly DataArchiveEntry Entry;
    private readonly AutoReleasingMonitor Sync;
    private Animation? Animation;
    private Task? AnimationTask;
    private PeriodicTimer? AnimationTimer;
    private int CurrentFrameIndex;
    private bool Disposed;
    private SKElement? Element;
    private bool IsPanning;
    private SKPoint LastPanPoint;
    private SKMatrix? Matrix;

    public EntryPreviewControl(
        DataArchive archive,
        DataArchiveEntry entry,
        string archiveName,
        string archiveRoot)
    {
        Archive = archive;
        Entry = entry;
        ArchiveName = archiveName;
        ArchiveRoot = archiveRoot;
        Sync = new AutoReleasingMonitor();

        InitializeComponent();
        Initialize();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        using var @lock = Sync.Enter();

        if (Disposed)
            return;

        Disposed = true;

        Animation?.Dispose();
        AnimationTimer?.Dispose();
    }

    private void Initialize()
    {
        var type = Path.GetExtension(Entry.EntryName)
                       .ToLower();

        switch (type)
        {
            case ".tbl":
            case ".txt":
            case ".log":
            {
                Content = RenderUtil.RenderText(Entry);

                break;
            }
            case ".efa":
            {
                var animation = RenderUtil.RenderEfa(Entry);

                if (animation is null)
                    break;

                Animation = animation;

                break;
            }

            case ".spf":
            {
                var animation = RenderUtil.RenderSpf(Entry);

                if (animation is null)
                    break;

                Animation = animation;

                break;
            }
            case ".bmp":
            {
                var animation = RenderUtil.RenderBmp(Entry);

                if (animation is null)
                    break;

                Animation = animation;

                break;
            }
            case ".mpf":
            {
                var animation = RenderUtil.RenderMpf(Archive, Entry);

                if (animation is null)
                    break;

                Animation = animation;

                break;
            }
            case ".epf":
            {
                var animation = RenderUtil.RenderEpf(
                    Archive,
                    Entry,
                    ArchiveName,
                    ArchiveRoot);

                if (animation is null)
                    break;

                Animation = animation;

                break;
            }

            case ".hpf":
            {
                var animation = RenderUtil.RenderHpf(Archive, Entry);

                if (animation is null)
                    break;

                Animation = animation;

                break;
            }
        }

        if (Animation is not null)
            GenerateElement();
    }

    #region Animation
    private async Task AnimateElement()
    {
        ArgumentNullException.ThrowIfNull(Animation);
        ArgumentNullException.ThrowIfNull(AnimationTimer);
        ArgumentNullException.ThrowIfNull(Element);

        while (true)
            try
            {
                await AnimationTimer.WaitForNextTickAsync();

                using var @lock = Sync.Enter();

                if (Disposed)
                    return;

                CurrentFrameIndex = (CurrentFrameIndex + 1) % Animation.Frames.Count;
                Element.InvalidateVisual();
            } catch
            {
                //ignored
            }
    }

    private void GenerateElement()
    {
        ArgumentNullException.ThrowIfNull(Animation);

        Matrix = SKMatrix.CreateIdentity();
        AnimationTimer = new PeriodicTimer(TimeSpan.FromMilliseconds(Animation.FrameIntervalMs));
        Element = new SKElement();
        Element.PaintSurface += ElementOnPaintSurface;
        Element.MouseWheel += SkElement_MouseWheel;
        Element.MouseDown += SkElement_MouseDown;
        Element.MouseMove += SkElement_MouseMove;
        Element.MouseUp += SkElement_MouseUp;
        Element.Loaded += ElementOnLoaded;

        if (Animation.Frames.Count > 1)
            AnimationTask = AnimateElement();

        Content = Element;
    }

    private void ElementOnLoaded(object sender, RoutedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(Element);
        ArgumentNullException.ThrowIfNull(Animation);
        ArgumentNullException.ThrowIfNull(Matrix);

        var elementWidth = Element.ActualWidth;
        var elementHeight = Element.ActualHeight;

        var maxWidth = Animation.Frames.Max(frame => frame.Width);
        var maxHeight = Animation.Frames.Max(frame => frame.Height);

        var dpiScale = (float)DpiHelper.GetDpiScaleFactor();

        // Correctly center the image taking into account the DPI scaling
        var translateX = (float)(elementWidth * dpiScale - maxWidth) / 2f;
        var translateY = (float)(elementHeight * dpiScale - maxHeight) / 2f;

        Matrix = SKMatrix.CreateTranslation(translateX, translateY);

        Element.InvalidateVisual();
    }

    private void ElementOnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(Animation);
        ArgumentNullException.ThrowIfNull(Matrix);

        using var @lock = Sync.Enter();

        if (Disposed)
            return;

        var canvas = e.Surface.Canvas;
        var frame = Animation.Frames[CurrentFrameIndex];

        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        //sometimes frames can be null
        if (frame is null)
            return;

        //clear the canvas and draw the image
        canvas.Clear(SKColors.Black);
        canvas.SetMatrix(Matrix.Value);
        canvas.DrawImage(frame, 0, 0);
    }
    #endregion

    #region Preview Controls
    private void SkElement_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(Element);
        ArgumentNullException.ThrowIfNull(Matrix);

        using var @lock = Sync.Enter();

        var dpiScale = (float)DpiHelper.GetDpiScaleFactor();
        var position = e.GetPosition(Element);
        var mousePoint = new SKPoint((float)position.X * dpiScale, (float)position.Y * dpiScale);
        var scale = e.Delta > 0 ? 1.1f : 1 / 1.1f;

        if (!Matrix.Value.TryInvert(out var inverseMatrix))
            return;

        var transformedPoint = inverseMatrix.MapPoint(mousePoint);

        // Apply scaling transformation around the transformed point
        var scaling = SKMatrix.CreateScale(
            scale,
            scale,
            transformedPoint.X,
            transformedPoint.Y);

        Matrix = Matrix.Value.PreConcat(scaling);

        Element.InvalidateVisual();
    }

    private void SkElement_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
            return;

        ArgumentNullException.ThrowIfNull(Element);

        using var @lock = Sync.Enter();

        IsPanning = true;
        var position = e.GetPosition(Element);
        LastPanPoint = new SKPoint((float)position.X, (float)position.Y);
        Element.CaptureMouse();
    }

    private void SkElement_MouseMove(object sender, MouseEventArgs e)
    {
        if (!IsPanning || (e.LeftButton != MouseButtonState.Pressed))
            return;

        ArgumentNullException.ThrowIfNull(Element);
        ArgumentNullException.ThrowIfNull(Matrix);

        using var @lock = Sync.Enter();

        var position = e.GetPosition(Element);
        var dpiScale = DpiHelper.GetDpiScaleFactor();
        var deltaX = (position.X - LastPanPoint.X) * dpiScale;
        var deltaY = (position.Y - LastPanPoint.Y) * dpiScale;

        Matrix = SKMatrix.CreateTranslation((float)deltaX, (float)deltaY)
                         .PreConcat(Matrix.Value);
        LastPanPoint = new SKPoint((float)position.X, (float)position.Y);

        Element.InvalidateVisual();
    }

    private void SkElement_MouseUp(object sender, MouseButtonEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(Element);

        using var @lock = Sync.Enter();

        IsPanning = false;
        Element.ReleaseMouseCapture();
    }
    #endregion
}