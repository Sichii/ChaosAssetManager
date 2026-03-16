using System.Collections.Concurrent;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Chaos.Collections.Synchronized;
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

    //gpu textures must be disposed on the gl thread, so background render tasks
    //queue old texture images here for disposal during the next paint
    private readonly ConcurrentQueue<SKImage> PendingTextureDisposals = new();

    //tracks which chunks the previous mouse hover affected, so we can un-dirty them
    private readonly SynchronizedHashSet<MapChunk> PreviousHoverChunks = [];
    private readonly Lock RenderSync = new();

    private readonly LayerRenderState BackgroundRenderState = new();
    private readonly LayerRenderState ForegroundRenderState = new();
    private readonly LayerRenderState TabMapRenderState = new();
    private ChunkManager? ChunkMgr;
    private bool LatestLeftButtonPressed;
    private SKPoint LatestMapPoint = new(-1, -1);
    private SKRect LatestViewRect;

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
        ChunkMgr?.Dispose();
        ChunkMgr = null;

        MapEditorViewModel.PropertyChanged -= MapEditorViewModelOnPropertyChanged;

        if (HistoricalTileGrab is not null)
            HistoricalTileGrab.PropertyChanged -= TileGrabOnPropertyChanged;

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

    /// <summary>
    ///     Marks the chunks under the current draw-tool hover as dirty so animated
    ///     tile-grab tiles re-render even when the mouse is stationary
    /// </summary>
    public void InvalidateHoverChunks()
    {
        if (PreviousHoverChunks.Count == 0)
            return;

        var layers = MapEditorViewModel.EditingLayerFlags;

        foreach (var chunk in PreviousHoverChunks)
        {
            if (layers.HasFlag(LayerFlags.Background))
                chunk.BackgroundDirty = true;

            if (layers.HasFlag(LayerFlags.LeftForeground) || layers.HasFlag(LayerFlags.RightForeground))
            {
                chunk.ForegroundDirty = true;
                chunk.TabMapDirty = true;
            }
        }

        if (layers.HasFlag(LayerFlags.Background))
            ViewModel.BackgroundChangePending = true;

        if (layers.HasFlag(LayerFlags.LeftForeground) || layers.HasFlag(LayerFlags.RightForeground))
            ViewModel.ForegroundChangePending = true;
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
            //bounds may not be initialized yet
            if ((ViewModel.Bounds.Width <= 0) || (ViewModel.Bounds.Height <= 0))
                return new SKPoint(-1, -1);

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

        //mark all chunks dirty after mouse up to ensure clean state
        ChunkMgr?.MarkAllDirty(MapEditorViewModel.EditingLayerFlags);

        if (MapEditorViewModel.EditingLayerFlags.HasFlag(LayerFlags.Background))
            ViewModel.BackgroundChangePending = true;

        if (MapEditorViewModel.EditingLayerFlags.HasFlag(LayerFlags.LeftForeground)
            || MapEditorViewModel.EditingLayerFlags.HasFlag(LayerFlags.RightForeground))
            ViewModel.ForegroundChangePending = true;
    }

    private void MarkHoverChunksDirty(SKPoint tileCoordinates, LayerFlags layers)
    {
        if (ChunkMgr is null)
            return;

        //mark previously hovered chunks dirty to clear old hover effect
        foreach (var prevChunk in PreviousHoverChunks)
        {
            if (layers.HasFlag(LayerFlags.Background))
                prevChunk.BackgroundDirty = true;

            if (layers.HasFlag(LayerFlags.LeftForeground) || layers.HasFlag(LayerFlags.RightForeground))
            {
                prevChunk.ForegroundDirty = true;
                prevChunk.TabMapDirty = true;
            }
        }

        PreviousHoverChunks.Clear();

        //mark current hover chunks dirty
        var tileGrab = TileGrab;
        var startX = (int)tileCoordinates.X;
        var startY = (int)tileCoordinates.Y;
        var grabWidth = tileGrab?.Bounds.Width ?? 1;
        var grabHeight = tileGrab?.Bounds.Height ?? 1;

        ChunkMgr.MarkRangeDirty(
            startX,
            startY,
            grabWidth,
            grabHeight,
            layers);

        //track which chunks are now hovered
        var startCx = Math.Max(0, startX / ChunkManager.CHUNK_SIZE);
        var startCy = Math.Max(0, startY / ChunkManager.CHUNK_SIZE);
        var endCx = Math.Min(ChunkMgr.ChunksWide - 1, (startX + grabWidth - 1) / ChunkManager.CHUNK_SIZE);
        var endCy = Math.Min(ChunkMgr.ChunksHigh - 1, (startY + grabHeight - 1) / ChunkManager.CHUNK_SIZE);

        for (var cy = startCy; cy <= endCy; cy++)
            for (var cx = startCx; cx <= endCx; cx++)
                PreviousHoverChunks.Add(ChunkMgr.Chunks[cx, cy]);
    }

    private void ElementOnMouseMove(object sender, MouseEventArgs e)
    {
        //panning is handled entirely by the GPU via matrix transform
        if (Element.IsPanning)
            return;

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

                    var hoverLayers = LayerFlags.Background;

                    if (TileGrab.HasForegroundTiles)
                        hoverLayers |= LayerFlags.Foreground;

                    MarkHoverChunksDirty(tileCoordinates, hoverLayers);

                    if (TileGrab.HasBackgroundTiles)
                        ViewModel.BackgroundChangePending = true;

                    if (TileGrab.HasForegroundTiles)
                        ViewModel.ForegroundChangePending = true;
                }
            } else
            {
                var editLayers = MapEditorViewModel.EditingLayerFlags;
                MarkHoverChunksDirty(tileCoordinates, editLayers);

                if (editLayers.HasFlag(LayerFlags.Background))
                    ViewModel.BackgroundChangePending = true;

                if (editLayers.HasFlag(LayerFlags.LeftForeground) || editLayers.HasFlag(LayerFlags.RightForeground))
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
    private SKRect GetCurrentViewRect()
    {
        //if the element hasn't been laid out yet, return empty so we don't render all chunks;
        //OnPaint will have a valid viewport once layout completes
        if ((Element.ActualWidth == 0) || (Element.ActualHeight == 0))
            return SKRect.Empty;

        var inverted = Element.Matrix.Invert();
        var dpiScale = (float)DpiHelper.GetDpiScaleFactor();
        var topLeft = inverted.MapPoint(new SKPoint(0, 0));
        var bottomRight = inverted.MapPoint(new SKPoint((float)Element.ActualWidth * dpiScale, (float)Element.ActualHeight * dpiScale));

        return SKRect.Create(
            topLeft.X,
            topLeft.Y,
            bottomRight.X - topLeft.X,
            bottomRight.Y - topLeft.Y);
    }

    private void ElementOnPaint(object? sender, SKPaintGLSurfaceEventArgs e)
    {
        using var rendersync = RenderSync.EnterScope();

        //dispose gpu textures that were replaced by background render tasks
        while (PendingTextureDisposals.TryDequeue(out var oldTexture))
            oldTexture.Dispose();

        var canvas = e.Surface.Canvas;

        if (ChunkMgr is null)
            return;

        var grContext = (GRContext)e.Surface.Context;
        var viewRect = GetCurrentViewRect();

        //if viewport changed (pan/zoom/initial layout), check for dirty chunks
        //that need rendering (e.g. initial load before layout, or panning to new area)
        if (viewRect != LatestViewRect)
        {
            LatestViewRect = viewRect;

            var hasDirtyBg = false;
            var hasDirtyFg = false;
            var hasDirtyTab = false;

            foreach (var chunk in ChunkMgr.GetVisibleChunks(viewRect))
            {
                if (chunk.BackgroundDirty)
                    hasDirtyBg = true;

                if (chunk.ForegroundDirty)
                    hasDirtyFg = true;

                if (chunk.TabMapDirty)
                    hasDirtyTab = true;
            }

            if (hasDirtyBg)
                ViewModel.BackgroundChangePending = true;

            if (hasDirtyFg)
                ViewModel.ForegroundChangePending = true;

            if (hasDirtyTab)
                ViewModel.TabMapChangePending = true;
        }

        var visibleChunks = ChunkMgr.GetVisibleChunks(viewRect);

        //promote raster-backed chunk images to gpu textures
        //so subsequent paints don't re-upload from cpu
        if (grContext is not null)
            foreach (var chunk in visibleChunks)
                PromoteChunkToTexture(chunk, grContext);

        using var paint = new SKPaint();
        paint.IsAntialias = false;

        //draw background chunks
        foreach (var chunk in visibleChunks)
            if (chunk.BackgroundImage is not null)
                canvas.DrawImage(
                    chunk.BackgroundImage,
                    chunk.PixelBounds.Left,
                    chunk.PixelBounds.Top,
                    paint);

        //draw foreground chunks
        foreach (var chunk in visibleChunks)
            if (chunk.ForegroundImage is not null)
                canvas.DrawImage(
                    chunk.ForegroundImage,
                    chunk.ForegroundPixelBounds.Left,
                    chunk.ForegroundPixelBounds.Top,
                    paint);

        //draw screen-blend foreground chunks (sotp transparent tiles)
        using var screenBlendPaint = new SKPaint();
        screenBlendPaint.BlendMode = SKBlendMode.Screen;
        screenBlendPaint.IsAntialias = false;

        foreach (var chunk in visibleChunks)
            if (chunk.ScreenBlendForegroundImage is not null)
                canvas.DrawImage(
                    chunk.ScreenBlendForegroundImage,
                    chunk.ForegroundPixelBounds.Left,
                    chunk.ForegroundPixelBounds.Top,
                    screenBlendPaint);

        if (MapEditorViewModel.ShowGrid)
        {
            var mapW = ViewModel.Bounds.Width;
            var mapH = ViewModel.Bounds.Height;
            var hw = DALIB_CONSTANTS.HALF_TILE_WIDTH;
            var hh = DALIB_CONSTANTS.HALF_TILE_HEIGHT;

            //clip to the diamond boundary of the map using a staircase path
            //that matches the tile outline's 2:1 pixel pattern
            using var clipPath = RenderUtil.CreateIsometricDiamondPath(mapW, mapH, FOREGROUND_PADDING);

            canvas.Save();
            canvas.ClipPath(clipPath);

            //translate so the grid shader aligns with tile centers
            var gridOriginX = mapH * hw;
            var gridOriginY = FOREGROUND_PADDING + hh;

            canvas.Translate(gridOriginX, gridOriginY);

            RenderUtil.DrawIsometricGrid(
                canvas,
                e.Info.Width,
                e.Info.Height,
                1,
                SKBlendMode.Difference);

            canvas.Restore();
        }

        //draw tabmap chunks
        foreach (var chunk in visibleChunks)
            if (chunk.TabMapImage is not null)
                canvas.DrawImage(
                    chunk.TabMapImage,
                    chunk.PixelBounds.Left,
                    chunk.PixelBounds.Top,
                    paint);
    }

    private static void PromoteChunkToTexture(MapChunk chunk, GRContext grContext)
    {
        if (chunk.BackgroundImage is { IsTextureBacked: false } rasterBg)
        {
            chunk.BackgroundImage = rasterBg.ToTextureImage(grContext);
            rasterBg.Dispose();
        }

        if (chunk.ForegroundImage is { IsTextureBacked: false } rasterFg)
        {
            chunk.ForegroundImage = rasterFg.ToTextureImage(grContext);
            rasterFg.Dispose();
        }

        if (chunk.ScreenBlendForegroundImage is { IsTextureBacked: false } rasterScreenFg)
        {
            chunk.ScreenBlendForegroundImage = rasterScreenFg.ToTextureImage(grContext);
            rasterScreenFg.Dispose();
        }

        if (chunk.TabMapImage is { IsTextureBacked: false } rasterTab)
        {
            chunk.TabMapImage = rasterTab.ToTextureImage(grContext);
            rasterTab.Dispose();
        }
    }

    private void RenderTabMapChunk(MapChunk chunk, SKPoint mouseCoordinates, bool leftButtonPressed)
    {
        var bounds = ViewModel.Bounds;
        var lfgTiles = ViewModel.LeftForegroundTilesView;
        var rfgTiles = ViewModel.RightForegroundTilesView;
        var tglfgTiles = new ListSegment2D<TileViewModel>();
        var tgrfgTiles = new ListSegment2D<TileViewModel>();
        var isEditingLeftForeground = MapEditorViewModel.EditingLayerFlags.HasFlag(LayerFlags.LeftForeground);
        var isEditingRightForeground = MapEditorViewModel.EditingLayerFlags.HasFlag(LayerFlags.RightForeground);
        var tileGrab = TileGrab;

        if (tileGrab is not null)
        {
            tglfgTiles = tileGrab.LeftForegroundTilesView;
            tgrfgTiles = tileGrab.RightForegroundTilesView;
        }

        var chunkBounds = chunk.PixelBounds;
        var bitmapWidth = chunkBounds.Width;
        var bitmapHeight = chunkBounds.Height;

        if ((bitmapWidth <= 0) || (bitmapHeight <= 0))
            return;

        using var bitmap = new SKBitmap(new SKImageInfo(bitmapWidth, bitmapHeight));
        using var canvas = new SKCanvas(bitmap);

        //offset so tiles draw at local chunk coords
        canvas.Translate(-chunkBounds.Left, -chunkBounds.Top);

        //build wall map for the chunk region + 1 tile border for neighbor checks
        var wallStartX = Math.Max(0, chunk.TileBounds.Left - 1);
        var wallStartY = Math.Max(0, chunk.TileBounds.Top - 1);
        var wallEndX = Math.Min(bounds.Width - 1, chunk.TileBounds.Right + 1);
        var wallEndY = Math.Min(bounds.Height - 1, chunk.TileBounds.Bottom + 1);
        var wallW = wallEndX - wallStartX + 1;
        var wallH = wallEndY - wallStartY + 1;
        bool[,]? wallMap = null;

        if (MapEditorViewModel.ShowTabMap)
        {
            wallMap = new bool[wallW, wallH];

            for (var y = wallStartY; y <= wallEndY; y++)
            {
                for (var x = wallStartX; x <= wallEndX; x++)
                {
                    var leftTile = lfgTiles[x, y];
                    var rightTile = rfgTiles[x, y];
                    var point = new Point(x, y);
                    SKPaint? unusedPaint = null;

                    if (isEditingLeftForeground)
                        HandleToolHover(
                            point,
                            mouseCoordinates,
                            tglfgTiles,
                            static tg => tg.HasLeftForegroundTiles,
                            ref leftTile,
                            ref unusedPaint,
                            leftButtonPressed);

                    if (isEditingRightForeground)
                        HandleToolHover(
                            point,
                            mouseCoordinates,
                            tgrfgTiles,
                            static tg => tg.HasRightForegroundTiles,
                            ref rightTile,
                            ref unusedPaint,
                            leftButtonPressed);

                    wallMap[x - wallStartX, y - wallStartY] = MapEditorRenderUtil.IsWall(leftTile.TileId)
                                                              || MapEditorRenderUtil.IsWall(rightTile.TileId);
                }
            }
        }

        for (var y = chunk.TileBounds.Top; y <= chunk.TileBounds.Bottom; y++)
        {
            for (var x = chunk.TileBounds.Left; x <= chunk.TileBounds.Right; x++)
            {
                var leftTileViewModel = lfgTiles[x, y];
                var rightTileViewModel = rfgTiles[x, y];
                var point = new Point(x, y);
                SKPaint? leftForegroundPaint = null;
                SKPaint? rightForegroundPaint = null;

                if (isEditingLeftForeground)
                    HandleToolHover(
                        point,
                        mouseCoordinates,
                        tglfgTiles,
                        static tg => tg.HasLeftForegroundTiles,
                        ref leftTileViewModel,
                        ref leftForegroundPaint,
                        leftButtonPressed);

                if (isEditingRightForeground)
                    HandleToolHover(
                        point,
                        mouseCoordinates,
                        tgrfgTiles,
                        static tg => tg.HasRightForegroundTiles,
                        ref rightTileViewModel,
                        ref rightForegroundPaint,
                        leftButtonPressed);

                var wx = x - wallStartX;
                var wy = y - wallStartY;
                var isWall = wallMap is not null && wallMap[wx, wy];

                var (fgDrawX, fgDrawY) = MapEditorRenderUtil.GetTileDrawPosition(x, y, bounds.Height);

                if (MapEditorViewModel.ShowTabMap && isWall)
                {
                    canvas.DrawImage(MapEditorRenderUtil.RenderTabWall(), fgDrawX, fgDrawY);

                    var drawTopRight = (y == 0) || !wallMap![wx, wy - 1];
                    var drawBottomRight = (x == (bounds.Width - 1)) || !wallMap![wx + 1, wy];
                    var drawBottomLeft = (y == (bounds.Height - 1)) || !wallMap![wx, wy + 1];
                    var drawTopLeft = (x == 0) || !wallMap![wx - 1, wy];

                    if (drawTopRight || drawBottomRight || drawBottomLeft || drawTopLeft)
                        MapEditorRenderUtil.DrawTileOutlineEdges(
                            bitmap,
                            fgDrawX - chunkBounds.Left,
                            fgDrawY - chunkBounds.Top,
                            SKColors.Snow,
                            drawTopRight,
                            drawBottomRight,
                            drawBottomLeft,
                            drawTopLeft);
                }

                if (MapEditorViewModel.ShowForegroundGrid)
                {
                    //per-tile seed for deterministic colors matching the original sequential approach
                    var tileSeed = 8675309 + y * bounds.Width + x;
                    var tileRandom = new Random(tileSeed);
                    var randomColor = SKColorExtensions.GetRandomVividColor(tileRandom);
                    var randomPhase = tileRandom.Next(6);

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

                    var gridY = fgDrawY + DALIB_CONSTANTS.TILE_HEIGHT;

                    if (leftTileViewModel.TileId.IsRenderedTileIndex() && leftTileViewModel.CurrentFrame is { } lframe)
                    {
                        var leftRect = new SKRect(
                            fgDrawX,
                            gridY - lframe.Height,
                            fgDrawX + lframe.Width,
                            gridY);

                        canvas.DrawRect(leftRect, fgGridPaint);
                    }

                    var gridX = fgDrawX + DALIB_CONSTANTS.HALF_TILE_WIDTH;

                    if (rightTileViewModel.TileId.IsRenderedTileIndex() && rightTileViewModel.CurrentFrame is { } rframe)
                    {
                        var rightRect = new SKRect(
                            gridX,
                            gridY - rframe.Height,
                            gridX + rframe.Width,
                            gridY);

                        canvas.DrawRect(rightRect, fgGridPaint);
                    }
                }
            }
        }

        SKImage? oldImage;
        var image = SKImage.FromBitmap(bitmap);

        using (RenderSync.EnterScope())
        {
            oldImage = chunk.TabMapImage;
            chunk.TabMapImage = image;
        }

        if (oldImage is not null)
            PendingTextureDisposals.Enqueue(oldImage);
    }

    private void RenderBackgroundChunk(MapChunk chunk, SKPoint mouseCoordinates, bool leftButtonPressed)
    {
        var chunkBounds = chunk.PixelBounds;
        var bitmapWidth = chunkBounds.Width;
        var bitmapHeight = chunkBounds.Height;

        if ((bitmapWidth <= 0) || (bitmapHeight <= 0))
            return;

        using var bitmap = new SKBitmap(new SKImageInfo(bitmapWidth, bitmapHeight));
        using var canvas = new SKCanvas(bitmap);

        if (MapEditorViewModel.ShowBackground)
        {
            var bgTiles = ViewModel.BackgroundTilesView;
            var bounds = ViewModel.Bounds;
            var tgbgTiles = new ListSegment2D<TileViewModel>();
            var isEditingBackground = MapEditorViewModel.EditingLayerFlags.HasFlag(LayerFlags.Background);
            var tileGrab = TileGrab;

            if (tileGrab is not null)
                tgbgTiles = tileGrab.BackgroundTilesView;

            //offset so tiles draw at local chunk coords
            canvas.Translate(-chunkBounds.Left, -chunkBounds.Top);

            for (var y = chunk.TileBounds.Top; y <= chunk.TileBounds.Bottom; y++)
            {
                for (var x = chunk.TileBounds.Left; x <= chunk.TileBounds.Right; x++)
                {
                    var point = new Point(x, y);
                    var tileViewModel = bgTiles[x, y];
                    SKPaint? paint = null;

                    if (isEditingBackground)
                        HandleToolHover(
                            point,
                            mouseCoordinates,
                            tgbgTiles,
                            static tg => tg.HasBackgroundTiles,
                            ref tileViewModel,
                            ref paint,
                            leftButtonPressed);

                    var currentFrame = tileViewModel.CurrentFrame;

                    //pixel position formula
                    var (drawX, drawY) = MapEditorRenderUtil.GetTileDrawPosition(x, y, bounds.Height);

                    canvas.DrawImage(
                        currentFrame,
                        drawX,
                        drawY,
                        paint);

                    paint?.Dispose();
                }
            }
        }

        SKImage? oldImage;
        var image = SKImage.FromBitmap(bitmap);

        using (RenderSync.EnterScope())
        {
            oldImage = chunk.BackgroundImage;
            chunk.BackgroundImage = image;
        }

        if (oldImage is not null)
            PendingTextureDisposals.Enqueue(oldImage);
    }

    public SKImage RenderMapImage()
    {
        using var rendersync = RenderSync.EnterScope();

        if (ChunkMgr is null)
            return SKImage.Create(new SKImageInfo(1, 1));

        var dimensions = ImageHelper.CalculateRenderedImageSize(
            ViewModel.BackgroundTilesView,
            ViewModel.LeftForegroundTilesView,
            ViewModel.RightForegroundTilesView);
        using var bitmap = new SKBitmap(dimensions.Width, dimensions.Height + FOREGROUND_PADDING);
        using var canvas = new SKCanvas(bitmap);

        //composite all chunks for export
        using var exportScreenBlendPaint = new SKPaint();
        exportScreenBlendPaint.BlendMode = SKBlendMode.Screen;

        foreach (var chunk in ChunkMgr.GetAllChunks())
        {
            if (chunk.BackgroundImage is not null)
                canvas.DrawImage(chunk.BackgroundImage, chunk.PixelBounds.Left, chunk.PixelBounds.Top);

            if (chunk.ForegroundImage is not null)
                canvas.DrawImage(chunk.ForegroundImage, chunk.ForegroundPixelBounds.Left, chunk.ForegroundPixelBounds.Top);

            if (chunk.ScreenBlendForegroundImage is not null)
                canvas.DrawImage(
                    chunk.ScreenBlendForegroundImage,
                    chunk.ForegroundPixelBounds.Left,
                    chunk.ForegroundPixelBounds.Top,
                    exportScreenBlendPaint);

            if (chunk.TabMapImage is not null)
                canvas.DrawImage(chunk.TabMapImage, chunk.PixelBounds.Left, chunk.PixelBounds.Top);
        }

        canvas.Flush();

        return SKImage.FromBitmap(bitmap);
    }

    private void HandleToolHover(
        Point currentPoint,
        SKPoint mouseCoordinates,
        ListSegment2D<TileViewModel> grabTiles,
        Func<TileGrabViewModel, bool> hasGrabTiles,
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
                if (drawBounds.Contains(currentPoint) && hasGrabTiles(tileGrab!))
                {
                    var tileGrabX = (int)(currentPoint.X - mouseCoordinates.X);
                    var tileGrabY = (int)(currentPoint.Y - mouseCoordinates.Y);

                    // if the tilegrab is empty, dont overwrite what's already there
                    if (grabTiles[tileGrabX, tileGrabY].TileId == 0)
                        return;

                    tileViewModel = grabTiles[tileGrabX, tileGrabY];
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

    private void RenderForegroundChunk(MapChunk chunk, SKPoint mouseCoordinates, bool leftButtonPressed)
    {
        var fgBounds = chunk.ForegroundPixelBounds;
        var bitmapWidth = fgBounds.Width;
        var bitmapHeight = fgBounds.Height;

        if ((bitmapWidth <= 0) || (bitmapHeight <= 0))
            return;

        using var bitmap = new SKBitmap(new SKImageInfo(bitmapWidth, bitmapHeight));
        using var canvas = new SKCanvas(bitmap);

        SKBitmap? screenBlendBitmap = null;
        SKCanvas? screenBlendCanvas = null;
        var hasScreenBlendTiles = false;

        var lfgTiles = ViewModel.LeftForegroundTilesView;
        var rfgTiles = ViewModel.RightForegroundTilesView;
        var bounds = ViewModel.Bounds;
        var tglfgTiles = new ListSegment2D<TileViewModel>();
        var tgrfgTiles = new ListSegment2D<TileViewModel>();
        var isEditingLeftForeground = MapEditorViewModel.EditingLayerFlags.HasFlag(LayerFlags.LeftForeground);
        var isEditingRightForeground = MapEditorViewModel.EditingLayerFlags.HasFlag(LayerFlags.RightForeground);
        var tileGrab = TileGrab;

        if (tileGrab is not null)
        {
            tglfgTiles = tileGrab.LeftForegroundTilesView;
            tgrfgTiles = tileGrab.RightForegroundTilesView;
        }

        //offset so tiles draw at local chunk coords
        canvas.Translate(-fgBounds.Left, -fgBounds.Top);

        for (var y = chunk.TileBounds.Top; y <= chunk.TileBounds.Bottom; y++)
        {
            for (var x = chunk.TileBounds.Left; x <= chunk.TileBounds.Right; x++)
            {
                var leftTileViewModel = lfgTiles[x, y];
                var rightTileViewModel = rfgTiles[x, y];
                var point = new Point(x, y);
                SKPaint? leftForegroundPaint = null;
                SKPaint? rightForegroundPaint = null;

                if (isEditingLeftForeground)
                    HandleToolHover(
                        point,
                        mouseCoordinates,
                        tglfgTiles,
                        static tg => tg.HasLeftForegroundTiles,
                        ref leftTileViewModel,
                        ref leftForegroundPaint,
                        leftButtonPressed);

                if (isEditingRightForeground)
                    HandleToolHover(
                        point,
                        mouseCoordinates,
                        tgrfgTiles,
                        static tg => tg.HasRightForegroundTiles,
                        ref rightTileViewModel,
                        ref rightForegroundPaint,
                        leftButtonPressed);

                var leftCurrentFrame = leftTileViewModel.CurrentFrame;
                var rightCurrentFrame = rightTileViewModel.CurrentFrame;

                //pixel position formula
                var (fgDrawX, fgDrawY) = MapEditorRenderUtil.GetTileDrawPosition(x, y, bounds.Height);

                if (MapEditorViewModel.ShowLeftForeground && leftCurrentFrame is not null && leftTileViewModel.TileId.IsRenderedTileIndex())
                    GetTargetCanvas(leftTileViewModel.TileId)
                        .DrawImage(
                            leftCurrentFrame,
                            fgDrawX,
                            fgDrawY + DALIB_CONSTANTS.HALF_TILE_HEIGHT - leftCurrentFrame.Height + DALIB_CONSTANTS.HALF_TILE_HEIGHT,
                            leftForegroundPaint);

                if (MapEditorViewModel.ShowRightForeground
                    && rightCurrentFrame is not null
                    && rightTileViewModel.TileId.IsRenderedTileIndex())
                    GetTargetCanvas(rightTileViewModel.TileId)
                        .DrawImage(
                            rightCurrentFrame,
                            fgDrawX + DALIB_CONSTANTS.HALF_TILE_WIDTH,
                            fgDrawY + DALIB_CONSTANTS.HALF_TILE_HEIGHT - rightCurrentFrame.Height + DALIB_CONSTANTS.HALF_TILE_HEIGHT,
                            rightForegroundPaint);

                continue;

                SKCanvas GetTargetCanvas(int tileId)
                {
                    if (!MapEditorRenderUtil.IsTransparent(tileId))
                        return canvas;

                    if (screenBlendBitmap is null)
                    {
                        screenBlendBitmap = new SKBitmap(new SKImageInfo(bitmapWidth, bitmapHeight));
                        screenBlendCanvas = new SKCanvas(screenBlendBitmap);
                        screenBlendCanvas.Translate(-fgBounds.Left, -fgBounds.Top);
                    }

                    hasScreenBlendTiles = true;

                    return screenBlendCanvas!;
                }
            }
        }

        SKImage? oldImage;
        SKImage? oldScreenBlendImage;
        var image = SKImage.FromBitmap(bitmap);
        var screenBlendImage = hasScreenBlendTiles ? SKImage.FromBitmap(screenBlendBitmap!) : null;

        screenBlendCanvas?.Dispose();
        screenBlendBitmap?.Dispose();

        using (RenderSync.EnterScope())
        {
            oldImage = chunk.ForegroundImage;
            oldScreenBlendImage = chunk.ScreenBlendForegroundImage;
            chunk.ForegroundImage = image;
            chunk.ScreenBlendForegroundImage = screenBlendImage;
        }

        if (oldImage is not null)
            PendingTextureDisposals.Enqueue(oldImage);

        if (oldScreenBlendImage is not null)
            PendingTextureDisposals.Enqueue(oldScreenBlendImage);
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
        {
            ChunkMgr?.MarkAllDirty(LayerFlags.Background);
            ViewModel.BackgroundChangePending = true;
        }

        if (e.PropertyName.EqualsI(nameof(TileGrab.RawLeftForegroundTiles))
            || e.PropertyName.EqualsI(nameof(TileGrab.RawRightForegroundTiles)))
        {
            ChunkMgr?.MarkAllDirty(LayerFlags.Foreground);
            ViewModel.ForegroundChangePending = true;
        }
    }

    private void ViewModelOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName))
            return;

        //when the map dimensions change, recreate the chunk manager before any render
        if (e.PropertyName.EqualsI(nameof(MapViewerViewModel.Bounds)))
        {
            RecreateChunkManager();

            return;
        }

        // Calculate mouse state once for all renders
        var mousePoint = Element.GetMousePoint();
        var leftButtonPressed = Mouse.LeftButton == MouseButtonState.Pressed;
        var mapPoint = mousePoint.HasValue ? ConvertMouseToTileCoordinates(mousePoint.Value) : new SKPoint(-1, -1);

        //update shared state so background render loops always use the latest coordinates
        LatestMapPoint = mapPoint;
        LatestLeftButtonPressed = leftButtonPressed;
        LatestViewRect = GetCurrentViewRect();

        if (e.PropertyName.EqualsI(nameof(MapViewerViewModel.BackgroundChangePending)) && ViewModel.BackgroundChangePending)
            QueueLayerRender(
                BackgroundRenderState,
                () => ViewModel.BackgroundChangePending = false,
                viewRect => ChunkMgr!.GetDirtyVisibleBackgroundChunks(viewRect),
                chunk => chunk.BackgroundDirty = false,
                RenderBackgroundChunk);

        if (e.PropertyName.EqualsI(nameof(MapViewerViewModel.ForegroundChangePending)) && ViewModel.ForegroundChangePending)
            QueueLayerRender(
                ForegroundRenderState,
                () => ViewModel.ForegroundChangePending = false,
                viewRect => ChunkMgr!.GetDirtyVisibleForegroundChunks(viewRect),
                chunk => chunk.ForegroundDirty = false,
                RenderForegroundChunk);

        if (e.PropertyName.EqualsI(nameof(MapViewerViewModel.TabMapChangePending)) && ViewModel.TabMapChangePending)
            QueueLayerRender(
                TabMapRenderState,
                () => ViewModel.TabMapChangePending = false,
                viewRect => ChunkMgr!.GetDirtyVisibleTabMapChunks(viewRect),
                chunk => chunk.TabMapDirty = false,
                RenderTabMapChunk);
    }

    private void RecreateChunkManager()
    {
        ChunkMgr?.Dispose();
        ChunkMgr = null;
        ViewModel.ChunkMgr = null;

        if ((ViewModel.Bounds.Width <= 0) || (ViewModel.Bounds.Height <= 0))
            return;

        ChunkMgr = new ChunkManager(ViewModel.Bounds.Width, ViewModel.Bounds.Height);
        ViewModel.ChunkMgr = ChunkMgr;
        ChunkMgr.MarkAllDirty(LayerFlags.All);

        //unsubscribe during refresh to prevent premature render queueing
        //(Refresh triggers ObservingCollection.CollectionChanged which sets pending flags)
        ViewModel.PropertyChanged -= ViewModelOnPropertyChanged;

        ViewModel.Refresh();

        //clear pending flags that Refresh() set via ObservingCollection.CollectionChanged
        //so that the explicit sets below actually fire PropertyChanged
        ViewModel.BackgroundChangePending = false;
        ViewModel.ForegroundChangePending = false;
        ViewModel.TabMapChangePending = false;

        //re-subscribe, then trigger renders with all tiles fully initialized
        ViewModel.PropertyChanged += ViewModelOnPropertyChanged;

        ViewModel.BackgroundChangePending = true;
        ViewModel.ForegroundChangePending = true;
        ViewModel.TabMapChangePending = true;
    }

    private void QueueLayerRender(
        LayerRenderState state,
        Action clearPending,
        Func<SKRect, List<MapChunk>> getDirtyVisibleChunks,
        Action<MapChunk> clearDirtyFlag,
        Action<MapChunk, SKPoint, bool> renderChunk)
    {
        state.LastRequestedTime = DateTime.UtcNow;
        clearPending();

        if (state.RenderTask is { IsCompleted: false })
            return;

        state.RenderTask = Task.Run(() =>
        {
            if (ChunkMgr is null)
                return;

            while (state.LastRequestedTime > state.LastRenderTime)
            {
                state.LastRenderTime = DateTime.UtcNow;

                //read latest state so renders target the current viewport and hover position
                var viewRect = LatestViewRect;
                var mapPoint = LatestMapPoint;
                var leftButtonPressed = LatestLeftButtonPressed;

                var dirtyVisible = getDirtyVisibleChunks(viewRect);

                //if nothing is dirty but a render was requested, re-render all visible chunks
                //this handles external triggers (layer toggles, undo/redo) that don't mark chunks
                if (dirtyVisible.Count == 0)
                    dirtyVisible = ChunkMgr.GetVisibleChunks(viewRect);

                //clear all dirty flags upfront so new flags set during rendering survive
                foreach (var chunk in dirtyVisible)
                    clearDirtyFlag(chunk);

                foreach (var chunk in dirtyVisible)
                    renderChunk(chunk, mapPoint, leftButtonPressed);
            }

            Dispatcher.BeginInvoke(() =>
            {
                Element.Redraw();
            });
        });
    }

    private void MapViewerControl_OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        ChunkMgr?.Dispose();
        ChunkMgr = null;

        if (e.OldValue is MapViewerViewModel oldViewModel)
        {
            oldViewModel.PropertyChanged -= ViewModelOnPropertyChanged;
            oldViewModel.Control = null;
            oldViewModel.ViewerTransform = Element.Matrix;
            oldViewModel.ChunkMgr = null;
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

        ViewModel.Control = this;

        if (SetViewerTransform())
        {
            Element.Matrix = ViewModel.ViewerTransform!.Value;
            Element.Redraw();
        }

        if ((ViewModel.Bounds.Width <= 0) || (ViewModel.Bounds.Height <= 0))
        {
            ViewModel.PropertyChanged += ViewModelOnPropertyChanged;

            return;
        }

        //initialize chunk manager
        ChunkMgr = new ChunkManager(ViewModel.Bounds.Width, ViewModel.Bounds.Height);
        ViewModel.ChunkMgr = ChunkMgr;
        ChunkMgr.MarkAllDirty(LayerFlags.All);

        //refresh all tiles BEFORE subscribing to PropertyChanged
        //this prevents a race condition where QueueRender tasks start
        //before all tiles have been initialized (rendering empty chunks)
        ViewModel.Refresh();

        //clear pending flags that Refresh() set via ObservingCollection.CollectionChanged
        //so that the explicit sets below actually fire PropertyChanged
        ViewModel.BackgroundChangePending = false;
        ViewModel.ForegroundChangePending = false;
        ViewModel.TabMapChangePending = false;

        //now subscribe — the explicit pending flag sets below will trigger renders
        //with all tiles fully initialized
        ViewModel.PropertyChanged += ViewModelOnPropertyChanged;

        ViewModel.BackgroundChangePending = true;
        ViewModel.ForegroundChangePending = true;
        ViewModel.TabMapChangePending = true;
    }
    #endregion

    private sealed class LayerRenderState
    {
        public DateTime LastRenderTime = DateTime.MinValue;
        public DateTime LastRequestedTime = DateTime.MinValue;
        public Task? RenderTask;
    }
}