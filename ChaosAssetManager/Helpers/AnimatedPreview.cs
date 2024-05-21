using Chaos.Common.Synchronization;
using DALib.Utility;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using SkiaSharp.Views.WPF;

namespace ChaosAssetManager.Helpers;

public class AnimatedPreview : IDisposable
{
    protected int CurrentFrameIndex { get; set; }
    protected bool Disposed { get; set; }
    protected Task? AnimationTask { get; }
    protected PeriodicTimer AnimationTimer { get; }
    public SKElement Element { get; }
    protected SKImageCollection Frames { get; }
    protected AutoReleasingMonitor Sync { get; }

    public AnimatedPreview(SKImageCollection frames, int frameIntervalMs = 100)
    {
        Frames = frames;
        AnimationTimer = new PeriodicTimer(TimeSpan.FromMilliseconds(frameIntervalMs));
        Element = new SKElement();
        Element.PaintSurface += ElementOnPaintSurface;
        Sync = new AutoReleasingMonitor();

        if (Frames.Count > 1)
            AnimationTask = AnimateElement();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        using var @lock = Sync.Enter();

        Disposed = true;
        Frames.Dispose();
    }

    protected async Task AnimateElement()
    {
        while (!Disposed)
            try
            {
                await AnimationTimer.WaitForNextTickAsync();

                using var @lock = Sync.Enter();
                CurrentFrameIndex = (CurrentFrameIndex + 1) % Frames.Count;
                Element.InvalidateVisual();
            } catch
            {
                //ignored
            }
    }

    protected virtual void ElementOnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        using var @lock = Sync.Enter();

        if (Disposed)
            return;

        var canvas = e.Surface.Canvas;
        var imageWidth = Frames.Max(frame => frame.Width);
        var imageHeight = Frames.Max(frame => frame.Height);
        var frame = Frames[CurrentFrameIndex];

        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (frame is null)
            return;

        canvas.Clear(SKColors.Black);

        //center the image in the canvas
        var x = (e.Info.Width - imageWidth) / 2;
        var y = (e.Info.Height - imageHeight) / 2;

        canvas.DrawImage(frame, x, y);
    }
}