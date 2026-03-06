using ChaosAssetManager.Controls.MapEditorControls;
using ChaosAssetManager.Definitions;
using SkiaSharp;
using DALIB_CONSTANTS = DALib.Definitions.CONSTANTS;

namespace ChaosAssetManager.Helpers;

public sealed class MapChunk : IDisposable
{
    //dirty flags per layer
    public bool BackgroundDirty { get; set; } = true;

    //cached rendered images per layer
    public SKImage? BackgroundImage { get; set; }
    public bool ForegroundDirty { get; set; } = true;
    public SKImage? ForegroundImage { get; set; }
    public bool TabMapDirty { get; set; } = true;
    public SKImage? TabMapImage { get; set; }
    public int ChunkX { get; }
    public int ChunkY { get; }

    //pixel bounds extended upward for foreground sprites
    public SKRectI ForegroundPixelBounds { get; }

    //pixel bounds in full map space (for background/tabmap layer)
    public SKRectI PixelBounds { get; }
    public int TileEndX { get; }
    public int TileEndY { get; }

    //tile range this chunk covers
    public int TileStartX { get; }
    public int TileStartY { get; }

    public MapChunk(
        int chunkX,
        int chunkY,
        int tileStartX,
        int tileStartY,
        int tileEndX,
        int tileEndY,
        int mapWidth,
        int mapHeight)
    {
        ChunkX = chunkX;
        ChunkY = chunkY;
        TileStartX = tileStartX;
        TileStartY = tileStartY;
        TileEndX = tileEndX;
        TileEndY = tileEndY;

        PixelBounds = ComputePixelBounds(
            tileStartX,
            tileStartY,
            tileEndX,
            tileEndY,
            mapHeight);
        ForegroundPixelBounds = ComputeForegroundPixelBounds(PixelBounds);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        BackgroundImage?.Dispose();
        ForegroundImage?.Dispose();
        TabMapImage?.Dispose();
        BackgroundImage = null;
        ForegroundImage = null;
        TabMapImage = null;
    }

    private static SKRectI ComputeForegroundPixelBounds(SKRectI baseBounds)
        =>

            //foreground sprites can be much taller than a tile, extend top upward
            new(
                baseBounds.Left,
                baseBounds.Top - MapViewerControl.FOREGROUND_PADDING,
                baseBounds.Right,
                baseBounds.Bottom);

    /// <summary>
    ///     Computes the pixel bounding box for tiles in the given range using the isometric formula.
    /// </summary>
    private static SKRectI ComputePixelBounds(
        int startX,
        int startY,
        int endX,
        int endY,
        int mapHeight)
    {
        //pixel position formula from the rendering code:
        //pixelX(x, y) = (mapHeight - 1 - y) * HALF_TILE_WIDTH + x * HALF_TILE_WIDTH
        //pixelY(x, y) = FOREGROUND_PADDING + y * HALF_TILE_HEIGHT + x * HALF_TILE_HEIGHT

        var minPixelX = int.MaxValue;
        var minPixelY = int.MaxValue;
        var maxPixelX = int.MinValue;
        var maxPixelY = int.MinValue;

        //check all 4 corner tiles to find the bounding box
        int[] xs =
        [
            startX,
            endX
        ];

        int[] ys =
        [
            startY,
            endY
        ];

        foreach (var x in xs)
        {
            foreach (var y in ys)
            {
                var px = (mapHeight - 1 - y) * DALIB_CONSTANTS.HALF_TILE_WIDTH + x * DALIB_CONSTANTS.HALF_TILE_WIDTH;
                var py = MapViewerControl.FOREGROUND_PADDING + y * DALIB_CONSTANTS.HALF_TILE_HEIGHT + x * DALIB_CONSTANTS.HALF_TILE_HEIGHT;

                minPixelX = Math.Min(minPixelX, px);
                minPixelY = Math.Min(minPixelY, py);

                //tile occupies TILE_WIDTH x TILE_HEIGHT from its origin
                maxPixelX = Math.Max(maxPixelX, px + DALIB_CONSTANTS.TILE_WIDTH);
                maxPixelY = Math.Max(maxPixelY, py + DALIB_CONSTANTS.TILE_HEIGHT);
            }
        }

        return new SKRectI(
            minPixelX,
            minPixelY,
            maxPixelX,
            maxPixelY);
    }

