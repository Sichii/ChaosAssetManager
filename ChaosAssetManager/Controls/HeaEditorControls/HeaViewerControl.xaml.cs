using System.Collections.Concurrent;
using System.IO;
using System.Windows.Input;
using System.Windows.Threading;
using ChaosAssetManager.Controls.MapEditorControls;
using ChaosAssetManager.Controls.PreviewControls;
using ChaosAssetManager.Definitions;
using ChaosAssetManager.Helpers;
using ChaosAssetManager.Model;
using ChaosAssetManager.ViewModel;
using DALib.Data;
using DALib.Extensions;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using DALIB_CONSTANTS = DALib.Definitions.CONSTANTS;

// ReSharper disable ClassCanBeSealed.Global

namespace ChaosAssetManager.Controls.HeaEditorControls;

public partial class HeaViewerControl : IDisposable
{
    //the HEA light map has 2 screens of padding built into its dimensions:
    //  scanline_width  = 28*(tileW+tileH) + screenW*2
    //  scanline_height = 14*(tileW+tileH) + screenH*2
    //the map tiles are centered within this padded area, so the overlay must be
    //drawn offset by -1 screen resolution from the map's origin
    private const int OVERLAY_OFFSET_X = -640;
    private const int OVERLAY_OFFSET_Y = -480;

    //gpu textures must be disposed on the gl thread, so background render tasks
    //queue old texture images here for disposal during the next paint
    private readonly ConcurrentQueue<SKImage> PendingTextureDisposals = new();
    private readonly FixedSizeDeque<HeaActionContext> RedoStack = new(100);
    private readonly Lock RenderSync = new();

    //undo/redo
    private readonly FixedSizeDeque<HeaActionContext> UndoStack = new(100);

    //tile IDs read from the .map file, indexed [x, y]
    private int[,]? BgTileIds;
    private LightBrush? CachedBrush;
    private string? CachedBrushKey;

    private DarknessChunkManager? DarknessChunkManager;
    private int GridHeight;
    private int GridWidth;

    //reusable buffer for brush preview snapshot to avoid per-frame allocation
    private byte[,]? BrushPreviewSnapshot;

    private bool IsDrawing;
    private int[,]? LfgTileIds;
    private readonly System.Diagnostics.Stopwatch MapAnimationStopwatch = System.Diagnostics.Stopwatch.StartNew();
    private readonly HashSet<(int cx, int cy)> AnimatedBgChunks = [];
    private readonly HashSet<(int cx, int cy)> AnimatedFgChunks = [];
    private System.Windows.Threading.DispatcherTimer? MapAnimationTimer;
    private SKRect LatestMapViewRect;
    private Task? MapBackgroundRenderTask;
    private ChunkManager? MapChunkManager;
    private Task? MapForegroundRenderTask;
    private int MapTileHeight;
    private int[,]? RfgTileIds;
    private SKGLElementPlus? SkElement;
    private byte[,]? StrokeBeforeSnapshot;

    //tracks the affected region during a drag stroke for undo snapshot
    private int StrokeMinX,
                StrokeMinY,
                StrokeMaxX,
                StrokeMaxY;

    private int TileH;

    //map tile dimensions for coordinate display
    private int TileW;

    //the raw light grid: [scanlineCount, scanlineWidth], values 0-32
    public byte[,]? LightGrid { get; private set; }

    public HeaEditorViewModel? ViewModel { get; set; }

    public HeaViewerControl() => InitializeComponent();

    /// <inheritdoc />
    public void Dispose()
    {
        MapAnimationTimer?.Stop();
        MapAnimationTimer = null;

        DisposeSkElement();

        //drain pending gpu texture disposals
        while (PendingTextureDisposals.TryDequeue(out var oldTexture))
            oldTexture.Dispose();

        InvalidateBrushPreview();
        MapChunkManager?.Dispose();
        DarknessChunkManager?.Dispose();
        MapChunkManager = null;
        DarknessChunkManager = null;
        BgTileIds = null;
        LfgTileIds = null;
        RfgTileIds = null;
        LightGrid = null;
    }

