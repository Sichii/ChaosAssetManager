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
    public SKImage? ScreenBlendForegroundImage { get; set; }

    //pixel bounds extended upward for foreground sprites
    public SKRectI ForegroundPixelBounds { get; set; }

    //pixel bounds in full map space (for background/tabmap layer)
    public SKRectI PixelBounds { get; set; }
    public bool TabMapDirty { get; set; } = true;
    public SKImage? TabMapImage { get; set; }
    public int ChunkX { get; }
    public int ChunkY { get; }

    //tile range this chunk covers
    public SKRectI TileBounds { get; }

    public MapChunk(
        int chunkX,
        int chunkY,
        SKRectI tileBounds,
        int mapHeight)
    {
        ChunkX = chunkX;
        ChunkY = chunkY;
        TileBounds = tileBounds;

        PixelBounds = ComputePixelBounds(tileBounds, mapHeight);
        ForegroundPixelBounds = ComputeForegroundPixelBounds(PixelBounds);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        BackgroundImage?.Dispose();
        ForegroundImage?.Dispose();
        ScreenBlendForegroundImage?.Dispose();
        TabMapImage?.Dispose();
        BackgroundImage = null;
        ForegroundImage = null;
        ScreenBlendForegroundImage = null;
        TabMapImage = null;
    }

    private static SKRectI ComputeForegroundPixelBounds(SKRectI baseBounds)
        =>

            //foreground sprites can be much taller than a tile, extend top upward
            new(
                baseBounds.Left,
                baseBounds.Top - MapEditorRenderUtil.FOREGROUND_PADDING,
                baseBounds.Right,
                baseBounds.Bottom);

    private static SKRectI ComputePixelBounds(SKRectI tb, int mapHeight)
    {
        var minPixelX = int.MaxValue;
        var minPixelY = int.MaxValue;
        var maxPixelX = int.MinValue;
        var maxPixelY = int.MinValue;

        int[] xs =
        [
            tb.Left,
            tb.Right
        ];

        int[] ys =
        [
            tb.Top,
            tb.Bottom
        ];

        foreach (var x in xs)
        {
            foreach (var y in ys)
            {
                var (px, py) = MapEditorRenderUtil.GetTileDrawPosition(x, y, mapHeight);

                minPixelX = Math.Min(minPixelX, px);
                minPixelY = Math.Min(minPixelY, py);

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
                var tileBounds = new SKRectI(
                    cx * CHUNK_SIZE,
                    cy * CHUNK_SIZE,
                    Math.Min(cx * CHUNK_SIZE + CHUNK_SIZE - 1, mapWidth - 1),
                    Math.Min(cy * CHUNK_SIZE + CHUNK_SIZE - 1, mapHeight - 1));

                Chunks[cx, cy] = new MapChunk(
                    cx,
                    cy,
                    tileBounds,
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