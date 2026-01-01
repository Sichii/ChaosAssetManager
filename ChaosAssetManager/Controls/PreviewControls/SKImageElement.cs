using System.Windows;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using SkiaSharp.Views.WPF;

namespace ChaosAssetManager.Controls.PreviewControls;

public sealed class SKImageElement : SKElement
{
    public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
        nameof(Source),
        typeof(SKImage),
        typeof(SKImageElement),
        new PropertyMetadata(null, OnPropertyChanged));

    public static readonly DependencyProperty ScaleProperty = DependencyProperty.Register(
        nameof(Scale),
        typeof(double),
        typeof(SKImageElement),
        new PropertyMetadata(1.0, OnPropertyChanged));

    public SKImage? Source
    {
        get => (SKImage?)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public double Scale
    {
        get => (double)GetValue(ScaleProperty);
        set => SetValue(ScaleProperty, value);
    }

    private static void OnPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SKImageElement element)
            element.InvalidateVisual();
    }

    protected override void OnPaintSurface(SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        if (Source == null)
            return;

        var scale = (float)Scale;
        canvas.Scale(scale, scale);
        canvas.DrawImage(Source, 0, 0);
    }
}