    private void ApplyBrushAtMouse()
    {
        if (LightGrid is null || SkElement is null || ViewModel is null)
            return;

        var mousePoint = SkElement.GetMousePoint();

        if (mousePoint is null)
            return;

        var pt = mousePoint.Value;

        //convert from map-space to light grid-space (account for overlay offset)
        var px = (int)pt.X - OVERLAY_OFFSET_X;
        var py = (int)pt.Y - OVERLAY_OFFSET_Y;

        EnsureBrushCached();

        if (CachedBrush is null)
            return;

        // ReSharper disable once SwitchStatementMissingSomeEnumCasesNoDefault
        switch (ViewModel.SelectedTool)
        {
            case HeaToolType.Draw:
                CachedBrush.Stamp(LightGrid, px, py);

                break;
            case HeaToolType.Erase:
                CachedBrush.Erase(LightGrid, px, py);

                break;
        }

        //track the stroke bounds for undo snapshot
        var brushExtent = Math.Max(CachedBrush.Width, CachedBrush.Height) / 2 + 1;
        StrokeMinX = Math.Min(StrokeMinX, px - brushExtent);
        StrokeMinY = Math.Min(StrokeMinY, py - brushExtent);
        StrokeMaxX = Math.Max(StrokeMaxX, px + brushExtent);
        StrokeMaxY = Math.Max(StrokeMaxY, py + brushExtent);

        //only rebuild the affected region for performance
        RebuildDarknessOverlayRegion(px, py, brushExtent);
        Redraw();
    }

    public void CenterOnMap()
        =>

            //defer until after layout so ActualWidth/Height are available
            Dispatcher.BeginInvoke(
                DispatcherPriority.Loaded,
                () =>
                {
                    if (SkElement is null || (TileW == 0) || (TileH == 0))
                        return;

                    var dpiScale = (float)DpiHelper.GetDpiScaleFactor();
                    var viewW = (float)SkElement.ActualWidth * dpiScale;
                    var viewH = (float)SkElement.ActualHeight * dpiScale;

                    if ((viewW == 0) || (viewH == 0))
                        return;

                    var mapCenterX = (TileW + TileH) * DALIB_CONSTANTS.HALF_TILE_WIDTH / 2f;
                    var mapCenterY = (TileW + TileH) * DALIB_CONSTANTS.HALF_TILE_HEIGHT / 2f;

                    SkElement.Matrix = SKMatrix.CreateTranslation(viewW / 2f - mapCenterX, viewH / 2f - mapCenterY);
                    Focus();
                    Redraw();
                });

    private void CreateSkElement()
    {
        DisposeSkElement();

        SkElement = new SKGLElementPlus
        {
            DragButton = MouseButton.Right
        };

        SkElement.Paint += OnPaint;
        Content.Content = SkElement;

        SkElement.MouseLeftButtonDown += OnMouseLeftButtonDown;
        SkElement.MouseLeftButtonUp += OnMouseLeftButtonUp;
        SkElement.MouseMove += OnMouseMove;

        //hook PreviewMouseWheel on ourselves (the parent) so it intercepts
        //before the inner SKGLElement's MouseWheel zoom handler fires
        PreviewMouseWheel += OnPreviewMouseWheel;
    }

    private void DisposeSkElement()
    {
        if (SkElement is null)
            return;

        SkElement.Paint -= OnPaint;
        SkElement.MouseLeftButtonDown -= OnMouseLeftButtonDown;
        SkElement.MouseLeftButtonUp -= OnMouseLeftButtonUp;
        SkElement.MouseMove -= OnMouseMove;
        PreviewMouseWheel -= OnPreviewMouseWheel;

        SkElement.Dispose();
        SkElement = null;
    }

