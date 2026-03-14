using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using Cursors = System.Windows.Input.Cursors;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;

namespace ChaosAssetManager.Controls.HeaEditorControls;

// ReSharper disable once ClassCanBeSealed.Global
public partial class RadialAngleSelector
{
    private const double RADIUS = 16;
    private const double CENTER = 20;
    private const double HANDLE_SIZE = 8;

    public static readonly DependencyProperty AngleProperty = DependencyProperty.Register(
        nameof(Angle),
        typeof(double),
        typeof(RadialAngleSelector),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnAngleChanged));

    private readonly Ellipse CenterDot;
    private readonly Ellipse HandleDot;

    private readonly Ellipse OuterCircle;
    private readonly Line RadiusLine;
    private bool IsDragging;

    public double Angle
    {
        get => (double)GetValue(AngleProperty);
        set => SetValue(AngleProperty, value);
    }

    public RadialAngleSelector()
    {
        InitializeComponent();

        OuterCircle = new Ellipse
        {
            Width = RADIUS * 2,
            Height = RADIUS * 2,
            Stroke = Brushes.Gray,
            StrokeThickness = 1.5,
            Fill = Brushes.Transparent
        };

        Canvas.SetLeft(OuterCircle, CENTER - RADIUS);
        Canvas.SetTop(OuterCircle, CENTER - RADIUS);
        AngleCanvas.Children.Add(OuterCircle);

        CenterDot = new Ellipse
        {
            Width = 4,
            Height = 4,
            Fill = Brushes.White
        };

        Canvas.SetLeft(CenterDot, CENTER - 2);
        Canvas.SetTop(CenterDot, CENTER - 2);
        AngleCanvas.Children.Add(CenterDot);

        RadiusLine = new Line
        {
            Stroke = new SolidColorBrush(
                Color.FromArgb(
                    200,
                    255,
                    255,
                    100)),
            StrokeThickness = 1.5
        };

        AngleCanvas.Children.Add(RadiusLine);

        HandleDot = new Ellipse
        {
            Width = HANDLE_SIZE,
            Height = HANDLE_SIZE,
            Fill = new SolidColorBrush(
                Color.FromArgb(
                    220,
                    255,
                    255,
                    100)),
            Stroke = Brushes.White,
            StrokeThickness = 1,
            Cursor = Cursors.Hand
        };

        AngleCanvas.Children.Add(HandleDot);

        UpdateHandlePosition();
    }

    private void Canvas_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        IsDragging = true;
        AngleCanvas.CaptureMouse();
        UpdateAngleFromMouse(e);
    }

    private void Canvas_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        IsDragging = false;
        AngleCanvas.ReleaseMouseCapture();
    }

    private void Canvas_OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!IsDragging)
            return;

        UpdateAngleFromMouse(e);
    }

    private static void OnAngleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is RadialAngleSelector selector)
            selector.UpdateHandlePosition();
    }

    private void UpdateAngleFromMouse(MouseEventArgs e)
    {
        var pos = e.GetPosition(AngleCanvas);
        var dx = pos.X - CENTER;
        var dy = pos.Y - CENTER;
        var angle = Math.Atan2(dy, dx) * 180.0 / Math.PI;

        if (angle < 0)
            angle += 360;

        Angle = angle % 360;
    }

    private void UpdateHandlePosition()
    {
        var rad = Angle * Math.PI / 180.0;
        var hx = CENTER + Math.Cos(rad) * RADIUS;
        var hy = CENTER + Math.Sin(rad) * RADIUS;

        Canvas.SetLeft(HandleDot, hx - HANDLE_SIZE / 2);
        Canvas.SetTop(HandleDot, hy - HANDLE_SIZE / 2);

        RadiusLine.X1 = CENTER;
        RadiusLine.Y1 = CENTER;
        RadiusLine.X2 = hx;
        RadiusLine.Y2 = hy;
    }
}