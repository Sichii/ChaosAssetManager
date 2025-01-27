using System.ComponentModel;
using System.Windows;
using ChaosAssetManager.Helpers;
using ChaosAssetManager.ViewModel;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using SkiaSharp.Views.WPF;

namespace ChaosAssetManager.Controls.MapEditorControls;

// ReSharper disable once ClassCanBeSealed.Global
public partial class TilePickerEntryControl
{
    private readonly Lock Sync = new();
    public SKElement Element { get; }

    public TileViewModel? TileViewModel => DataContext as TileViewModel;

    public TilePickerEntryControl()
    {
        InitializeComponent();

        Element = new SKElement
        {
            Margin = new Thickness(0)
        };

        Element.PaintSurface += ElementOnPaintSurface;

        ContentControl.Content = Element;
    }

    private void ElementOnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        using var @lock = Sync.EnterScope();

        var surface = e.Surface;
        var canvas = surface.Canvas;

        if (TileViewModel?.Animation is null)
        {
            canvas.Clear(SKColors.Transparent);

            return;
        }

        var frame = TileViewModel.CurrentFrame;

        if (frame is null || (frame.Handle == nint.Zero))
            return;

        var dpiScale = (float)DpiHelper.GetDpiScaleFactor();

        //animated foregrounds might have different heights
        var maxHeight = TileViewModel.Animation.Frames.Max(x => x.Height);

        Element.Width = 112;
        Element.Height = maxHeight;

        //draw the frame at the bottom of the control
        var drawY = maxHeight - frame.Height;

        canvas.Clear(SKColors.Transparent);
        canvas.Scale(dpiScale);
        canvas.DrawImage(frame, 0, drawY);

        canvas.Flush();
    }

    private void TilePickerEntryControl_OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        using var @lock = Sync.EnterScope();

        if (TileViewModel is null)
        {
            Element.InvalidateVisual();

            return;
        }

        if (e.OldValue is TileViewModel oldTileViewModel)
            oldTileViewModel.PropertyChanged -= TileViewModel_OnPropertyChanged;

        TileViewModel.PropertyChanged += TileViewModel_OnPropertyChanged;
        TileViewModel.Initialize();
        Element.InvalidateVisual();
    }

    private void TileViewModel_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        using var @lock = Sync.EnterScope();

        if (e.PropertyName == nameof(TileViewModel.CurrentFrameIndex))
            Element.InvalidateVisual();
    }
}