    /// <summary>
    ///     Previews the brush stamp by temporarily applying it to the light grid
    ///     and rebuilding the affected darkness chunks
    /// </summary>
    private void DrawBrushPreview(SKCanvas canvas)
    {
        if (SkElement is null || ViewModel is null || LightGrid is null
            || DarknessChunkManager is null || (ViewModel.SelectedTool != HeaToolType.Draw))
            return;

        var mousePoint = SkElement.GetMousePoint();

        if (mousePoint is null)
            return;

        var pt = mousePoint.Value;
        EnsureBrushCached();

        if (CachedBrush is null)
            return;

        //convert from map-space to light grid-space
        var px = (int)pt.X - OVERLAY_OFFSET_X;
        var py = (int)pt.Y - OVERLAY_OFFSET_Y;

        var brushExtent = Math.Max(CachedBrush.Width, CachedBrush.Height) / 2 + 1;
        var x0 = Math.Max(0, px - brushExtent);
        var y0 = Math.Max(0, py - brushExtent);
        var x1 = Math.Min(GridWidth - 1, px + brushExtent);
        var y1 = Math.Min(GridHeight - 1, py + brushExtent);

        if (x0 > x1 || y0 > y1)
            return;

        var w = x1 - x0 + 1;
        var h = y1 - y0 + 1;

        //snapshot the light grid region under the brush (reuse buffer)
        if (BrushPreviewSnapshot is null || (BrushPreviewSnapshot.GetLength(0) < h) || (BrushPreviewSnapshot.GetLength(1) < w))
            BrushPreviewSnapshot = new byte[h, w];

        for (var sy = 0; sy < h; sy++)
            for (var sx = 0; sx < w; sx++)
                BrushPreviewSnapshot[sy, sx] = LightGrid[y0 + sy, x0 + sx];

        //temporarily stamp into the light grid
        CachedBrush.Stamp(LightGrid, px, py);

        //rebuild affected darkness chunks
        var layer = CurrentDarknessLayer;

        DarknessChunkManager.MarkRangeDirty(x0, y0, x1, y1);
        DarknessChunkManager.RebuildDirtyChunks(LightGrid, layer.Alpha, layer.Color);

        //restore the light grid from snapshot
        for (var sy = 0; sy < h; sy++)
            for (var sx = 0; sx < w; sx++)
                LightGrid[y0 + sy, x0 + sx] = BrushPreviewSnapshot[sy, sx];

        //mark chunks dirty again so next frame (or actual stamp) rebuilds from real data
        DarknessChunkManager.MarkRangeDirty(x0, y0, x1, y1);
    }

    private void EnsureBrushCached()
    {
        if (ViewModel is null)
            return;

        var key = GetBrushCacheKey();

        if (key == CachedBrushKey)
            return;

        CachedBrushKey = key;

        if (ViewModel.SelectedPrefabBrush is not null)
            CachedBrush = ViewModel.SelectedPrefabBrush;
        else
            CachedBrush = LightBrush.FromShapeRotated(
                ViewModel.SelectedBrushShape,
                ViewModel.BrushRadius,
                ViewModel.BrushRotation,
                ViewModel.BrushIntensity);
    }

    private string GetBrushCacheKey()
    {
        if (ViewModel is null)
            return "";

        if (ViewModel.SelectedPrefabBrush is not null)
            return $"prefab_{ViewModel.SelectedPrefabBrush.GetHashCode()}";

        return $"{ViewModel.SelectedBrushShape}_{ViewModel.BrushRadius}_{ViewModel.BrushRotation:F1}_{ViewModel.BrushIntensity}";
    }

    private SKRect GetCurrentViewRect()
    {
        if (SkElement is null || (SkElement.ActualWidth == 0) || (SkElement.ActualHeight == 0))
            return SKRect.Empty;

        if (!SkElement.Matrix.TryInvert(out var inverted))
            return SKRect.Empty;

        var dpiScale = (float)DpiHelper.GetDpiScaleFactor();
        var topLeft = inverted.MapPoint(new SKPoint(0, 0));
        var bottomRight = inverted.MapPoint(new SKPoint((float)SkElement.ActualWidth * dpiScale, (float)SkElement.ActualHeight * dpiScale));

        return SKRect.Create(
            topLeft.X,
            topLeft.Y,
            bottomRight.X - topLeft.X,
            bottomRight.Y - topLeft.Y);
    }

    public void Initialize(int tileW, int tileH, byte[,] lightGrid)
    {
        TileW = tileW;
        TileH = tileH;
        LightGrid = lightGrid;
        GridHeight = lightGrid.GetLength(0);
        GridWidth = lightGrid.GetLength(1);

        CreateSkElement();
        RebuildDarknessOverlay();
    }

