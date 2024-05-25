using System.Windows;
using System.Windows.Input;
using Chaos.Common.Synchronization;
using ChaosAssetManager.Helpers;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;

namespace ChaosAssetManager.Controls.PreviewControls;

public partial class SKElementPlus
{
    private readonly AutoReleasingMonitor Sync;
    private bool IsPanning;
    private SKPoint LastPanPoint;
    public SKMatrix Matrix { get; set; }

    public SKElementPlus()
    {
        Matrix = SKMatrix.CreateIdentity();
        InitializeComponent();

        Sync = new AutoReleasingMonitor();
    }

    private void Element_OnLoaded(object sender, RoutedEventArgs e) => ElementLoaded?.Invoke(sender, e);

    public event EventHandler<RoutedEventArgs>? ElementLoaded;

    private void ElementOnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        try
        {
            using var @lock = Sync.Enter();

            var canvas = e.Surface.Canvas;
            canvas.Clear(SKColors.Black);
            canvas.SetMatrix(Matrix);

            Paint?.Invoke(sender, e);
        } catch
        {
            //ignored
        }
    }

    public event EventHandler<SKPaintSurfaceEventArgs>? Paint;

    public void Redraw() => Element?.InvalidateVisual();

    #region Preview Controls
    private void SkElement_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(Element);
        ArgumentNullException.ThrowIfNull(Matrix);

        try
        {
            using var @lock = Sync.Enter();

            var dpiScale = (float)DpiHelper.GetDpiScaleFactor();
            var position = e.GetPosition(Element);
            var mousePoint = new SKPoint((float)position.X * dpiScale, (float)position.Y * dpiScale);
            var scale = e.Delta > 0 ? 1.1f : 1 / 1.1f;

            if (!Matrix.TryInvert(out var inverseMatrix))
                return;

            var transformedPoint = inverseMatrix.MapPoint(mousePoint);

            // Apply scaling transformation around the transformed point
            var scaling = SKMatrix.CreateScale(
                scale,
                scale,
                transformedPoint.X,
                transformedPoint.Y);

            Matrix = Matrix.PreConcat(scaling);

            Element.InvalidateVisual();
        } catch
        {
            //ignored
        }
    }

    private void SkElement_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
            return;

        ArgumentNullException.ThrowIfNull(Element);

        try
        {
            using var @lock = Sync.Enter();

            IsPanning = true;
            var position = e.GetPosition(Element);
            LastPanPoint = new SKPoint((float)position.X, (float)position.Y);
            Element.CaptureMouse();
        } catch
        {
            //ignored
        }
    }

    private void SkElement_MouseMove(object sender, MouseEventArgs e)
    {
        if (!IsPanning || (e.LeftButton != MouseButtonState.Pressed))
            return;

        ArgumentNullException.ThrowIfNull(Element);
        ArgumentNullException.ThrowIfNull(Matrix);

        try
        {
            using var @lock = Sync.Enter();

            var position = e.GetPosition(Element);
            var dpiScale = DpiHelper.GetDpiScaleFactor();
            var deltaX = (position.X - LastPanPoint.X) * dpiScale;
            var deltaY = (position.Y - LastPanPoint.Y) * dpiScale;

            Matrix = SKMatrix.CreateTranslation((float)deltaX, (float)deltaY)
                             .PreConcat(Matrix);
            LastPanPoint = new SKPoint((float)position.X, (float)position.Y);

            Element.InvalidateVisual();
        } catch
        {
            //ignored
        }
    }

    private void SkElement_MouseUp(object sender, MouseButtonEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(Element);

        try
        {
            using var @lock = Sync.Enter();

            IsPanning = false;
            Element.ReleaseMouseCapture();
        } catch
        {
            //ignored
        }
    }
    #endregion
}