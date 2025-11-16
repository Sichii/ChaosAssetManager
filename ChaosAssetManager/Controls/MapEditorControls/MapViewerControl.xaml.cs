using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Chaos.Extensions.Common;
using Chaos.Extensions.Geometry;
using Chaos.Geometry;
using ChaosAssetManager.Controls.PreviewControls;
using ChaosAssetManager.Definitions;
using ChaosAssetManager.Helpers;
using ChaosAssetManager.ViewModel;
using DALib.Extensions;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using DALIB_CONSTANTS = DALib.Definitions.CONSTANTS;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Point = Chaos.Geometry.Point;

#pragma warning disable CS8618, CS9264

// ReSharper disable ClassCanBeSealed.Global

namespace ChaosAssetManager.Controls.MapEditorControls;

public partial class MapViewerControl : IDisposable
{
    public const int FOREGROUND_PADDING = 512;
    private readonly Lock RenderSync = new();
    private SKImage? BackgroundImage;

    private Task? BackgroundRenderTask;
    private SKImage? ForegroundImage;
    private Task? ForegroundRenderTask;
    private DateTime LastBackgroundRenderTime = DateTime.MinValue;
    private DateTime LastForegroundRenderTime = DateTime.MinValue;
    private DateTime LastRequestedBackgroundRenderTime = DateTime.MinValue;
    private DateTime LastRequestedForegroundRenderTime = DateTime.MinValue;
    private DateTime LastRequestedTabMapRenderTime = DateTime.MinValue;
    private DateTime LastTabMapRenderTime = DateTime.MinValue;
    private SKImage? TabMapImage;
    private Task? TabMapRenderTask;

    private TileGrabViewModel? HistoricalTileGrab { get; set; }

    private TileGrabViewModel? TileGrab
    {
        get => MapEditorViewModel.TileGrab;
        set => MapEditorViewModel.TileGrab = value;
    }

    public MapViewerViewModel ViewModel { get; set; }

    public SKGLElementPlus Element { get; }
    private MapEditorViewModel MapEditorViewModel => MapEditorControl.Instance.ViewModel;

