using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Chaos.Extensions.Common;
using Chaos.Extensions.Geometry;
using ChaosAssetManager.Controls.PreviewControls;
using ChaosAssetManager.Definitions;
using ChaosAssetManager.Helpers;
using ChaosAssetManager.Model;
using ChaosAssetManager.ViewModel;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using DALIB_CONSTANTS = DALib.Definitions.CONSTANTS;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Point = Chaos.Geometry.Point;
using Rectangle = Chaos.Geometry.Rectangle;

// ReSharper disable ClassCanBeSealed.Global

namespace ChaosAssetManager.Controls.MapEditorControls;

public partial class MapViewerControl
{
    public const int FOREGROUND_PADDING = 512;
    private SKImage? BackgroundImage;
    private SKImage? ForegroundImage;
    private TileGrab? TileGrab;
    public SKGLElementPlus Element { get; }
    private MapEditorViewModel MapEditorViewModel => MapEditorControl.Instance.ViewModel;
    public MapViewerViewModel ViewModel => (DataContext as MapViewerViewModel)!;

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
    }

    #region Utilities
    private SKPaint GetBrightenPaint()
    {
        const float BRIGHTEN_FACTOR = 1.5f;
        
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

    private SKPoint ConvertMouseToTileCoordinates(SKPoint mousePoint)
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
            return new SKPoint(-1, -1);

        return new SKPoint(point.X, point.Y);
    }

    private void HandleDrawToolClick(SKPoint tileCoordinates)
    {
        if (TileGrab is null || (tileCoordinates == new SKPoint(-1, -1)))
            return;

        var tileX = (int)tileCoordinates.X;
        var tileY = (int)tileCoordinates.Y;

        if (TileGrab.HasBackgroundTiles && MapEditorViewModel.EditingLayerFlags.HasFlag(LayerFlags.Background))
            for (var y = 0; y < TileGrab.Bounds.Height; y++)
            {
                for (var x = 0; x < TileGrab.Bounds.Width; x++)
                {
                    var tile = TileGrab.BackgroundTilesView[x, y]
                                       .Clone();
                    tile.Initialize();
                    var bgTiles = ViewModel.BackgroundTilesView;

                    var point = new Point(tileX + x, tileY + y);

                    if (!ViewModel.Bounds.Contains(point))
                        continue;

                    bgTiles[point.X, point.Y] = tile;
                }
            }

        if (TileGrab.HasLeftForegroundTiles && MapEditorViewModel.EditingLayerFlags.HasFlag(LayerFlags.LeftForeground))
            for (var y = 0; y < TileGrab.Bounds.Height; y++)
            {
                for (var x = 0; x < TileGrab.Bounds.Width; x++)
                {
                    var tile = TileGrab.LeftForegroundTilesView[x, y]
                                       .Clone();
                    tile.LayerFlags = LayerFlags.LeftForeground;

                    tile.Initialize();
                    var fgTiles = ViewModel.LeftForegroundTilesView;

                    var point = new Point(tileX + x, tileY + y);

                    if (!ViewModel.Bounds.Contains(point))
                        continue;

                    fgTiles[point.X, point.Y] = tile;
                }
            }

        if (TileGrab.HasRightForegroundTiles && MapEditorViewModel.EditingLayerFlags.HasFlag(LayerFlags.RightForeground))
            for (var y = 0; y < TileGrab.Bounds.Height; y++)
            {
                for (var x = 0; x < TileGrab.Bounds.Width; x++)
                {
                    var tile = TileGrab.RightForegroundTilesView[x, y]
                                       .Clone();
                    tile.LayerFlags = LayerFlags.RightForeground;

                    tile.Initialize();
                    var fgTiles = ViewModel.RightForegroundTilesView;

                    var point = new Point(tileX + x, tileY + y);

                    if (!ViewModel.Bounds.Contains(point))
                        continue;

                    fgTiles[point.X, point.Y] = tile;
                }
            }
    }

    private void HandleSelectToolClick(SKPoint tileCoordinates)
    {
        var tileX = (int)tileCoordinates.X;
        var tileY = (int)tileCoordinates.Y;

        TileGrab = new TileGrab
        {
            Bounds = new Rectangle(
                0,
                0,
                1,
                1),
            SelectionStart = new SKPoint(tileX, tileY)
        };

        if (MapEditorViewModel.EditingLayerFlags.HasFlag(LayerFlags.Background))
            TileGrab.RawBackgroundTiles.Add(ViewModel.BackgroundTilesView[tileX, tileY]);

        if (MapEditorViewModel.EditingLayerFlags.HasFlag(LayerFlags.LeftForeground))
            TileGrab.RawLeftForegroundTiles.Add(ViewModel.LeftForegroundTilesView[tileX, tileY]);

        if (MapEditorViewModel.EditingLayerFlags.HasFlag(LayerFlags.RightForeground))
            TileGrab.RawRightForegroundTiles.Add(ViewModel.RightForegroundTilesView[tileX, tileY]);
    }

    private void HandleSampleToolClick(SKPoint tileCoordinates)
    {
        if (MapEditorViewModel.EditingLayerFlags is LayerFlags.All or LayerFlags.Foreground)
            return;

        switch (MapEditorViewModel.EditingLayerFlags)
        {
            case LayerFlags.Background:
            {
                var sampledTile = ViewModel.LeftForegroundTilesView[(int)tileCoordinates.X, (int)tileCoordinates.Y];

                var originalTile = MapEditorViewModel.BackgroundTiles
                                                     .SelectMany(
                                                         (row, rowIndex) => row.Select(
                                                             (tile, columnIndex) => new
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
                                                     .SelectMany(
                                                         (row, rowIndex) => row.Select(
                                                             (tile, columnIndex) => new
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
                                                     .SelectMany(
                                                         (row, rowIndex) => row.Select(
                                                             (tile, columnIndex) => new
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

        //delete tiles from map that are in the tilegraab
        if (TileGrab is null)
            return;

        for (var y = TileGrab.Bounds.Top; y <= TileGrab.Bounds.Bottom; y++)
        {
            for (var x = TileGrab.Bounds.Left; x <= TileGrab.Bounds.Right; x++)
            {
                if (TileGrab.HasBackgroundTiles && MapEditorViewModel.EditingLayerFlags.HasFlag(LayerFlags.Background))
                {
                    var local = ViewModel.BackgroundTilesView;
                    local[x, y] = TileViewModel.EmptyBackground;
                }

                if (TileGrab.HasLeftForegroundTiles && MapEditorViewModel.EditingLayerFlags.HasFlag(LayerFlags.LeftForeground))
                {
                    var local = ViewModel.LeftForegroundTilesView;
                    local[x, y] = TileViewModel.EmptyLeftForeground;
                }

                if (TileGrab.HasRightForegroundTiles && MapEditorViewModel.EditingLayerFlags.HasFlag(LayerFlags.RightForeground))
                {
                    var local = ViewModel.RightForegroundTilesView;
                    local[x, y] = TileViewModel.EmptyRightForeground;
                }
            }
        }
    }

    private void HandleSelectToolDrag(SKPoint tileCoordinates)
    {
        if (TileGrab?.SelectionStart is null)
            return;

        var startX = (int)TileGrab.SelectionStart.Value.X;
        var startY = (int)TileGrab.SelectionStart.Value.Y;
        var tileX = (int)tileCoordinates.X;
        var tileY = (int)tileCoordinates.Y;
        var selectionWidth = Math.Abs(tileX - startX) + 1;
        var selectionHeight = Math.Abs(tileY - startY) + 1;
        var topX = Math.Min(startX, tileX);
        var topY = Math.Min(startY, tileY);

        var selectionBounds = new Rectangle(
            topX,
            topY,
            selectionWidth,
            selectionHeight);

        TileGrab.Bounds = selectionBounds;

        if (MapEditorViewModel.EditingLayerFlags.HasFlag(LayerFlags.Background))
        {
            TileGrab.RawBackgroundTiles.Clear();

            for (var y = 0; y < selectionHeight; y++)
                for (var x = 0; x < selectionWidth; x++)
                    TileGrab.RawBackgroundTiles.Add(ViewModel.BackgroundTilesView[topX + x, topY + y]);
        }

        if (MapEditorViewModel.EditingLayerFlags.HasFlag(LayerFlags.LeftForeground))
        {
            TileGrab.RawLeftForegroundTiles.Clear();

            for (var y = 0; y < selectionHeight; y++)
                for (var x = 0; x < selectionWidth; x++)
                    TileGrab.RawLeftForegroundTiles.Add(ViewModel.LeftForegroundTilesView[topX + x, topY + y]);
        }

        if (MapEditorViewModel.EditingLayerFlags.HasFlag(LayerFlags.RightForeground))
        {
            TileGrab.RawRightForegroundTiles.Clear();

            for (var y = 0; y < selectionHeight; y++)
                for (var x = 0; x < selectionWidth; x++)
                    TileGrab.RawRightForegroundTiles.Add(ViewModel.RightForegroundTilesView[topX + x, topY + y]);
        }
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
        var canvas = e.Surface.Canvas;

        canvas.Clear(SKColors.Black);

        var mousePoint = Element.GetMousePoint();
        var mapPoint = ConvertMouseToTileCoordinates(mousePoint!.Value);

        var backgroundTiles = ViewModel.BackgroundTilesView;
        var leftForegroundTiles = ViewModel.LeftForegroundTilesView;
        var rightForegroundTiles = ViewModel.RightForegroundTilesView;

        RenderBackground(mapPoint, backgroundTiles);
        RenderForeground(mapPoint, leftForegroundTiles, rightForegroundTiles);

        canvas.DrawImage(BackgroundImage, SKPoint.Empty);
        canvas.DrawImage(ForegroundImage, SKPoint.Empty);

        //draw tabgrid if enabled

        canvas.Flush();

        ViewModel.BackgroundChangePending = false;
        ViewModel.ForegroundChangePending = false;
    }

    private void RenderBackground(SKPoint mouseCoordinates, ListSegment2D<TileViewModel> backgroundTiles)
    {
        if (ViewModel is { BackgroundChangePending: false, ForegroundChangePending: false })
            return;

        var width = (ViewModel.Bounds.Width + ViewModel.Bounds.Height + 1) * DALIB_CONSTANTS.HALF_TILE_WIDTH;
        var height = (ViewModel.Bounds.Width + ViewModel.Bounds.Height + 1) * DALIB_CONSTANTS.HALF_TILE_HEIGHT + FOREGROUND_PADDING;

        using var bitmap = new SKBitmap(width, height);

        if (MapEditorViewModel.ShowBackground)
        {
            using var canvas = new SKCanvas(bitmap);

            var bgInitialDrawX = (ViewModel.Bounds.Height - 1) * DALIB_CONSTANTS.HALF_TILE_WIDTH;
            var bgInitialDrawY = FOREGROUND_PADDING;
            var bounds = ViewModel.Bounds;

            for (var y = 0; y < bounds.Height; y++)
            {
                for (var x = 0; x < bounds.Width; x++)
                {
                    var point = new Point(x, y);
                    var tileViewModel = backgroundTiles[x, y];
                    SKPaint? paint = null;

                    if (MapEditorViewModel.EditingLayerFlags.HasFlag(LayerFlags.Background))
                        HandleBackgroundToolHover(
                            point,
                            mouseCoordinates,
                            ref tileViewModel,
                            ref paint);

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

        BackgroundImage?.Dispose();
        BackgroundImage = SKImage.FromBitmap(bitmap);
    }

    private void HandleBackgroundToolHover(
        Point currentPoint,
        SKPoint mouseCoordinates,
        ref TileViewModel tileViewModel,
        ref SKPaint? paint)
    {
        if (mouseCoordinates == new SKPoint(-1, -1))
            return;

        var tileGrab = TileGrab;

        var selectionBounds = tileGrab?.SelectionStart is not null ? tileGrab.Bounds : null;

        var drawBounds = tileGrab is not null
            ? new Rectangle(
                (int)mouseCoordinates.X,
                (int)mouseCoordinates.Y,
                tileGrab.Bounds.Width,
                tileGrab.Bounds.Height)
            : null;

        switch (MapEditorViewModel.SelectedTool)
        {
            case ToolType.Draw:
            {
                if ((drawBounds?.Contains(currentPoint) ?? false) && tileGrab!.HasBackgroundTiles)
                {
                    var tileGrabX = (int)(currentPoint.X - mouseCoordinates.X);
                    var tileGrabY = (int)(currentPoint.Y - mouseCoordinates.Y);

                    tileViewModel = tileGrab.BackgroundTilesView[tileGrabX, tileGrabY];
                }

                break;
            }
            case ToolType.Select:
            {
                if (Mouse.LeftButton == MouseButtonState.Pressed)
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
                if (Mouse.LeftButton == MouseButtonState.Pressed)
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

    private void RenderForeground(
        SKPoint mouseCoordinates,
        ListSegment2D<TileViewModel> leftForegroundTiles,
        ListSegment2D<TileViewModel> rightForegroundTiles)
    {
        if (!ViewModel.ForegroundChangePending)
            return;

        var width = (ViewModel.Bounds.Width + ViewModel.Bounds.Height + 1) * DALIB_CONSTANTS.HALF_TILE_WIDTH;
        var height = (ViewModel.Bounds.Width + ViewModel.Bounds.Height + 1) * DALIB_CONSTANTS.HALF_TILE_HEIGHT + FOREGROUND_PADDING;

        using var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);

        var fgInitialDrawX = (ViewModel.Bounds.Height - 1) * DALIB_CONSTANTS.HALF_TILE_WIDTH;
        var fgInitialDrawY = FOREGROUND_PADDING;
        var bounds = ViewModel.Bounds;

        for (var y = 0; y < bounds.Height; y++)
        {
            for (var x = 0; x < bounds.Width; x++)
            {
                var leftTileViewModel = leftForegroundTiles[x, y];
                var rightTileViewModel = rightForegroundTiles[x, y];
                var point = new Point(x, y);
                SKPaint? leftForegroundPaint = null;
                SKPaint? rightForegroundPaint = null;

                if (MapEditorViewModel.EditingLayerFlags.HasFlag(LayerFlags.LeftForeground))
                    HandleLeftForegroundToolHover(
                        point,
                        mouseCoordinates,
                        ref leftTileViewModel,
                        ref leftForegroundPaint);

                if (MapEditorViewModel.EditingLayerFlags.HasFlag(LayerFlags.RightForeground))
                    HandleRightForegroundToolHover(
                        point,
                        mouseCoordinates,
                        ref rightTileViewModel,
                        ref rightForegroundPaint);

                var leftCurrentFrame = leftTileViewModel.CurrentFrame;
                var rightCurrentFrame = rightTileViewModel.CurrentFrame;

                if (MapEditorViewModel.ShowLeftForeground && leftCurrentFrame is not null && ((leftTileViewModel.TileId % 10000) > 1))
                    canvas.DrawImage(
                        leftCurrentFrame,
                        fgInitialDrawX + x * DALIB_CONSTANTS.HALF_TILE_WIDTH,
                        fgInitialDrawY
                        + (x + 1) * DALIB_CONSTANTS.HALF_TILE_HEIGHT
                        - leftCurrentFrame.Height
                        + DALIB_CONSTANTS.HALF_TILE_HEIGHT,
                        leftForegroundPaint);

                if (MapEditorViewModel.ShowRightForeground && rightCurrentFrame is not null && ((rightTileViewModel.TileId % 10000) > 1))
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

        ForegroundImage?.Dispose();
        ForegroundImage = SKImage.FromBitmap(bitmap);
    }

    private void HandleLeftForegroundToolHover(
        Point currentPoint,
        SKPoint mouseCoordinates,
        ref TileViewModel currentFrame,
        ref SKPaint? paint)
    {
        if (mouseCoordinates == new SKPoint(-1, -1))
            return;

        var tileGrab = TileGrab;

        var selectionBounds = tileGrab?.SelectionStart is not null ? tileGrab.Bounds : null;

        var drawBounds = tileGrab is not null
            ? new Rectangle(
                (int)mouseCoordinates.X,
                (int)mouseCoordinates.Y,
                tileGrab.Bounds.Width,
                tileGrab.Bounds.Height)
            : null;

        switch (MapEditorViewModel.SelectedTool)
        {
            case ToolType.Draw:
            {
                if ((drawBounds?.Contains(currentPoint) ?? false) && tileGrab!.HasLeftForegroundTiles)
                {
                    var tileGrabX = (int)(currentPoint.X - mouseCoordinates.X);
                    var tileGrabY = (int)(currentPoint.Y - mouseCoordinates.Y);

                    currentFrame = tileGrab.LeftForegroundTilesView[tileGrabX, tileGrabY];
                }

                break;
            }
            case ToolType.Select:
            {
                if (Mouse.LeftButton == MouseButtonState.Pressed)
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
                if (Mouse.LeftButton == MouseButtonState.Pressed)
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
        ref TileViewModel currentFrame,
        ref SKPaint? paint)
    {
        if (mouseCoordinates == new SKPoint(-1, -1))
            return;

        var tileGrab = TileGrab;

        var selectionBounds = tileGrab?.SelectionStart is not null ? tileGrab.Bounds : null;

        var drawBounds = tileGrab is not null
            ? new Rectangle(
                (int)mouseCoordinates.X,
                (int)mouseCoordinates.Y,
                tileGrab.Bounds.Width,
                tileGrab.Bounds.Height)
            : null;

        switch (MapEditorViewModel.SelectedTool)
        {
            case ToolType.Draw:
            {
                if ((drawBounds?.Contains(currentPoint) ?? false) && tileGrab!.HasRightForegroundTiles)
                {
                    var tileGrabX = (int)(currentPoint.X - mouseCoordinates.X);
                    var tileGrabY = (int)(currentPoint.Y - mouseCoordinates.Y);

                    currentFrame = tileGrab.RightForegroundTilesView[tileGrabX, tileGrabY];
                }

                break;
            }
            case ToolType.Select:
            {
                if (Mouse.LeftButton == MouseButtonState.Pressed)
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
                if (Mouse.LeftButton == MouseButtonState.Pressed)
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
            if (TileGrab is not null)
                TileGrab.PropertyChanged -= TileGrabOnPropertyChanged;

            TileGrab = MapEditorViewModel.TileGrab;

            if (TileGrab is not null)
                TileGrab.PropertyChanged += TileGrabOnPropertyChanged;
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

        if ((e.PropertyName.EqualsI(nameof(MapViewerViewModel.BackgroundChangePending)) && ViewModel.BackgroundChangePending)
            || (e.PropertyName.EqualsI(nameof(MapViewerViewModel.ForegroundChangePending)) && ViewModel.ForegroundChangePending))
            Element.Redraw();
    }

    private void MapViewerControl_OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        BackgroundImage = null;
        ForegroundImage = null;

        if (e.OldValue is MapViewerViewModel oldViewModel)
        {
            oldViewModel.PropertyChanged -= ViewModelOnPropertyChanged;
            oldViewModel.Control = null;
            oldViewModel.ViwerTransform = Element.Matrix;
        }

        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        // removal from the collection will trigger this and set datacontext to null
        if (ViewModel is null)
            return;

        ViewModel.PropertyChanged += ViewModelOnPropertyChanged;
        ViewModel.Control = this;
        Element.Matrix = ViewModel.ViwerTransform;

        if (TileGrab is not null)
        {
            TileGrab.PropertyChanged -= TileGrabOnPropertyChanged;
            TileGrab = null;
        }

        TileGrab = MapEditorViewModel.TileGrab;

        if (TileGrab is not null)
            TileGrab.PropertyChanged += TileGrabOnPropertyChanged;

        ViewModel.BackgroundChangePending = true;
        ViewModel.ForegroundChangePending = true;
    }
    #endregion
}