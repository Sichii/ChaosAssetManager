using System.ComponentModel;
using System.Windows;
using ChaosAssetManager.Helpers;
using ChaosAssetManager.ViewModel;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using SkiaSharp.Views.WPF;
using DALIB_CONSTANTS = DALib.Definitions.CONSTANTS;

namespace ChaosAssetManager.Controls.MapEditorControls;

// ReSharper disable once ClassCanBeSealed.Global
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

        //all this crap is to calculate the size of the image
        //we basically have to fake render it to get it's size (nothing is actually drawn or rendered)
        //but we have to do this for all frames of the animations and take the biggest size
        //so that we can have a consistent scaling to fit the whole animation in

        //get the max frame count of all animations
        var maxBgFrames = ViewModel.RawBackgroundTiles
                                   .Select(tile => tile.Animation?.Frames.Count)
                                   .Max();

        var maxLfgFrames = ViewModel.RawLeftForegroundTiles
                                    .Select(tile => tile.Animation?.Frames.Count)
                                    .Max();

        var maxRfgFrames = ViewModel.RawRightForegroundTiles
                                    .Select(tile => tile.Animation?.Frames.Count)
                                    .Max();

        var frameCount = Math.Max(maxBgFrames ?? 0, Math.Max(maxLfgFrames ?? 0, maxRfgFrames ?? 0));
        var width = 0;
        var height = 0;

        //save the current frame indexes so we can restore them after to calculate the size
        var currentBgFrameIndexes = ViewModel.RawBackgroundTiles
                                             .Select(tile => tile.CurrentFrameIndex)
                                             .ToArray();

        var currentLfgFrameIndexes = ViewModel.RawLeftForegroundTiles
                                              .Select(tile => tile.CurrentFrameIndex)
                                              .ToArray();

        var currentRfgFrameIndexes = ViewModel.RawRightForegroundTiles
                                              .Select(tile => tile.CurrentFrameIndex)
                                              .ToArray();

        //iterate FrameCount times
        for (var i = 0; i < frameCount; i++)
        {
            //set all current frame indexes to i % animation.framecount
            foreach (var frame in ViewModel.RawBackgroundTiles)
                frame.CurrentFrameIndex = i % frame.Animation?.Frames.Count ?? 0;

            foreach (var frame in ViewModel.RawLeftForegroundTiles)
                frame.CurrentFrameIndex = i % frame.Animation?.Frames.Count ?? 0;

            foreach (var frame in ViewModel.RawRightForegroundTiles)
                frame.CurrentFrameIndex = i % frame.Animation?.Frames.Count ?? 0;

            //calculate the size of the image
            var backgroundTilesView = ViewModel.BackgroundTilesView;
            var leftForegroundTilesView = ViewModel.LeftForegroundTilesView;
            var rightForegroundTilesView = ViewModel.RightForegroundTilesView;

            var bounds = ImageHelper.CalculateRenderedImageSize(
                backgroundTilesView,
                leftForegroundTilesView,
                rightForegroundTilesView,
                DALIB_CONSTANTS.HALF_TILE_WIDTH,
                DALIB_CONSTANTS.HALF_TILE_HEIGHT);

            //take the biggest size
            width = Math.Max(width, bounds.Width);
            height = Math.Max(height, bounds.Height);
        }

        //restore current frame indexes
        for (var i = 0; i < ViewModel.RawBackgroundTiles.Count; i++)
            ViewModel.RawBackgroundTiles[i].CurrentFrameIndex = currentBgFrameIndexes[i];

        for (var i = 0; i < ViewModel.RawLeftForegroundTiles.Count; i++)
            ViewModel.RawLeftForegroundTiles[i].CurrentFrameIndex = currentLfgFrameIndexes[i];

        for (var i = 0; i < ViewModel.RawRightForegroundTiles.Count; i++)
            ViewModel.RawRightForegroundTiles[i].CurrentFrameIndex = currentRfgFrameIndexes[i];

        ImageWidth = width;
        ImageHeight = height;

        Element.InvalidateVisual();
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
}