    public MapViewerControl()
    {
        InitializeComponent();

        Element = new SKGLElementPlus();
        Element.DragButton = MouseButton.Right;
        Element.Paint += ElementOnPaint;
        Element.MouseMove += ElementOnMouseMove;
        Element.MouseLeftButtonDown += ElementOnMouseLeftButtonDown;
        Element.MouseLeftButtonUp += ElementOnMouseLeftButtonUp;

        Content.Content = Element;
        MapEditorViewModel.PropertyChanged += MapEditorViewModelOnPropertyChanged;

        if (MapEditorViewModel.TileGrab is not null)
        {
            HistoricalTileGrab = MapEditorViewModel.TileGrab;
            MapEditorViewModel.TileGrab.PropertyChanged += TileGrabOnPropertyChanged;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        BackgroundImage?.Dispose();
        ForegroundImage?.Dispose();
        TabMapImage?.Dispose();

        Element.Paint -= ElementOnPaint;
        Element.MouseMove -= ElementOnMouseMove;
        Element.MouseLeftButtonDown -= ElementOnMouseLeftButtonDown;
        Element.MouseLeftButtonUp -= ElementOnMouseLeftButtonUp;

        Element.Dispose();

        GC.SuppressFinalize(this);
    }

    private void MapViewerControl_OnLoaded(object sender, RoutedEventArgs e)
    {
        if (SetViewerTransform())
        {
            Element.Matrix = ViewModel.ViewerTransform!.Value;
            Element.Redraw();
        }
    }

    private bool SetViewerTransform()
    {
        if (ViewModel.ViewerTransform is not null)
            return true;

        if ((ViewModel == MapViewerViewModel.Empty) || (Element.ActualWidth == 0) || (Element.ActualHeight == 0))
            return false;

        var dimensions = ImageHelper.CalculateRenderedImageSize(
            ViewModel.BackgroundTilesView,
            ViewModel.LeftForegroundTilesView,
            ViewModel.RightForegroundTilesView);

        var dpiScale = (float)DpiHelper.GetDpiScaleFactor();

        var x = (float)((Element.ActualWidth * dpiScale - dimensions.Width) / 2f);
        var y = (float)((Element.ActualHeight * dpiScale - dimensions.Height) / 2f - FOREGROUND_PADDING / 1.33f);

        ViewModel.ViewerTransform = SKMatrix.CreateTranslation(x, y);

        return true;
    }

    #region Utilities
    private SKPaint GetBrightenPaint()
    {
        const float BRIGHTEN_FACTOR = 1.75f;
        
        // @formatter:off
        var colorMatrix = new[]
        {
            BRIGHTEN_FACTOR, 0, 0, 0, 0,
            0, BRIGHTEN_FACTOR, 0, 0, 0,
            0, 0, BRIGHTEN_FACTOR, 0, 0,
            0, 0, 0,        1,        0 
        };
        // @formatter:on

        var colorFilter = SKColorFilter.CreateColorMatrix(colorMatrix);

        return new SKPaint
        {
            ColorFilter = colorFilter,
            IsAntialias = true
        };
    }

    public SKPoint ConvertMouseToTileCoordinates(SKPoint mousePoint)
    {
        var mouseX = mousePoint.X;
        var mouseY = mousePoint.Y;
        var bgInitialDrawX = (ViewModel.Bounds.Height - 1) * DALIB_CONSTANTS.HALF_TILE_WIDTH;
        var bgInitialDrawY = FOREGROUND_PADDING;

        var localX = mouseX - (bgInitialDrawX + DALIB_CONSTANTS.HALF_TILE_WIDTH);
        var localY = mouseY - bgInitialDrawY;

        var offsetX = localX / DALIB_CONSTANTS.HALF_TILE_WIDTH;
        var offsetY = localY / DALIB_CONSTANTS.HALF_TILE_HEIGHT;

        var tileX = (int)Math.Floor((offsetX + offsetY) / 2f);
        var tileY = (int)Math.Floor((offsetY - offsetX) / 2f);
        var point = new Point(tileX, tileY);

        if (!ViewModel.Bounds.Contains(point))
        {
            var clampedX = Math.Clamp(tileX, ViewModel.Bounds.Left, ViewModel.Bounds.Right);
            var clampedY = Math.Clamp(tileY, ViewModel.Bounds.Top, ViewModel.Bounds.Bottom);
            
            //if the mouse is outside the map bounds, snap it to the closest edge
            point = new Point(clampedX, clampedY);
        }

        return new SKPoint(point.X, point.Y);
    }

    private void HandleDrawToolClick(SKPoint tileCoordinates)
    {
        if (TileGrab is null || TileGrab.IsEmpty || (tileCoordinates == new SKPoint(-1, -1)))
            return;

        var after = TileGrab.WithTileCoordinates(tileCoordinates);

        var before = TileGrabViewModel.CreateFrom(
            ViewModel,
            after,
            MapEditorViewModel.EditingLayerFlags,
            tileCoordinates);

        ViewModel.AddAction(
            ActionType.Draw,
            before,
            after,
            MapEditorViewModel.EditingLayerFlags,
            tileCoordinates);

        TileGrab.Apply(ViewModel, MapEditorViewModel.EditingLayerFlags);
    }

    private void HandleSelectToolClick(SKPoint tileCoordinates)
        => TileGrab = TileGrabViewModel.Create(
            ViewModel,
            tileCoordinates,
            1,
            1,
            MapEditorViewModel.EditingLayerFlags);

    private void HandleSampleToolClick(SKPoint tileCoordinates)
    {
        if (MapEditorViewModel.EditingLayerFlags is LayerFlags.All or LayerFlags.Foreground)
            return;

        switch (MapEditorViewModel.EditingLayerFlags)
        {
            case LayerFlags.Background:
            {
                var sampledTile = ViewModel.BackgroundTilesView[(int)tileCoordinates.X, (int)tileCoordinates.Y];

                var originalTile = MapEditorViewModel.BackgroundTiles
                                                     .SelectMany((row, rowIndex) => row.Select((tile, columnIndex) => new
                                                     {
                                                         tile,
                                                         rowIndex,
                                                         columnIndex
                                                     }))
                                                     .Single(x => x.tile.TileId == sampledTile.TileId);

                SelectCell(originalTile.rowIndex, originalTile.columnIndex);

                break;
            }
            case LayerFlags.LeftForeground:
            {
                var sampledTile = ViewModel.LeftForegroundTilesView[(int)tileCoordinates.X, (int)tileCoordinates.Y];

                var originalTile = MapEditorViewModel.ForegroundTiles
                                                     .SelectMany((row, rowIndex) => row.Select((tile, columnIndex) => new
                                                     {
                                                         tile,
                                                         rowIndex,
                                                         columnIndex
                                                     }))
                                                     .Single(x => x.tile.TileId == sampledTile.TileId);

                SelectCell(originalTile.rowIndex, originalTile.columnIndex);

                break;
            }
            case LayerFlags.RightForeground:
            {
                var sampledTile = ViewModel.RightForegroundTilesView[(int)tileCoordinates.X, (int)tileCoordinates.Y];

                var originalTile = MapEditorViewModel.ForegroundTiles
                                                     .SelectMany((row, rowIndex) => row.Select((tile, columnIndex) => new
                                                     {
                                                         tile,
                                                         rowIndex,
                                                         columnIndex
                                                     }))
                                                     .Single(x => x.tile.TileId == sampledTile.TileId);

                SelectCell(originalTile.rowIndex, originalTile.columnIndex);

                break;
            }
        }

        return;

        static void SelectCell(int rowIndex, int columnIndex)
        {
            var dataGrid = MapEditorControl.Instance.TilesControl;
            var selectedCells = dataGrid.SelectedCells;
            selectedCells.Clear();

            var row = dataGrid.Items[rowIndex]!;
            var column = dataGrid.Columns[columnIndex]!;

            selectedCells.Add(new DataGridCellInfo(row, column));
            dataGrid.ScrollIntoView(row, column);
        }
    }

    private void HandleEraseToolClick(SKPoint tileCoordinates) => HandleSelectToolClick(tileCoordinates);

    private void HandleEraseToolDrag(SKPoint tileCoordinates) => HandleSelectToolDrag(tileCoordinates);

    private void HandleEraseToolRelease(SKPoint tileCoordinates)
    {
        HandleEraseToolDrag(tileCoordinates);

        if (TileGrab?.SelectionStart is null)
            return;

        var topX = Math.Min((int)TileGrab.SelectionStart!.Value.X, (int)tileCoordinates.X);
        var topY = Math.Min((int)TileGrab.SelectionStart!.Value.Y, (int)tileCoordinates.Y);
        tileCoordinates = new SKPoint(topX, topY);

        var before = TileGrabViewModel.CreateFrom(
            ViewModel,
            TileGrab,
            MapEditorViewModel.EditingLayerFlags,
            tileCoordinates);

        ViewModel.AddAction(
            ActionType.Erase,
            before,
            TileGrab,
            MapEditorViewModel.EditingLayerFlags,
            tileCoordinates);

        TileGrab?.Erase(ViewModel, MapEditorViewModel.EditingLayerFlags);
        TileGrab = null;
    }

    private void HandleSelectToolDrag(SKPoint tileCoordinates)
    {
        if (TileGrab?.SelectionStart is null)
            return;

        if (tileCoordinates == new SKPoint(-1, -1))
            return;

        var originalSelectionStart = TileGrab.SelectionStart.Value;
        var startX = (int)TileGrab.SelectionStart.Value.X;
        var startY = (int)TileGrab.SelectionStart.Value.Y;
        var tileX = (int)tileCoordinates.X;
        var tileY = (int)tileCoordinates.Y;
        var selectionWidth = Math.Abs(tileX - startX) + 1;
        var selectionHeight = Math.Abs(tileY - startY) + 1;
        var topX = Math.Min(startX, tileX);
        var topY = Math.Min(startY, tileY);

        TileGrab = TileGrabViewModel.Create(
            ViewModel,
            new SKPoint(topX, topY),
            selectionWidth,
            selectionHeight,
            MapEditorViewModel.EditingLayerFlags);
        TileGrab.SelectionStart = originalSelectionStart;
    }
    #endregion

    #region Mouse Handlers
    private void ElementOnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var mousePosition = Element.GetMousePoint()!;
        var tileCoordinates = ConvertMouseToTileCoordinates(mousePosition.Value);

        if (tileCoordinates == new SKPoint(-1, -1))
            return;

        switch (MapEditorViewModel.SelectedTool)
        {
            case ToolType.Draw:
            {
                HandleDrawToolClick(tileCoordinates);

                break;
            }
            case ToolType.Select:
            {
                HandleSelectToolClick(tileCoordinates);

                break;
            }
            case ToolType.Sample:
            {
                HandleSampleToolClick(tileCoordinates);

                break;
            }
            case ToolType.Erase:
            {
                HandleEraseToolClick(tileCoordinates);

                break;
            }
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private void ElementOnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (MapEditorViewModel.SelectedTool == ToolType.Erase)
        {
            var mouseCoordinates = Element.GetMousePoint()!;
            var tileCoordinates = ConvertMouseToTileCoordinates(mouseCoordinates.Value);

            HandleEraseToolRelease(tileCoordinates);
        }

        if (MapEditorViewModel.EditingLayerFlags.HasFlag(LayerFlags.Background))
            ViewModel.BackgroundChangePending = true;

        if (MapEditorViewModel.EditingLayerFlags.HasFlag(LayerFlags.LeftForeground)
            || MapEditorViewModel.EditingLayerFlags.HasFlag(LayerFlags.RightForeground))
            ViewModel.ForegroundChangePending = true;
    }

    private void ElementOnMouseMove(object sender, MouseEventArgs e)
    {
        var mousePosition = Element.GetMousePoint();

        if (mousePosition is null)
            return;

        var tileCoordinates = ConvertMouseToTileCoordinates(new SKPoint(mousePosition.Value.X, mousePosition.Value.Y));

        if (tileCoordinates == new SKPoint(-1, -1))
            return;

        if (MapEditorViewModel.MouseHoverTileCoordinates != tileCoordinates)
        {
            MapEditorViewModel.MouseHoverTileCoordinates = tileCoordinates;

            if (MapEditorViewModel.SelectedTool == ToolType.Draw)
            {
                if (TileGrab is not null)
                {
                    //if you drag the mouse while drawing
                    //it will repeat the click for each new tile you pass over
                    if (e.LeftButton == MouseButtonState.Pressed)
                        HandleDrawToolClick(tileCoordinates);

                    if (TileGrab.HasBackgroundTiles)
                        ViewModel.BackgroundChangePending = true;

                    if (TileGrab.HasForegroundTiles)
                        ViewModel.ForegroundChangePending = true;
                }
            } else
            {
                if (MapEditorViewModel.EditingLayerFlags.HasFlag(LayerFlags.Background))
                    ViewModel.BackgroundChangePending = true;

                if (MapEditorViewModel.EditingLayerFlags.HasFlag(LayerFlags.LeftForeground)
                    || MapEditorViewModel.EditingLayerFlags.HasFlag(LayerFlags.RightForeground))
                    ViewModel.ForegroundChangePending = true;
            }

            if (e.LeftButton == MouseButtonState.Pressed)
            {
                if (MapEditorViewModel.SelectedTool == ToolType.Select)
                    HandleSelectToolDrag(tileCoordinates);

                if (MapEditorViewModel.SelectedTool == ToolType.Erase)
                    HandleEraseToolDrag(tileCoordinates);
            }
        }
    }
    #endregion

    #region Rendering
    private void ElementOnPaint(object? sender, SKPaintGLSurfaceEventArgs e)
    {
        using var rendersync = RenderSync.EnterScope();

        var canvas = e.Surface.Canvas;

        // All rendering is now done asynchronously in background tasks
        // This paint handler just draws the cached images

        if (BackgroundImage is null || ForegroundImage is null || TabMapImage is null)
            return;

        // Calculate visible portion of the map to reduce pixels drawn
        var inverted = Element.Matrix.Invert();
        var dpiScale = (float)DpiHelper.GetDpiScaleFactor();
        var topLeft = inverted.MapPoint(new SKPoint(0, 0));
        var bottomRight = inverted.MapPoint(new SKPoint((float)Element.ActualWidth * dpiScale, (float)Element.ActualHeight * dpiScale));

        // viewRect is in map space - what portion of the map is visible on screen
        var viewRect = SKRect.Create(
            topLeft.X,
            topLeft.Y,
            bottomRight.X - topLeft.X,
            bottomRight.Y - topLeft.Y);

        var imgRect = SKRect.Create(
            0,
            0,
            BackgroundImage.Width,
            BackgroundImage.Height);

        var visibleRect = SKRect.Intersect(viewRect, imgRect);

        // Use paint with no filtering for faster drawing
        using var paint = new SKPaint();
        paint.IsAntialias = false;

        // Create subsets containing only visible portions - MUCH smaller GPU uploads!
        // Clamp to actual image bounds to prevent Subset() from throwing
        var visibleRectInt = SKRectI.Ceiling(visibleRect);

        var imageBounds = new SKRectI(
            0,
            0,
            BackgroundImage.Width,
            BackgroundImage.Height);

        visibleRectInt.Intersect(imageBounds);

        SKImage? bgSubset = null;
        SKImage? fgSubset = null;
        SKImage? tmSubset = null;

        Parallel.Invoke(
            () => bgSubset = BackgroundImage.Subset(visibleRectInt),
            () => fgSubset = ForegroundImage.Subset(visibleRectInt),
            () => tmSubset = TabMapImage.Subset(visibleRectInt));

        // Draw the small subsets at the visible rect location (use float position from visibleRect)
        if (bgSubset is not null)
            canvas.DrawImage(
                bgSubset,
                visibleRectInt.Left,
                visibleRectInt.Top,
                paint);

        if (fgSubset is not null)
            canvas.DrawImage(
                fgSubset,
                visibleRectInt.Left,
                visibleRectInt.Top,
                paint);

        if (tmSubset is not null)
            canvas.DrawImage(
                tmSubset,
                visibleRectInt.Left,
                visibleRectInt.Top,
                paint);

        if (bgSubset != BackgroundImage)
            bgSubset?.Dispose();

        if (fgSubset != ForegroundImage)
            fgSubset?.Dispose();

        if (tmSubset != TabMapImage)
            tmSubset?.Dispose();
    }

    private void RenderTabMap(SKPoint mouseCoordinates, bool leftButtonPressed)
    {
        var width = (ViewModel.Bounds.Width + ViewModel.Bounds.Height + 1) * DALIB_CONSTANTS.HALF_TILE_WIDTH;
        var height = (ViewModel.Bounds.Width + ViewModel.Bounds.Height + 1) * DALIB_CONSTANTS.HALF_TILE_HEIGHT + FOREGROUND_PADDING;
        var lfgTiles = ViewModel.LeftForegroundTilesView;
        var rfgTiles = ViewModel.RightForegroundTilesView;

        using var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);

        var fgInitialDrawX = (ViewModel.Bounds.Height - 1) * DALIB_CONSTANTS.HALF_TILE_WIDTH;
        var fgInitialDrawY = FOREGROUND_PADDING;
        var bounds = ViewModel.Bounds;
        var tglfgTiles = new ListSegment2D<TileViewModel>();
        var tgrfgTiles = new ListSegment2D<TileViewModel>();
        var tabWallImage = MapEditorRenderUtil.RenderTabWall();
        var tileOutline = MapEditorRenderUtil.RenderTileOutline();
        var isEditingLeftForeground = MapEditorViewModel.EditingLayerFlags.HasFlag(LayerFlags.LeftForeground);
        var isEditingRightForeground = MapEditorViewModel.EditingLayerFlags.HasFlag(LayerFlags.RightForeground);
        var seededRandom = new Random(8675309);

        if (TileGrab is not null)
        {
            tglfgTiles = TileGrab.LeftForegroundTilesView;
            tgrfgTiles = TileGrab.RightForegroundTilesView;
        }

        for (var y = 0; y < bounds.Height; y++)
        {
            for (var x = 0; x < bounds.Width; x++)
            {
                var leftTileViewModel = lfgTiles[x, y];
                var rightTileViewModel = rfgTiles[x, y];
                var point = new Point(x, y);
                SKPaint? leftForegroundPaint = null;
                SKPaint? rightForegroundPaint = null;

                if (isEditingLeftForeground)
                    HandleLeftForegroundToolHover(
                        point,
                        mouseCoordinates,
                        tglfgTiles,
                        ref leftTileViewModel,
                        ref leftForegroundPaint,
                        leftButtonPressed);

                if (isEditingRightForeground)
                    HandleRightForegroundToolHover(
                        point,
                        mouseCoordinates,
                        tgrfgTiles,
                        ref rightTileViewModel,
                        ref rightForegroundPaint,
                        leftButtonPressed);

                var isWall = MapEditorRenderUtil.IsWall(leftTileViewModel.TileId) || MapEditorRenderUtil.IsWall(rightTileViewModel.TileId);
                var paint = leftForegroundPaint ?? rightForegroundPaint;

                var gridPaint = new SKPaint
                {
                    BlendMode = SKBlendMode.Hue
                };

                var tlx = fgInitialDrawX + x * DALIB_CONSTANTS.HALF_TILE_WIDTH;
                var tly = fgInitialDrawY + x * DALIB_CONSTANTS.HALF_TILE_HEIGHT;

                if (MapEditorViewModel.ShowTabMap && isWall)
                    canvas.DrawImage(
                        tabWallImage,
                        tlx,
                        tly,
                        paint);

                if (MapEditorViewModel.ShowGrid)
                    canvas.DrawImage(
                        tileOutline,
                        tlx,
                        tly,
                        gridPaint);

                if (MapEditorViewModel.ShowForegroundGrid)
                {
                    var randomColor = SKColorExtensions.GetRandomVividColor(seededRandom);
                    var randomPhase = seededRandom.Next(6);

                    var fgGridPaint = new SKPaint
                    {
                        Color = randomColor,
                        Style = SKPaintStyle.Stroke,
                        StrokeWidth = 1,
                        PathEffect = SKPathEffect.CreateDash(
                            [
                                2,
                                3
                            ],
                            randomPhase),
                        BlendMode = SKBlendMode.SrcOver
                    };

                    tly += DALIB_CONSTANTS.TILE_HEIGHT;

                    if (leftTileViewModel.TileId.IsRenderedTileIndex() && leftTileViewModel.CurrentFrame is { } lframe)
                    {
                        var leftRect = new SKRect(
                            tlx,
                            tly - lframe.Height,
                            tlx + lframe.Width,
                            tly);

                        canvas.DrawRect(leftRect, fgGridPaint);
                    }

                    tlx += DALIB_CONSTANTS.HALF_TILE_WIDTH;

                    if (rightTileViewModel.TileId.IsRenderedTileIndex() && rightTileViewModel.CurrentFrame is { } rframe)
                    {
                        var rightRect = new SKRect(
                            tlx,
                            tly - rframe.Height,
                            tlx + rframe.Width,
                            tly);

                        canvas.DrawRect(rightRect, fgGridPaint);
                    }
                }
            }

            fgInitialDrawX -= DALIB_CONSTANTS.HALF_TILE_WIDTH;
            fgInitialDrawY += DALIB_CONSTANTS.HALF_TILE_HEIGHT;
        }

        var oldImage = TabMapImage;
        var image = SKImage.FromBitmap(bitmap);

        using (RenderSync.EnterScope())
            TabMapImage = image;

        oldImage?.Dispose();
    }

