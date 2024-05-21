using Chaos.Common.Synchronization;
using DALib.Utility;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using SkiaSharp.Views.WPF;

namespace ChaosAssetManager.Helpers;

public class AnimatedPreview : IDisposable
{
    private readonly PeriodicTimer AnimationTimer;
    public readonly SKElement Element;
    private readonly SKImageCollection Frames;
    private readonly AutoReleasingMonitor Sync;
    private Task AnimationTask;
    private int CurrentFrameIndex;
    private bool Disposed;

    public AnimatedPreview(SKImageCollection frames, int frameIntervalMs)
    {
        Frames = frames;
        AnimationTimer = new PeriodicTimer(TimeSpan.FromMilliseconds(frameIntervalMs));
        Element = new SKElement();
        Element.PaintSurface += ElementOnPaintSurface;
        AnimationTask = AnimateElement();
        Sync = new AutoReleasingMonitor();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        using var @lock = Sync.Enter();

        Disposed = true;
        Frames.Dispose();
    }

    private async Task AnimateElement()
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

    private void ElementOnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        using var @lock = Sync.Enter();

        if (Disposed)
            return;

        var canvas = e.Surface.Canvas;
        var frame = Frames[CurrentFrameIndex];
        canvas.Clear(SKColors.Black);

        //center the image in the canvas
        var x = (e.Info.Width - frame.Width) / 2;
        var y = (e.Info.Height - frame.Height) / 2;

        canvas.DrawImage(frame, x, y);
    }
}