    public void InvalidateBrushPreview()
    {
        CachedBrush = null;
        CachedBrushKey = null;
    }

    /// <summary>
    ///     Renders the actual map tiles as the background using chunk-based rendering
    /// </summary>
    public void LoadMapBackground(int mapNumber, int width, int height)
    {
        //dispose old map chunk manager
        MapChunkManager?.Dispose();
        MapChunkManager = null;
        AnimatedBgChunks.Clear();
        AnimatedFgChunks.Clear();
        BgTileIds = null;
        LfgTileIds = null;
        RfgTileIds = null;

        try
        {
            var mapPath = Path.Combine(PathHelper.Instance.ArchivesPath!, "maps", $"lod{mapNumber}.map");

            if (!File.Exists(mapPath))
                return;

            var map = MapFile.FromFile(mapPath, width, height);

            //extract all tile IDs into flat 2d arrays
            MapTileHeight = height;
            BgTileIds = new int[width, height];
            LfgTileIds = new int[width, height];
            RfgTileIds = new int[width, height];

            for (var y = 0; y < height; y++)
                for (var x = 0; x < width; x++)
                {
                    var bgId = map.Tiles[x, y].Background;

                    if (bgId > 0)
                        bgId--;

                    BgTileIds[x, y] = bgId;
                    LfgTileIds[x, y] = map.Tiles[x, y].LeftForeground;
                    RfgTileIds[x, y] = map.Tiles[x, y].RightForeground;
                }

            //create chunk manager and mark all chunks dirty
            MapChunkManager = new ChunkManager(width, height);
            MapChunkManager.MarkAllDirty(LayerFlags.All);

            //initialize viewport rect; OnPaint will update it once layout is ready,
            //but the first render needs a valid rect to determine visible chunks
            var viewRect = GetCurrentViewRect();

            LatestMapViewRect = new SKRect(
                viewRect.Left,
                viewRect.Top + MapViewerControl.FOREGROUND_PADDING,
                viewRect.Right,
                viewRect.Bottom + MapViewerControl.FOREGROUND_PADDING);

            QueueMapBackgroundRender();
            QueueMapForegroundRender();

            //start animation timer for map tile animations
            MapAnimationTimer?.Stop();

            MapAnimationTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(200)
            };

            MapAnimationTimer.Tick += (_, _) =>
            {
                if (MapChunkManager is null)
                    return;

                //only re-render chunks that actually contain animated tiles
                var hasBgWork = false;
                var hasFgWork = false;

                foreach (var (cx, cy) in AnimatedBgChunks)
                {
                    MapChunkManager.Chunks[cx, cy].BackgroundDirty = true;
                    hasBgWork = true;
                }

                foreach (var (cx, cy) in AnimatedFgChunks)
                {
                    MapChunkManager.Chunks[cx, cy].ForegroundDirty = true;
                    hasFgWork = true;
                }

                if (hasBgWork)
                    QueueMapBackgroundRender();

                if (hasFgWork)
                    QueueMapForegroundRender();
            };

            MapAnimationTimer.Start();
        } catch
        {
            //ignored - map background is optional
        }

        Redraw();
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (SkElement is null || ViewModel is null || LightGrid is null)
            return;

        //don't draw while panning
        if (SkElement.IsPanning)
            return;

        //start a new stroke — snapshot the entire grid before any changes
        //we'll compute the affected region during the stroke and snapshot just that region on mouse up
        StrokeMinX = int.MaxValue;
        StrokeMinY = int.MaxValue;
        StrokeMaxX = int.MinValue;
        StrokeMaxY = int.MinValue;
        StrokeBeforeSnapshot = (byte[,])LightGrid.Clone();

        IsDrawing = true;
        ApplyBrushAtMouse();
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        //move focus to the parent UserControl so PreviewKeyDown (Ctrl+Z) works
        Focus();