    private void RenderBackground(SKPoint mouseCoordinates, bool leftButtonPressed)
    {
        var width = (ViewModel.Bounds.Width + ViewModel.Bounds.Height + 1) * DALIB_CONSTANTS.HALF_TILE_WIDTH;
        var height = (ViewModel.Bounds.Width + ViewModel.Bounds.Height + 1) * DALIB_CONSTANTS.HALF_TILE_HEIGHT + FOREGROUND_PADDING;
        var bgTiles = ViewModel.BackgroundTilesView;

        using var bitmap = new SKBitmap(width, height);

        if (MapEditorViewModel.ShowBackground)
        {
            using var canvas = new SKCanvas(bitmap);

            var bgInitialDrawX = (ViewModel.Bounds.Height - 1) * DALIB_CONSTANTS.HALF_TILE_WIDTH;
            var bgInitialDrawY = FOREGROUND_PADDING;
            var bounds = ViewModel.Bounds;
            var tgbgTiles = new ListSegment2D<TileViewModel>();
            var isEditingBackground = MapEditorViewModel.EditingLayerFlags.HasFlag(LayerFlags.Background);

            if (TileGrab is not null)
                tgbgTiles = TileGrab.BackgroundTilesView;

            for (var y = 0; y < bounds.Height; y++)
            {
                for (var x = 0; x < bounds.Width; x++)
                {
                    var point = new Point(x, y);
                    var tileViewModel = bgTiles[x, y];
                    SKPaint? paint = null;

                    if (isEditingBackground)
                        HandleBackgroundToolHover(
                            point,
                            mouseCoordinates,
                            tgbgTiles,
                            ref tileViewModel,
                            ref paint,
                            leftButtonPressed);

                    var currentFrame = tileViewModel.CurrentFrame;

                    canvas.DrawImage(
                        currentFrame,
                        bgInitialDrawX + x * DALIB_CONSTANTS.HALF_TILE_WIDTH,
                        bgInitialDrawY + x * DALIB_CONSTANTS.HALF_TILE_HEIGHT,
                        paint);

                    paint?.Dispose();
                }

                bgInitialDrawX -= DALIB_CONSTANTS.HALF_TILE_WIDTH;
                bgInitialDrawY += DALIB_CONSTANTS.HALF_TILE_HEIGHT;
            }
        }

        var oldImage = BackgroundImage;
        var image = SKImage.FromBitmap(bitmap);

        using (RenderSync.EnterScope())
            BackgroundImage = image;

        oldImage?.Dispose();
    }