    public bool IsDirty(LayerFlags layer)
    {
        if (layer.HasFlag(LayerFlags.Background) && BackgroundDirty)
            return true;

        if ((layer.HasFlag(LayerFlags.LeftForeground) || layer.HasFlag(LayerFlags.RightForeground)) && ForegroundDirty)
            return true;

        return false;
    }
}

public sealed class ChunkManager : IDisposable
{
    public const int CHUNK_SIZE = 16;

    public MapChunk[,] Chunks { get; }
    public int ChunksHigh { get; }
    public int ChunksWide { get; }
    public int MapHeight { get; }
    public int MapWidth { get; }

    public ChunkManager(int mapWidth, int mapHeight)
    {
        MapWidth = mapWidth;
        MapHeight = mapHeight;
        ChunksWide = (mapWidth + CHUNK_SIZE - 1) / CHUNK_SIZE;
        ChunksHigh = (mapHeight + CHUNK_SIZE - 1) / CHUNK_SIZE;
        Chunks = new MapChunk[ChunksWide, ChunksHigh];

        for (var cy = 0; cy < ChunksHigh; cy++)
        {
            for (var cx = 0; cx < ChunksWide; cx++)
            {
                var tileStartX = cx * CHUNK_SIZE;
                var tileStartY = cy * CHUNK_SIZE;
                var tileEndX = Math.Min(tileStartX + CHUNK_SIZE - 1, mapWidth - 1);
                var tileEndY = Math.Min(tileStartY + CHUNK_SIZE - 1, mapHeight - 1);

                Chunks[cx, cy] = new MapChunk(
                    cx,
                    cy,
                    tileStartX,
                    tileStartY,
                    tileEndX,
                    tileEndY,
                    mapWidth,
                    mapHeight);
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        for (var cy = 0; cy < ChunksHigh; cy++)
            for (var cx = 0; cx < ChunksWide; cx++)
                Chunks[cx, cy]
                    .Dispose();
    }

    /// <summary>
    ///     Returns all chunks (for full map export).
    /// </summary>
    public IEnumerable<MapChunk> GetAllChunks()
    {
        for (var cy = 0; cy < ChunksHigh; cy++)
            for (var cx = 0; cx < ChunksWide; cx++)
                yield return Chunks[cx, cy];
    }

    /// <summary>
    ///     Returns chunks that are both dirty for the specified layer and visible in the viewport.
    /// </summary>
    public List<MapChunk> GetDirtyVisibleBackgroundChunks(SKRect viewRect)
    {
        var result = new List<MapChunk>();
        var viewRectI = SKRectI.Ceiling(viewRect);

        for (var cy = 0; cy < ChunksHigh; cy++)
            for (var cx = 0; cx < ChunksWide; cx++)
            {
                var chunk = Chunks[cx, cy];

                if (chunk.BackgroundDirty && chunk.PixelBounds.IntersectsWith(viewRectI))
                    result.Add(chunk);
            }

        return result;
    }

    public List<MapChunk> GetDirtyVisibleForegroundChunks(SKRect viewRect)
    {
        var result = new List<MapChunk>();
        var viewRectI = SKRectI.Ceiling(viewRect);

        for (var cy = 0; cy < ChunksHigh; cy++)
            for (var cx = 0; cx < ChunksWide; cx++)
            {
                var chunk = Chunks[cx, cy];

                if (chunk.ForegroundDirty && chunk.ForegroundPixelBounds.IntersectsWith(viewRectI))
                    result.Add(chunk);
            }

        return result;
    }

    public List<MapChunk> GetDirtyVisibleTabMapChunks(SKRect viewRect)
    {
        var result = new List<MapChunk>();
        var viewRectI = SKRectI.Ceiling(viewRect);

        for (var cy = 0; cy < ChunksHigh; cy++)
            for (var cx = 0; cx < ChunksWide; cx++)
            {
                var chunk = Chunks[cx, cy];

                if (chunk.TabMapDirty && chunk.PixelBounds.IntersectsWith(viewRectI))
                    result.Add(chunk);
            }

        return result;
    }

    /// <summary>
    ///     Returns chunks whose pixel bounds intersect the given viewport rect.
    /// </summary>
    public List<MapChunk> GetVisibleChunks(SKRect viewRect)
    {
        var result = new List<MapChunk>();
        var viewRectI = SKRectI.Ceiling(viewRect);

        for (var cy = 0; cy < ChunksHigh; cy++)
            for (var cx = 0; cx < ChunksWide; cx++)
            {
                var chunk = Chunks[cx, cy];

                if (chunk.ForegroundPixelBounds.IntersectsWith(viewRectI))
                    result.Add(chunk);
            }

        return result;
    }

    /// <summary>
    ///     Marks all chunks as dirty for the specified layers.
    /// </summary>
    public void MarkAllDirty(LayerFlags layers)
    {
        for (var cy = 0; cy < ChunksHigh; cy++)
            for (var cx = 0; cx < ChunksWide; cx++)
            {
                var chunk = Chunks[cx, cy];

                if (layers.HasFlag(LayerFlags.Background))
                    chunk.BackgroundDirty = true;

                if (layers.HasFlag(LayerFlags.LeftForeground) || layers.HasFlag(LayerFlags.RightForeground))
                {
                    chunk.ForegroundDirty = true;
                    chunk.TabMapDirty = true;
                }
            }
    }

    /// <summary>
    ///     Marks the chunk containing the given tile as dirty for the specified layers.
    /// </summary>
    public void MarkDirty(int tileX, int tileY, LayerFlags layers)
    {
        var cx = tileX / CHUNK_SIZE;
        var cy = tileY / CHUNK_SIZE;

        if ((cx < 0) || (cx >= ChunksWide) || (cy < 0) || (cy >= ChunksHigh))
            return;

        var chunk = Chunks[cx, cy];

        if (layers.HasFlag(LayerFlags.Background))
            chunk.BackgroundDirty = true;

        if (layers.HasFlag(LayerFlags.LeftForeground) || layers.HasFlag(LayerFlags.RightForeground))
        {
            chunk.ForegroundDirty = true;
            chunk.TabMapDirty = true;
        }
    }

    /// <summary>
    ///     Marks a range of tiles dirty (e.g. for TileGrab operations).
    /// </summary>
    public void MarkRangeDirty(
        int startX,
        int startY,
        int width,
        int height,
        LayerFlags layers)
    {
        var startCx = Math.Max(0, startX / CHUNK_SIZE);
        var startCy = Math.Max(0, startY / CHUNK_SIZE);
        var endCx = Math.Min(ChunksWide - 1, (startX + width - 1) / CHUNK_SIZE);
        var endCy = Math.Min(ChunksHigh - 1, (startY + height - 1) / CHUNK_SIZE);

        for (var cy = startCy; cy <= endCy; cy++)
            for (var cx = startCx; cx <= endCx; cx++)
            {
                var chunk = Chunks[cx, cy];

                if (layers.HasFlag(LayerFlags.Background))
                    chunk.BackgroundDirty = true;

                if (layers.HasFlag(LayerFlags.LeftForeground) || layers.HasFlag(LayerFlags.RightForeground))
                {
                    chunk.ForegroundDirty = true;
                    chunk.TabMapDirty = true;
                }
            }
    }
}