        if (IsDrawing && LightGrid is not null && StrokeBeforeSnapshot is not null && (StrokeMinX <= StrokeMaxX))
        {
            //compute the affected bounding box
            var x = Math.Max(0, StrokeMinX);
            var y = Math.Max(0, StrokeMinY);
            var x2 = Math.Min(GridWidth - 1, StrokeMaxX);
            var y2 = Math.Min(GridHeight - 1, StrokeMaxY);
            var w = x2 - x + 1;
            var h = y2 - y + 1;

            if ((w > 0) && (h > 0))
            {
                var before = HeaActionContext.CaptureRegion(
                    StrokeBeforeSnapshot,
                    x,
                    y,
                    w,
                    h);

                var after = HeaActionContext.CaptureRegion(
                    LightGrid,
                    x,
                    y,
                    w,
                    h);

                //only record if something actually changed
                if (!before.AsSpan()
                           .SequenceEqual(after))
                {
                    UndoStack.AddNewest(
                        new HeaActionContext
                        {
                            X = x,
                            Y = y,
                            Width = w,
                            Height = h,
                            Before = before,
                            After = after
                        });

                    RedoStack.Clear();
                }
            }
        }

        StrokeBeforeSnapshot = null;
        IsDrawing = false;
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (SkElement is null || ViewModel is null)
            return;

        //update mouse position text
        var mousePoint = SkElement.GetMousePoint();

        if (mousePoint is not null)
        {
            var pt = mousePoint.Value;

            //convert to light grid coords
            var gx = (int)pt.X - OVERLAY_OFFSET_X;
            var gy = (int)pt.Y - OVERLAY_OFFSET_Y;

            if ((gx >= 0) && (gx < GridWidth) && (gy >= 0) && (gy < GridHeight))
            {
                var lightVal = LightGrid?[gy, gx] ?? 0;
                ViewModel.MousePositionText = $"({gx}, {gy}) Light: {lightVal}/255";
            } else
                ViewModel.MousePositionText = $"({gx}, {gy})";
        }

        if (IsDrawing && (e.LeftButton == MouseButtonState.Pressed))
            ApplyBrushAtMouse();

