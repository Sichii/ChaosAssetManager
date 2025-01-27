using System.Windows.Input;
using ChaosAssetManager.Helpers;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using SkiaSharp.Views.WPF;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;

namespace ChaosAssetManager.Controls.PreviewControls;

public sealed partial class SKGLElementPlus : IDisposable
{
    private readonly SKGLElement Element;
    private readonly Lock Sync;
    private bool IsPanning;
    private SKPoint LastPanPoint;
    public MouseButton DragButton { get; set; } = MouseButton.Left;
    public SKMatrix Matrix { get; set; }

    public SKGLElementPlus()
    {
        Matrix = SKMatrix.CreateIdentity();
        InitializeComponent();

        Sync = new Lock();

        Element = SkGlElementPool.Instance.Get();
        Element.MouseWheel += SkElement_MouseWheel;
        Element.MouseDown += SkElement_MouseDown;
        Element.MouseMove += SkElement_MouseMove;
        Element.MouseUp += SkElement_MouseUp;
        Element.PaintSurface += ElementOnPaintSurface;
        Element.InvalidateVisual();

        ElementContent.Content = Element;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Paint = null;

        ElementContent.Content = null;

        Element.MouseWheel -= SkElement_MouseWheel;
        Element.MouseDown -= SkElement_MouseDown;
        Element.MouseMove -= SkElement_MouseMove;
        Element.MouseUp -= SkElement_MouseUp;
        Element.PaintSurface -= ElementOnPaintSurface;

        SkGlElementPool.Instance.Return(Element);
    }

    private void ElementOnPaintSurface(object? sender, SKPaintGLSurfaceEventArgs e)
    {
        try
        {
            using var @lock = Sync.EnterScope();

            var canvas = e.Surface.Canvas;
            canvas.Clear(SKColors.Black);
            canvas.SetMatrix(Matrix);

            Paint?.Invoke(sender, e);

            canvas.Flush();
        } catch
        {
            //ignored
        }
    }

    public event EventHandler<SKPaintGLSurfaceEventArgs>? Paint;

    public void Redraw() => Element.InvalidateVisual();

    #region Preview Controls
    private void SkElement_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(Element);

        try
        {
            using var @lock = Sync.EnterScope();

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

    public SKPoint? GetMousePoint()
    {
        var dpiScale = (float)DpiHelper.GetDpiScaleFactor();
        var position = Mouse.GetPosition(Element);
        var mousePoint = new SKPoint((float)position.X * dpiScale, (float)position.Y * dpiScale);

        if (!Matrix.TryInvert(out var inverseMatrix))
            return null;

        return inverseMatrix.MapPoint(mousePoint);
    }

    private void SkElement_MouseDown(object sender, MouseButtonEventArgs e)
    {
        // ReSharper disable once SwitchStatementMissingSomeEnumCasesNoDefault
        switch (DragButton)
        {
            case MouseButton.Left:
                if (e.LeftButton != MouseButtonState.Pressed)
                    return;

                break;
            case MouseButton.Right:
                if (e.RightButton != MouseButtonState.Pressed)
                    return;

                break;
            case MouseButton.Middle:
                if (e.MiddleButton != MouseButtonState.Pressed)
                    return;

                break;
        }

        ArgumentNullException.ThrowIfNull(Element);

        try
        {
            using var @lock = Sync.EnterScope();

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
        if (!IsPanning)
            return;

        // ReSharper disable once SwitchStatementMissingSomeEnumCasesNoDefault
        switch (DragButton)
        {
            case MouseButton.Left:
                if (e.LeftButton != MouseButtonState.Pressed)
                    return;

                break;
            case MouseButton.Right:
                if (e.RightButton != MouseButtonState.Pressed)
                    return;

                break;
            case MouseButton.Middle:
                if (e.MiddleButton != MouseButtonState.Pressed)
                    return;

                break;
        }

        ArgumentNullException.ThrowIfNull(Element);

        try
        {
            using var @lock = Sync.EnterScope();

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
            using var @lock = Sync.EnterScope();

            IsPanning = false;
            Element.ReleaseMouseCapture();
        } catch
        {
            //ignored
        }
    }
    #endregion
}