using System.ComponentModel;
using System.Windows;
using ChaosAssetManager.Helpers;
using ChaosAssetManager.ViewModel;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using SkiaSharp.Views.WPF;
using DALIB_CONSTANTS = DALib.Definitions.CONSTANTS;

namespace ChaosAssetManager.Controls.MapEditorControls;

public partial class StructurePickerEntry
{
    private readonly SKElement Element;
    public int ImageHeight { get; private set; }
    public int ImageWidth { get; private set; }
    private StructureViewModel? ViewModel => DataContext as StructureViewModel;

    public StructurePickerEntry()
    {
        InitializeComponent();

        Element = new SKElement
        {
            Margin = new Thickness(0),
            Width = 220,
            Height = 126
        };

        Element.PaintSurface += ElementOnPaintSurface;

        ContentControl.Content = Element;
    }

    private void ElementOnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        if (ViewModel is null)
            return;

        var surface = e.Surface;
        var canvas = surface.Canvas;
        var dpi = (float)DpiHelper.GetDpiScaleFactor();

        canvas.Clear(SKColors.Transparent);

        //scale the image to fit the canvas
        //and draw it in the center
        var canvasWidth = 220 * dpi;
        var canvasHeight = 126 * dpi;

        var scaleX = canvasWidth / ImageWidth;
        var scaleY = canvasHeight / ImageHeight;
        var scale = Math.Min(scaleX, scaleY) / 1.33f;

        var offsetX = (canvasWidth - ImageWidth * scale) / 2f;
        var offsetY = (canvasHeight - ImageHeight * scale) / 2f;

        //canvas.Save();
        canvas.Translate(offsetX, offsetY);
        canvas.Scale(scale);

        if (ViewModel.HasBackgroundTiles)
            RenderBackground(canvas);

        if (ViewModel.HasForegroundTiles)
            RenderForeground(canvas);

        //canvas.Restore();
        canvas.Flush();
    }

    private void RenderBackground(SKCanvas canvas)
    {
        /*var width = (ViewModel!.Bounds.Width + ViewModel.Bounds.Height + 1) * DALIB_CONSTANTS.HALF_TILE_WIDTH;
        var height = (ViewModel.Bounds.Width + ViewModel.Bounds.Height + 1) * DALIB_CONSTANTS.HALF_TILE_HEIGHT + FOREGROUND_PADDING;*/
        var bgTiles = ViewModel!.BackgroundTilesView;

        /*using var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);*/

        var bgInitialDrawX = (ViewModel.Bounds.Height - 1) * DALIB_CONSTANTS.HALF_TILE_WIDTH;
        var bgInitialDrawY = ImageHeight - (ViewModel.Bounds.Width + ViewModel.Bounds.Height) * DALIB_CONSTANTS.HALF_TILE_HEIGHT;
        var bounds = ViewModel.Bounds;

        for (var y = 0; y < bounds.Height; y++)
        {
            for (var x = 0; x < bounds.Width; x++)
            {
                var tileViewModel = bgTiles[x, y];
                var currentFrame = tileViewModel.CurrentFrame;

                canvas.DrawImage(
                    currentFrame,
                    bgInitialDrawX + x * DALIB_CONSTANTS.HALF_TILE_WIDTH,
                    bgInitialDrawY + x * DALIB_CONSTANTS.HALF_TILE_HEIGHT);
            }

            bgInitialDrawX -= DALIB_CONSTANTS.HALF_TILE_WIDTH;
            bgInitialDrawY += DALIB_CONSTANTS.HALF_TILE_HEIGHT;
        }
    }

    private void RenderForeground(SKCanvas canvas)
    {
        /*var width = (ViewModel!.Bounds.Width + ViewModel.Bounds.Height + 1) * DALIB_CONSTANTS.HALF_TILE_WIDTH;
        var height = (ViewModel.Bounds.Width + ViewModel.Bounds.Height + 1) * DALIB_CONSTANTS.HALF_TILE_HEIGHT + FOREGROUND_PADDING;*/
        var lfgTiles = ViewModel!.LeftForegroundTilesView;
        var rfgTiles = ViewModel.RightForegroundTilesView;

        var fgInitialDrawX = (ViewModel.Bounds.Height - 1) * DALIB_CONSTANTS.HALF_TILE_WIDTH;
        var fgInitialDrawY = ImageHeight - (ViewModel.Bounds.Width + ViewModel.Bounds.Height) * DALIB_CONSTANTS.HALF_TILE_HEIGHT;
        var bounds = ViewModel.Bounds;

        for (var y = 0; y < bounds.Height; y++)
        {
            for (var x = 0; x < bounds.Width; x++)
            {
                var leftTileViewModel = lfgTiles[x, y];
                var rightTileViewModel = rfgTiles[x, y];

                var leftCurrentFrame = leftTileViewModel.CurrentFrame;
                var rightCurrentFrame = rightTileViewModel.CurrentFrame;

                if (leftCurrentFrame is not null && (leftTileViewModel.TileId >= 13) && ((leftTileViewModel.TileId % 10000) > 1))
                    canvas.DrawImage(
                        leftCurrentFrame,
                        fgInitialDrawX + x * DALIB_CONSTANTS.HALF_TILE_WIDTH,
                        fgInitialDrawY
                        + (x + 1) * DALIB_CONSTANTS.HALF_TILE_HEIGHT
                        - leftCurrentFrame.Height
                        + DALIB_CONSTANTS.HALF_TILE_HEIGHT);

                if (rightCurrentFrame is not null && (rightTileViewModel.TileId >= 13) && ((rightTileViewModel.TileId % 10000) > 1))
                    canvas.DrawImage(
                        rightCurrentFrame,
                        fgInitialDrawX + (x + 1) * DALIB_CONSTANTS.HALF_TILE_WIDTH,
                        fgInitialDrawY
                        + (x + 1) * DALIB_CONSTANTS.HALF_TILE_HEIGHT
                        - rightCurrentFrame.Height
                        + DALIB_CONSTANTS.HALF_TILE_HEIGHT);
            }

            fgInitialDrawX -= DALIB_CONSTANTS.HALF_TILE_WIDTH;
            fgInitialDrawY += DALIB_CONSTANTS.HALF_TILE_HEIGHT;
        }
    }

    private void StructureViewModel_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName))
            return;

        if (e.PropertyName is nameof(StructureViewModel.RawBackgroundTiles)
                              or nameof(StructureViewModel.RawLeftForegroundTiles)
                              or nameof(StructureViewModel.RawRightForegroundTiles))
            Element.InvalidateVisual();
    }

    private void StructurePickerEntryControl_OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (ViewModel is null)
        {
            Element.InvalidateVisual();

            return;
        }

        if (e.OldValue is TileGrabViewModel oldTileViewModel)
            oldTileViewModel.PropertyChanged -= StructureViewModel_OnPropertyChanged;

        ViewModel.PropertyChanged += StructureViewModel_OnPropertyChanged;
        ViewModel.Initialize();

        var backgroundTilesView = ViewModel.BackgroundTilesView;
        var leftForegroundTilesView = ViewModel.LeftForegroundTilesView;
        var rightForegroundTilesView = ViewModel.RightForegroundTilesView;

        var bounds = ImageHelper.CalculateRenderedImageSize(
            backgroundTilesView,
            leftForegroundTilesView,
            rightForegroundTilesView,
            DALIB_CONSTANTS.HALF_TILE_WIDTH,
            DALIB_CONSTANTS.HALF_TILE_HEIGHT);

        ImageWidth = bounds.Width;
        ImageHeight = bounds.Height;

        Element.InvalidateVisual();
    }
}