        //redraw for brush cursor update
        Redraw();
    }

    private void OnPaint(object? sender, SKPaintGLSurfaceEventArgs e)
    {
        using var renderSync = RenderSync.EnterScope();

        //dispose gpu textures that were replaced by background render tasks
        while (PendingTextureDisposals.TryDequeue(out var oldTexture))
            oldTexture.Dispose();

        var canvas = e.Surface.Canvas;
        var grContext = (GRContext)e.Surface.Context;

        //draw map chunks (background then foreground)
        if (MapChunkManager is not null)
        {
            var viewRect = GetCurrentViewRect();

            //shift viewport into chunk coordinate space: chunks include FOREGROUND_PADDING
            //in their pixel bounds, but the HeaEditor draws them offset by -FOREGROUND_PADDING
            var chunkViewRect = new SKRect(
                viewRect.Left,
                viewRect.Top + MapViewerControl.FOREGROUND_PADDING,
                viewRect.Right,
                viewRect.Bottom + MapViewerControl.FOREGROUND_PADDING);

            var visibleMapChunks = MapChunkManager.GetVisibleChunks(chunkViewRect);

            //only check for dirty chunks when viewport has changed (pan/zoom/initial load)
            //avoids re-triggering renders on every paint (render→redraw→paint cycle)
            if (chunkViewRect != LatestMapViewRect)
            {
                LatestMapViewRect = chunkViewRect;

                var needsBgRender = false;
                var needsFgRender = false;

                foreach (var chunk in visibleMapChunks)
                {
                    if (chunk.BackgroundDirty)
                        needsBgRender = true;

                    if (chunk.ForegroundDirty)
                        needsFgRender = true;
                }

                if (needsBgRender)
                    QueueMapBackgroundRender();

                if (needsFgRender)
                    QueueMapForegroundRender();
            }

            //promote raster-backed map chunk images to gpu textures
            if (grContext is not null)
                foreach (var chunk in visibleMapChunks)
                    PromoteMapChunkToTexture(chunk, grContext);

            using var paint = new SKPaint();
            paint.IsAntialias = false;

            //background tiles
            foreach (var chunk in visibleMapChunks)
                if (chunk.BackgroundImage is not null)
                    canvas.DrawImage(
                        chunk.BackgroundImage,
                        chunk.PixelBounds.Left,
                        chunk.PixelBounds.Top - MapViewerControl.FOREGROUND_PADDING,
                        paint);

            //foreground tiles (drawn on top of background)
            foreach (var chunk in visibleMapChunks)
                if (chunk.ForegroundImage is not null)
                    canvas.DrawImage(
                        chunk.ForegroundImage,
                        chunk.ForegroundPixelBounds.Left,
                        chunk.ForegroundPixelBounds.Top - MapViewerControl.FOREGROUND_PADDING,
                        paint);

            //screen-blend foreground (transparent tiles drawn with Screen blend mode)
            using var screenBlendPaint = new SKPaint();
            screenBlendPaint.BlendMode = SKBlendMode.Screen;

            foreach (var chunk in visibleMapChunks)
                if (chunk.ScreenBlendForegroundImage is not null)
                    canvas.DrawImage(
                        chunk.ScreenBlendForegroundImage,
                        chunk.ForegroundPixelBounds.Left,
                        chunk.ForegroundPixelBounds.Top - MapViewerControl.FOREGROUND_PADDING,
                        screenBlendPaint);
        }

        //temporarily stamp the brush into the light grid and rebuild affected
        //darkness chunks so the preview shows the exact result of stamping
        DrawBrushPreview(canvas);

        //draw darkness overlay
        if (DarknessChunkManager is not null)
            for (var cy = 0; cy < DarknessChunkManager.ChunksHigh; cy++)
                for (var cx = 0; cx < DarknessChunkManager.ChunksWide; cx++)
                {
                    var chunk = DarknessChunkManager.Chunks[cx, cy];

                    //promote darkness chunk images to gpu textures
                    if (grContext is not null && chunk.Image is { IsTextureBacked: false } rasterImg)
                    {
                        var gpuImage = rasterImg.ToTextureImage(grContext);
                        rasterImg.Dispose();
                        chunk.Image = gpuImage;
                    }

                    if (chunk.Image is not null)
                        canvas.DrawImage(chunk.Image, chunk.PixelBounds.Left + OVERLAY_OFFSET_X, chunk.PixelBounds.Top + OVERLAY_OFFSET_Y);
                }
    }

    private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (ViewModel is null)
            return;

        var mods = Keyboard.Modifiers;

        //ctrl + scroll = rotate brush
        if (mods.HasFlag(ModifierKeys.Control))
        {
            var delta = e.Delta > 0 ? 5f : -5f;
            var newAngle = (ViewModel.BrushRotation + delta) % 360f;

            if (newAngle < 0)
                newAngle += 360f;

            ViewModel.BrushRotation = newAngle;
            InvalidateBrushPreview();
            Redraw();
            e.Handled = true;

            return;
        }

        //shift + scroll = change radius
        if (mods.HasFlag(ModifierKeys.Shift))
        {
            var delta = e.Delta > 0 ? 2 : -2;
            ViewModel.BrushRadius = Math.Clamp(ViewModel.BrushRadius + delta, 5, 120);
            InvalidateBrushPreview();
            Redraw();
            e.Handled = true;

            return;
        }

        //alt + scroll = change intensity
        if (mods.HasFlag(ModifierKeys.Alt))
        {
            var delta = e.Delta > 0 ? 1 : -1;
            ViewModel.BrushIntensity = (byte)Math.Clamp(ViewModel.BrushIntensity + delta, 1, 255);
            InvalidateBrushPreview();
            Redraw();
            e.Handled = true;
        }
    }

    private static void PromoteMapChunkToTexture(MapChunk chunk, GRContext grContext)
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

        if (chunk.ScreenBlendForegroundImage is { IsTextureBacked: false } rasterScreenBlend)
        {
            chunk.ScreenBlendForegroundImage = rasterScreenBlend.ToTextureImage(grContext);
            rasterScreenBlend.Dispose();
        }
    }

    private void QueueMapBackgroundRender()
    {
        if (MapBackgroundRenderTask is { IsCompleted: false })
            return;

        MapBackgroundRenderTask = Task.Run(() =>
        {
            if (MapChunkManager is null || BgTileIds is null)
                return;

            var dirtyVisible = MapChunkManager.GetDirtyVisibleBackgroundChunks(LatestMapViewRect);

            foreach (var chunk in dirtyVisible)
                chunk.BackgroundDirty = false;

            foreach (var chunk in dirtyVisible)
                RenderMapBackgroundChunk(chunk);

            Dispatcher.BeginInvoke(Redraw);
        });
    }

    private void QueueMapForegroundRender()
    {
        if (MapForegroundRenderTask is { IsCompleted: false })
            return;

        MapForegroundRenderTask = Task.Run(() =>
        {
            if (MapChunkManager is null || LfgTileIds is null || RfgTileIds is null)
                return;

            var dirtyVisible = MapChunkManager.GetDirtyVisibleForegroundChunks(LatestMapViewRect);

            foreach (var chunk in dirtyVisible)
                chunk.ForegroundDirty = false;

            foreach (var chunk in dirtyVisible)
                RenderMapForegroundChunk(chunk);

            Dispatcher.BeginInvoke(Redraw);
        });
    }

    private DarknessLayer CurrentDarknessLayer
        => ViewModel?.SelectedDarknessLayer ?? DarknessLayer.Defaults[0];

    /// <summary>
    ///     Rebuilds the entire darkness overlay from scratch using chunks
    /// </summary>
    public void RebuildDarknessOverlay()
    {
        if (LightGrid is null)
            return;

        var layer = CurrentDarknessLayer;

        DarknessChunkManager?.Dispose();
        DarknessChunkManager = new DarknessChunkManager(GridWidth, GridHeight);
        DarknessChunkManager.MarkAllDirty();
        DarknessChunkManager.RebuildDirtyChunks(LightGrid, layer.Alpha, layer.Color);
    }

    /// <summary>
    ///     Rebuilds only the chunks affected by a brush stamp
    /// </summary>
    public void RebuildDarknessOverlayRegion(int centerX, int centerY, int brushRadius)
    {
        if (LightGrid is null || DarknessChunkManager is null)
        {
            RebuildDarknessOverlay();

            return;
        }

        var layer = CurrentDarknessLayer;

        var x0 = Math.Max(0, centerX - brushRadius);
        var y0 = Math.Max(0, centerY - brushRadius);
        var x1 = Math.Min(GridWidth - 1, centerX + brushRadius);
        var y1 = Math.Min(GridHeight - 1, centerY + brushRadius);

        DarknessChunkManager.MarkRangeDirty(
            x0,
            y0,
            x1,
            y1);
        DarknessChunkManager.RebuildDirtyChunks(LightGrid, layer.Alpha, layer.Color);
    }

    public void Redo()
    {
        if (LightGrid is null || (RedoStack.Count == 0))
            return;

        var action = RedoStack.PopNewest();

        HeaActionContext.RestoreRegion(
            LightGrid,
            action.After,
            action.X,
            action.Y,
            action.Width,
            action.Height);
        UndoStack.AddNewest(action);

        RebuildDarknessOverlayRegion(
            action.X + action.Width / 2,
            action.Y + action.Height / 2,
            Math.Max(action.Width, action.Height) / 2 + 1);

        Redraw();
    }

    public void Redraw() => SkElement?.Redraw();

    /// <summary>
    ///     Reinitializes the light grid with new dimensions (when user changes bounds selector). Existing light data is
    ///     discarded
    /// </summary>
    public void Reinitialize(int tileW, int tileH)
    {
        TileW = tileW;
        TileH = tileH;

        var scanW = 28 * (tileW + tileH) + 1280;
        var scanH = 14 * (tileW + tileH) + 960;

        LightGrid = new byte[scanH, scanW];
        GridHeight = scanH;
        GridWidth = scanW;

        RebuildDarknessOverlay();
        Redraw();
    }

    private void RenderMapBackgroundChunk(MapChunk chunk)
    {
        var chunkBounds = chunk.PixelBounds;
        var bitmapWidth = chunkBounds.Width;
        var bitmapHeight = chunkBounds.Height;

        if ((bitmapWidth <= 0) || (bitmapHeight <= 0))
            return;

        using var bitmap = new SKBitmap(new SKImageInfo(bitmapWidth, bitmapHeight));
        using var canvas = new SKCanvas(bitmap);

        var elapsed = MapAnimationStopwatch.Elapsed;

        //offset so tiles draw at local chunk coords
        canvas.Translate(-chunkBounds.Left, -chunkBounds.Top);

        var hasAnimatedTiles = false;

        for (var y = chunk.TileBounds.Top; y <= chunk.TileBounds.Bottom; y++)
        {
            for (var x = chunk.TileBounds.Left; x <= chunk.TileBounds.Right; x++)
            {
                var tileId = BgTileIds![x, y];
                var animation = MapEditorRenderUtil.RenderAnimatedBackground(tileId);
                var frame = MapEditorRenderUtil.GetAnimationFrame(animation, elapsed);

                if (frame is null)
                    continue;

                if (animation!.Frames.Count > 1)
                    hasAnimatedTiles = true;

                var (drawX, drawY) = MapEditorRenderUtil.GetTileDrawPosition(x, y, MapTileHeight);

                canvas.DrawImage(frame, drawX, drawY);
            }
        }

        if (hasAnimatedTiles)
            AnimatedBgChunks.Add((chunk.ChunkX, chunk.ChunkY));

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

    private void RenderMapForegroundChunk(MapChunk chunk)
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
        var hasAnimatedTiles = false;

        var elapsed = MapAnimationStopwatch.Elapsed;

        canvas.Translate(-fgBounds.Left, -fgBounds.Top);

        for (var y = chunk.TileBounds.Top; y <= chunk.TileBounds.Bottom; y++)
        {
            for (var x = chunk.TileBounds.Left; x <= chunk.TileBounds.Right; x++)
            {
                var lfgId = LfgTileIds![x, y];
                var rfgId = RfgTileIds![x, y];

                var (drawX, drawY) = MapEditorRenderUtil.GetTileDrawPosition(x, y, MapTileHeight);

                if (lfgId.IsRenderedTileIndex())
                {
                    var lfgAnim = MapEditorRenderUtil.RenderAnimatedForeground(lfgId);
                    var frame = MapEditorRenderUtil.GetAnimationFrame(lfgAnim, elapsed);

                    if (frame is not null)
                    {
                        if (lfgAnim!.Frames.Count > 1)
                            hasAnimatedTiles = true;

                        GetTargetCanvas(lfgId)
                            .DrawImage(
                                frame,
                                drawX,
                                drawY + DALIB_CONSTANTS.HALF_TILE_HEIGHT - frame.Height + DALIB_CONSTANTS.HALF_TILE_HEIGHT);
                    }
                }

                if (rfgId.IsRenderedTileIndex())
                {
                    var rfgAnim = MapEditorRenderUtil.RenderAnimatedForeground(rfgId);
                    var frame = MapEditorRenderUtil.GetAnimationFrame(rfgAnim, elapsed);

                    if (frame is not null)
                    {
                        if (rfgAnim!.Frames.Count > 1)
                            hasAnimatedTiles = true;

                        GetTargetCanvas(rfgId)
                            .DrawImage(
                                frame,
                                drawX + DALIB_CONSTANTS.HALF_TILE_WIDTH,
                                drawY + DALIB_CONSTANTS.HALF_TILE_HEIGHT - frame.Height + DALIB_CONSTANTS.HALF_TILE_HEIGHT);
                    }
                }

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

        if (hasAnimatedTiles)
            AnimatedFgChunks.Add((chunk.ChunkX, chunk.ChunkY));

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

    public void Undo()
    {
        if (LightGrid is null || (UndoStack.Count == 0))
            return;

        var action = UndoStack.PopNewest();

        HeaActionContext.RestoreRegion(
            LightGrid,
            action.Before,
            action.X,
            action.Y,
            action.Width,
            action.Height);
        RedoStack.AddNewest(action);

        RebuildDarknessOverlayRegion(
            action.X + action.Width / 2,
            action.Y + action.Height / 2,
            Math.Max(action.Width, action.Height) / 2 + 1);

        Redraw();
    }
}