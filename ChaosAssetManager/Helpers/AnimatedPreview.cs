using System.Windows.Input;
using Chaos.Common.Synchronization;
using DALib.Utility;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using SkiaSharp.Views.WPF;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;

namespace ChaosAssetManager.Helpers;

public class AnimatedPreview : IDisposable
{
    private const float SCALE_FACTOR = 1.1f;
    private readonly Task? AnimationTask;
    private readonly PeriodicTimer AnimationTimer;
    public readonly SKElement Element;
    private readonly SKImageCollection Frames;
    private readonly AutoReleasingMonitor Sync;
    private int CurrentFrameIndex;
    private bool Disposed;
    private bool IsPanning;
    private SKPoint LastPanPoint;
    private SKPoint PanOffset = new(0, 0);
    private float Scale;

    public AnimatedPreview(SKImageCollection frames, int frameIntervalMs = 100)
    {
        Frames = frames;
        AnimationTimer = new PeriodicTimer(TimeSpan.FromMilliseconds(frameIntervalMs));
        Element = new SKElement();
        Element.PaintSurface += ElementOnPaintSurface;
        Element.MouseWheel += SkElement_MouseWheel;
        Element.MouseDown += SkElement_MouseDown;
        Element.MouseMove += SkElement_MouseMove;
        Element.MouseUp += SkElement_MouseUp;
        Sync = new AutoReleasingMonitor();
        Scale = 1.0f;

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
        //sometimes frames can be null
        if (frame is null)
            return;

        //calculate the top/left coordinates of the image so that it is centered
        var x = (e.Info.Width - imageWidth) / 2;
        var y = (e.Info.Height - imageHeight) / 2;

        //calculate center point of the canvas
        var centerX = e.Info.Width / 2f;
        var centerY = e.Info.Height / 2f;

        //keep the image centered regardless of scaling
        canvas.Translate(centerX, centerY);
        canvas.Scale(Scale);
        canvas.Translate(-centerX, -centerY);

        //apply panning offset
        canvas.Translate(PanOffset.X, PanOffset.Y);

        //clear the canvas and draw the image
        canvas.Clear(SKColors.Black);
        canvas.DrawImage(frame, x, y);
    }

    #region Preview Controls
    private void SkElement_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        //get position of mouse relative to center of preview element
        var position = e.GetPosition(Element);
        var centerX = (float)(position.X - Element.ActualWidth / 2);
        var centerY = (float)(position.Y - Element.ActualHeight / 2);

        if (e.Delta > 0)
        {
            Scale *= SCALE_FACTOR;
            PanOffset.X = (PanOffset.X - centerX) * SCALE_FACTOR + centerX;
            PanOffset.Y = (PanOffset.Y - centerY) * SCALE_FACTOR + centerY;
        } else
        {
            Scale /= SCALE_FACTOR;
            PanOffset.X = (PanOffset.X - centerX) / SCALE_FACTOR + centerX;
            PanOffset.Y = (PanOffset.Y - centerY) / SCALE_FACTOR + centerY;
        }

        // Invalidate the surface to trigger a redraw
        Element.InvalidateVisual();
    }

    private void SkElement_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            IsPanning = true;
            var position = e.GetPosition(Element);
            LastPanPoint = new SKPoint((float)position.X, (float)position.Y);
            Element.CaptureMouse();
        }
    }

    private void SkElement_MouseMove(object sender, MouseEventArgs e)
    {
        if (IsPanning && (e.LeftButton == MouseButtonState.Pressed))
        {
            var position = e.GetPosition(Element);
            var currentPoint = new SKPoint((float)position.X, (float)position.Y);

            // Calculate the difference from the last point
            var deltaX = currentPoint.X - LastPanPoint.X;
            var deltaY = currentPoint.Y - LastPanPoint.Y;
            var dpiScaling = (float)DpiHelper.GetDpiScaleFactor();

            //scale the delta by dpi scaling factor
            deltaX *= dpiScaling;
            deltaY *= dpiScaling;

            //panning is slower/faster depending on zoom level
            deltaX /= Scale;
            deltaY /= Scale;

            // Update the pan offset
            PanOffset.X += deltaX;
            PanOffset.Y += deltaY;

            // Update the last point
            LastPanPoint = currentPoint;

            // Invalidate the surface to trigger a redraw
            Element.InvalidateVisual();
        }
    }

    private void SkElement_MouseUp(object sender, MouseButtonEventArgs e)
    {
        IsPanning = false;
        Element.ReleaseMouseCapture();
    }
    #endregion
}