    public SKImage RenderMapImage()
    {
        using var rendersync = RenderSync.EnterScope();

        var dimensions = ImageHelper.CalculateRenderedImageSize(
            ViewModel.BackgroundTilesView,
            ViewModel.LeftForegroundTilesView,
            ViewModel.RightForegroundTilesView);
        using var bitmap = new SKBitmap(dimensions.Width, dimensions.Height + FOREGROUND_PADDING);
        using var canvas = new SKCanvas(bitmap);

        canvas.DrawImage(BackgroundImage, SKPoint.Empty);
        canvas.DrawImage(ForegroundImage, SKPoint.Empty);
        canvas.DrawImage(TabMapImage, SKPoint.Empty);

        canvas.Flush();

        return SKImage.FromBitmap(bitmap);
    }

    private void HandleBackgroundToolHover(
        Point currentPoint,
        SKPoint mouseCoordinates,
        ListSegment2D<TileViewModel> backgroundTiles,
        ref TileViewModel tileViewModel,
        ref SKPaint? paint,
        bool leftButtonPressed)
    {
        if (mouseCoordinates == new SKPoint(-1, -1))
            return;

        var tileGrab = TileGrab;

        var selectionBounds = tileGrab?.SelectionStart is not null ? tileGrab.Bounds : null;

        var drawBounds = tileGrab is not null
            ? new ValueRectangle(
                (int)mouseCoordinates.X,
                (int)mouseCoordinates.Y,
                tileGrab.Bounds.Width,
                tileGrab.Bounds.Height)
            : new ValueRectangle(
                -1,
                -1,
                0,
                0);

        switch (MapEditorViewModel.SelectedTool)
        {
            case ToolType.Draw:
            {
                if (drawBounds.Contains(currentPoint) && tileGrab!.HasBackgroundTiles)
                {
                    var tileGrabX = (int)(currentPoint.X - mouseCoordinates.X);
                    var tileGrabY = (int)(currentPoint.Y - mouseCoordinates.Y);

                    // if the tilegrab is empty, dont overwrite what's already there
                    if (backgroundTiles[tileGrabX, tileGrabY].TileId == 0)
                        return;

                    tileViewModel = backgroundTiles[tileGrabX, tileGrabY];
                }

                break;
            }
            case ToolType.Select:
            {
                if (leftButtonPressed)
                {
                    //if lmb is pressed, highlight all tiles in the selection rectangle
                    if (selectionBounds?.Contains(currentPoint) ?? false)
                        paint = GetBrightenPaint();
                }

                //if lmb is not pressed, highlight only the tile under the mouse
                else if (((int)mouseCoordinates.X == currentPoint.X) && ((int)mouseCoordinates.Y == currentPoint.Y))
                    paint = GetBrightenPaint();

                break;
            }
            case ToolType.Sample:
                if (((int)mouseCoordinates.X == currentPoint.X) && ((int)mouseCoordinates.Y == currentPoint.Y))
                    paint = GetBrightenPaint();

                break;
            case ToolType.Erase:
                if (leftButtonPressed)
                {
                    //if lmb is pressed, highlight all tiles in the selection rectangle
                    if (selectionBounds?.Contains(currentPoint) ?? false)
                        paint = GetBrightenPaint();
                }

                //if lmb is not pressed, highlight only the tile under the mouse
                else if (((int)mouseCoordinates.X == currentPoint.X) && ((int)mouseCoordinates.Y == currentPoint.Y))
                    paint = GetBrightenPaint();

                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private void RenderForeground(SKPoint mouseCoordinates, bool leftButtonPressed)
    {
        var width = (ViewModel.Bounds.Width + ViewModel.Bounds.Height + 1) * DALIB_CONSTANTS.HALF_TILE_WIDTH;
        var height = (ViewModel.Bounds.Width + ViewModel.Bounds.Height + 1) * DALIB_CONSTANTS.HALF_TILE_HEIGHT + FOREGROUND_PADDING;
        var lfgTiles = ViewModel.LeftForegroundTilesView;
        var rfgTiles = ViewModel.RightForegroundTilesView;

        using var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);

        var fgInitialDrawX = (ViewModel.Bounds.Height - 1) * DALIB_CONSTANTS.HALF_TILE_WIDTH;
        var fgInitialDrawY = FOREGROUND_PADDING;
        var bounds = ViewModel.Bounds;
        var tglfgTiles = new ListSegment2D<TileViewModel>();
        var tgrfgTiles = new ListSegment2D<TileViewModel>();
        var isEditingLeftForeground = MapEditorViewModel.EditingLayerFlags.HasFlag(LayerFlags.LeftForeground);
        var isEditingRightForeground = MapEditorViewModel.EditingLayerFlags.HasFlag(LayerFlags.RightForeground);

        if (TileGrab is not null)
        {
            tglfgTiles = TileGrab.LeftForegroundTilesView;
            tgrfgTiles = TileGrab.RightForegroundTilesView;
        }

        for (var y = 0; y < bounds.Height; y++)
        {
            for (var x = 0; x < bounds.Width; x++)
            {
                var leftTileViewModel = lfgTiles[x, y];
                var rightTileViewModel = rfgTiles[x, y];
                var point = new Point(x, y);
                SKPaint? leftForegroundPaint = null;
                SKPaint? rightForegroundPaint = null;

                if (isEditingLeftForeground)
                    HandleLeftForegroundToolHover(
                        point,
                        mouseCoordinates,
                        tglfgTiles,
                        ref leftTileViewModel,
                        ref leftForegroundPaint,
                        leftButtonPressed);

                if (isEditingRightForeground)
                    HandleRightForegroundToolHover(
                        point,
                        mouseCoordinates,
                        tgrfgTiles,
                        ref rightTileViewModel,
                        ref rightForegroundPaint,
                        leftButtonPressed);

                var leftCurrentFrame = leftTileViewModel.CurrentFrame;
                var rightCurrentFrame = rightTileViewModel.CurrentFrame;

                if (MapEditorViewModel.ShowLeftForeground && leftCurrentFrame is not null && leftTileViewModel.TileId.IsRenderedTileIndex())
                    canvas.DrawImage(
                        leftCurrentFrame,
                        fgInitialDrawX + x * DALIB_CONSTANTS.HALF_TILE_WIDTH,
                        fgInitialDrawY
                        + (x + 1) * DALIB_CONSTANTS.HALF_TILE_HEIGHT
                        - leftCurrentFrame.Height
                        + DALIB_CONSTANTS.HALF_TILE_HEIGHT,
                        leftForegroundPaint);

                if (MapEditorViewModel.ShowRightForeground
                    && rightCurrentFrame is not null
                    && rightTileViewModel.TileId.IsRenderedTileIndex())
                    canvas.DrawImage(
                        rightCurrentFrame,
                        fgInitialDrawX + (x + 1) * DALIB_CONSTANTS.HALF_TILE_WIDTH,
                        fgInitialDrawY
                        + (x + 1) * DALIB_CONSTANTS.HALF_TILE_HEIGHT
                        - rightCurrentFrame.Height
                        + DALIB_CONSTANTS.HALF_TILE_HEIGHT,
                        rightForegroundPaint);
            }

            fgInitialDrawX -= DALIB_CONSTANTS.HALF_TILE_WIDTH;
            fgInitialDrawY += DALIB_CONSTANTS.HALF_TILE_HEIGHT;
        }

        var oldImage = ForegroundImage;
        var image = SKImage.FromBitmap(bitmap);

        using (RenderSync.EnterScope())
            ForegroundImage = image;

        oldImage?.Dispose();
    }

    private void HandleLeftForegroundToolHover(
        Point currentPoint,
        SKPoint mouseCoordinates,
        ListSegment2D<TileViewModel> leftForegroundTiles,
        ref TileViewModel currentFrame,
        ref SKPaint? paint,
        bool leftButtonPressed)
    {
        if (mouseCoordinates == new SKPoint(-1, -1))
            return;

        var tileGrab = TileGrab;

        var selectionBounds = tileGrab?.SelectionStart is not null ? tileGrab.Bounds : null;

        var drawBounds = tileGrab is not null
            ? new ValueRectangle(
                (int)mouseCoordinates.X,
                (int)mouseCoordinates.Y,
                tileGrab.Bounds.Width,
                tileGrab.Bounds.Height)
            : new ValueRectangle(
                -1,
                -1,
                0,
                0);

        switch (MapEditorViewModel.SelectedTool)
        {
            case ToolType.Draw:
            {
                if (drawBounds.Contains(currentPoint) && tileGrab!.HasLeftForegroundTiles)
                {
                    var tileGrabX = (int)(currentPoint.X - mouseCoordinates.X);
                    var tileGrabY = (int)(currentPoint.Y - mouseCoordinates.Y);

                    // if the tilegrab is empty, dont overwrite what's already there
                    if (leftForegroundTiles[tileGrabX, tileGrabY].TileId == 0)
                        return;

                    currentFrame = leftForegroundTiles[tileGrabX, tileGrabY];
                }

                break;
            }
            case ToolType.Select:
            {
                if (leftButtonPressed)
                {
                    //if lmb is pressed, highlight all tiles in the selection rectangle
                    if (selectionBounds?.Contains(currentPoint) ?? false)
                        paint = GetBrightenPaint();
                }

                //if lmb is not pressed, highlight only the tile under the mouse
                else if (((int)mouseCoordinates.X == currentPoint.X) && ((int)mouseCoordinates.Y == currentPoint.Y))
                    paint = GetBrightenPaint();

                break;
            }
            case ToolType.Sample:
                if (((int)mouseCoordinates.X == currentPoint.X) && ((int)mouseCoordinates.Y == currentPoint.Y))
                    paint = GetBrightenPaint();

                break;
            case ToolType.Erase:
                if (leftButtonPressed)
                {
                    //if lmb is pressed, highlight all tiles in the selection rectangle
                    if (selectionBounds?.Contains(currentPoint) ?? false)
                        paint = GetBrightenPaint();
                }

                //if lmb is not pressed, highlight only the tile under the mouse
                else if (((int)mouseCoordinates.X == currentPoint.X) && ((int)mouseCoordinates.Y == currentPoint.Y))
                    paint = GetBrightenPaint();

                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private void HandleRightForegroundToolHover(
        Point currentPoint,
        SKPoint mouseCoordinates,
        ListSegment2D<TileViewModel> rightForegroundTiles,
        ref TileViewModel currentFrame,
        ref SKPaint? paint,
        bool leftButtonPressed)
    {
        if (mouseCoordinates == new SKPoint(-1, -1))
            return;

        var tileGrab = TileGrab;

        var selectionBounds = tileGrab?.SelectionStart is not null ? tileGrab.Bounds : null;

        var drawBounds = tileGrab is not null
            ? new ValueRectangle(
                (int)mouseCoordinates.X,
                (int)mouseCoordinates.Y,
                tileGrab.Bounds.Width,
                tileGrab.Bounds.Height)
            : new ValueRectangle(
                -1,
                -1,
                0,
                0);

        switch (MapEditorViewModel.SelectedTool)
        {
            case ToolType.Draw:
            {
                if (drawBounds.Contains(currentPoint) && tileGrab!.HasRightForegroundTiles)
                {
                    var tileGrabX = (int)(currentPoint.X - mouseCoordinates.X);
                    var tileGrabY = (int)(currentPoint.Y - mouseCoordinates.Y);

                    // if the tilegrab is empty, dont overwrite what's already there
                    if (rightForegroundTiles[tileGrabX, tileGrabY].TileId == 0)
                        return;

                    currentFrame = rightForegroundTiles[tileGrabX, tileGrabY];
                }

                break;
            }
            case ToolType.Select:
            {
                if (leftButtonPressed)
                {
                    //if lmb is pressed, highlight all tiles in the selection rectangle
                    if (selectionBounds?.Contains(currentPoint) ?? false)
                        paint = GetBrightenPaint();
                }

                //if lmb is not pressed, highlight only the tile under the mouse
                else if (((int)mouseCoordinates.X == currentPoint.X) && ((int)mouseCoordinates.Y == currentPoint.Y))
                    paint = GetBrightenPaint();

                break;
            }
            case ToolType.Sample:
                if (((int)mouseCoordinates.X == currentPoint.X) && ((int)mouseCoordinates.Y == currentPoint.Y))
                    paint = GetBrightenPaint();

                break;
            case ToolType.Erase:
                if (leftButtonPressed)
                {
                    //if lmb is pressed, highlight all tiles in the selection rectangle
                    if (selectionBounds?.Contains(currentPoint) ?? false)
                        paint = GetBrightenPaint();
                }

                //if lmb is not pressed, highlight only the tile under the mouse
                else if (((int)mouseCoordinates.X == currentPoint.X) && ((int)mouseCoordinates.Y == currentPoint.Y))
                    paint = GetBrightenPaint();

                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }
    #endregion

    #region Property Changed
    private void MapEditorViewModelOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName))
            return;

        if (e.PropertyName.EqualsI(nameof(MapEditorViewModel.TileGrab)) && MapEditorViewModel.TileGrab is not null)
        {
            if (HistoricalTileGrab is not null)
                HistoricalTileGrab.PropertyChanged -= TileGrabOnPropertyChanged;

            HistoricalTileGrab = MapEditorViewModel.TileGrab;

            if (HistoricalTileGrab is not null)
                HistoricalTileGrab.PropertyChanged += TileGrabOnPropertyChanged;
        }
    }

    private void TileGrabOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName))
            return;

        if (e.PropertyName.EqualsI(nameof(TileGrab.RawBackgroundTiles)))
            ViewModel.BackgroundChangePending = true;

        if (e.PropertyName.EqualsI(nameof(TileGrab.RawLeftForegroundTiles))
            || e.PropertyName.EqualsI(nameof(TileGrab.RawRightForegroundTiles)))
            ViewModel.ForegroundChangePending = true;
    }

    private void ViewModelOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName))
            return;

        // Calculate mouse state once for all renders
        var mousePoint = Element.GetMousePoint();
        var leftButtonPressed = Mouse.LeftButton == MouseButtonState.Pressed;
        var mapPoint = mousePoint.HasValue ? ConvertMouseToTileCoordinates(mousePoint.Value) : new SKPoint(-1, -1);

        if (e.PropertyName.EqualsI(nameof(MapViewerViewModel.BackgroundChangePending)) && ViewModel.BackgroundChangePending)
            QueueBackgroundRender(mapPoint, leftButtonPressed);

        if (e.PropertyName.EqualsI(nameof(MapViewerViewModel.ForegroundChangePending)) && ViewModel.ForegroundChangePending)
            QueueForegroundRender(mapPoint, leftButtonPressed);

        if (e.PropertyName.EqualsI(nameof(MapViewerViewModel.TabMapChangePending)) && ViewModel.TabMapChangePending)
            QueueTabMapRender(mapPoint, leftButtonPressed);
    }

    private void QueueBackgroundRender(SKPoint mapPoint, bool leftButtonPressed)
    {
        // Rate limit: Don't render more than 60 FPS
        var now = DateTime.UtcNow;

        LastRequestedBackgroundRenderTime = now;
        ViewModel.BackgroundChangePending = false;

        // Don't start a new render if one is already in progress
        if (BackgroundRenderTask is { IsCompleted: false })
            return;

        BackgroundRenderTask = Task.Run(() =>
        {
            while (LastRequestedBackgroundRenderTime > LastBackgroundRenderTime)
            {
                // No lock needed! SKImages are immutable, so even if tiles update mid-render,
                // we just get a mix of old/new frames which is fine and will fix itself next render
                RenderBackground(mapPoint, leftButtonPressed);
                LastBackgroundRenderTime = DateTime.UtcNow;

                Dispatcher.BeginInvoke(() =>
                {
                    Element.Redraw();
                });
            }
        });
    }

    private void QueueForegroundRender(SKPoint mapPoint, bool leftButtonPressed)
    {
        // Rate limit: Don't render more than 60 FPS
        var now = DateTime.UtcNow;

        LastRequestedForegroundRenderTime = now;
        ViewModel.ForegroundChangePending = false;

        // Don't start a new render if one is already in progress
        if (ForegroundRenderTask is { IsCompleted: false })
            return;

        ForegroundRenderTask = Task.Run(() =>
        {
            while (LastRequestedForegroundRenderTime > LastForegroundRenderTime)
            {
                RenderForeground(mapPoint, leftButtonPressed);
                LastForegroundRenderTime = DateTime.UtcNow;

                Dispatcher.BeginInvoke(() =>
                {
                    Element.Redraw();
                });
            }
        });
    }

    private void QueueTabMapRender(SKPoint mapPoint, bool leftButtonPressed)
    {
        // Rate limit: Don't render more than 60 FPS
        var now = DateTime.UtcNow;

        LastRequestedTabMapRenderTime = now;
        ViewModel.TabMapChangePending = false;

        // Don't start a new render if one is already in progress
        if (TabMapRenderTask is { IsCompleted: false })
            return;

        TabMapRenderTask = Task.Run(() =>
        {
            while (LastRequestedTabMapRenderTime > LastTabMapRenderTime)
            {
                // No lock needed - renders can run in parallel since they write to separate images
                RenderTabMap(mapPoint, leftButtonPressed);
                LastTabMapRenderTime = DateTime.UtcNow;

                Dispatcher.BeginInvoke(() =>
                {
                    Element.Redraw();
                });
            }
        });
    }

    private void MapViewerControl_OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        BackgroundImage = null;
        ForegroundImage = null;

        if (e.OldValue is MapViewerViewModel oldViewModel)
        {
            oldViewModel.PropertyChanged -= ViewModelOnPropertyChanged;
            oldViewModel.Control = null;
            oldViewModel.ViewerTransform = Element.Matrix;
        }

        if (DataContext is null)
            ViewModel = MapViewerViewModel.Empty;
        else
            ViewModel = (MapViewerViewModel)DataContext;

        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        // removal from the collection will trigger this and set datacontext to null
        if (ViewModel is null)
        {
            DataContext = MapViewerViewModel.Empty;

            return;
        }

        ViewModel.PropertyChanged += ViewModelOnPropertyChanged;
        ViewModel.Control = this;

        if (SetViewerTransform())
        {
            Element.Matrix = ViewModel.ViewerTransform!.Value;
            Element.Redraw();
        }

        if ((ViewModel.Bounds.Width <= 0) || (ViewModel.Bounds.Height <= 0))
            return;
        
        ViewModel.Refresh();
        ViewModel.BackgroundChangePending = false;
        ViewModel.ForegroundChangePending = false;
        ViewModel.TabMapChangePending = false;
        ViewModel.BackgroundChangePending = true;
        ViewModel.ForegroundChangePending = true;
        ViewModel.TabMapChangePending = true;
    }
    #endregion
}