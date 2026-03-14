using DALib.Drawing;
using SkiaSharp;

namespace ChaosAssetManager.Helpers;

public sealed class DarknessChunk : IDisposable
{
    public bool Dirty { get; set; } = true;
    public SKImage? Image { get; set; }
    public SKBitmap? Bitmap { get; set; }
    public int ChunkX { get; }
    public int ChunkY { get; }

    //pixel bounds in full overlay space
    public SKRectI PixelBounds { get; }

    public DarknessChunk(int chunkX, int chunkY, SKRectI pixelBounds)
    {
        ChunkX = chunkX;
        ChunkY = chunkY;
        PixelBounds = pixelBounds;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Image?.Dispose();
        Bitmap?.Dispose();
        Image = null;
        Bitmap = null;
    }
}

public sealed class DarknessChunkManager : IDisposable
{
    public const int CHUNK_SIZE = 256;

    public DarknessChunk[,] Chunks { get; }
    public int ChunksWide { get; }
    public int ChunksHigh { get; }
    public int GridWidth { get; }
    public int GridHeight { get; }

    public DarknessChunkManager(int gridWidth, int gridHeight)
    {
        GridWidth = gridWidth;
        GridHeight = gridHeight;
        ChunksWide = (gridWidth + CHUNK_SIZE - 1) / CHUNK_SIZE;
        ChunksHigh = (gridHeight + CHUNK_SIZE - 1) / CHUNK_SIZE;
        Chunks = new DarknessChunk[ChunksWide, ChunksHigh];

        for (var cy = 0; cy < ChunksHigh; cy++)
        {
            for (var cx = 0; cx < ChunksWide; cx++)
            {
                var px0 = cx * CHUNK_SIZE;
                var py0 = cy * CHUNK_SIZE;
                var px1 = Math.Min(px0 + CHUNK_SIZE, gridWidth);
                var py1 = Math.Min(py0 + CHUNK_SIZE, gridHeight);

                Chunks[cx, cy] = new DarknessChunk(
                    cx,
                    cy,
                    new SKRectI(px0, py0, px1, py1));
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        for (var cy = 0; cy < ChunksHigh; cy++)
            for (var cx = 0; cx < ChunksWide; cx++)
                Chunks[cx, cy].Dispose();
    }

    /// <summary>
    ///     Marks all chunks as dirty
    /// </summary>
    public void MarkAllDirty()
    {
        for (var cy = 0; cy < ChunksHigh; cy++)
            for (var cx = 0; cx < ChunksWide; cx++)
                Chunks[cx, cy].Dirty = true;
    }

    /// <summary>
    ///     Marks all chunks overlapping a pixel-space rectangle as dirty
    /// </summary>
    public void MarkRangeDirty(int x0, int y0, int x1, int y1)
    {
        var startCx = Math.Max(0, x0 / CHUNK_SIZE);
        var startCy = Math.Max(0, y0 / CHUNK_SIZE);
        var endCx = Math.Min(ChunksWide - 1, x1 / CHUNK_SIZE);
        var endCy = Math.Min(ChunksHigh - 1, y1 / CHUNK_SIZE);

        for (var cy = startCy; cy <= endCy; cy++)
            for (var cx = startCx; cx <= endCx; cx++)
                Chunks[cx, cy].Dirty = true;
    }

    /// <summary>
    ///     Rebuilds any dirty chunks from the light grid
    /// </summary>
    public void RebuildDirtyChunks(byte[,] lightGrid, byte opacity)
    {
        for (var cy = 0; cy < ChunksHigh; cy++)
            for (var cx = 0; cx < ChunksWide; cx++)
            {
                var chunk = Chunks[cx, cy];

                if (!chunk.Dirty)
                    continue;

                RebuildChunk(chunk, lightGrid, opacity);
                chunk.Dirty = false;
            }
    }

    private static void RebuildChunk(DarknessChunk chunk, byte[,] lightGrid, byte opacity)
    {
        var bounds = chunk.PixelBounds;
        var w = bounds.Width;
        var h = bounds.Height;

        //reuse or create bitmap
        if (chunk.Bitmap is null || chunk.Bitmap.Width != w || chunk.Bitmap.Height != h)
        {
            chunk.Bitmap?.Dispose();
            chunk.Bitmap = new SKBitmap(w, h, SKColorType.Bgra8888, SKAlphaType.Unpremul);
        }

        using var pixMap = chunk.Bitmap.PeekPixels();
        var pixelBuffer = pixMap.GetPixelSpan<SKColor>();
        var darkColor = new SKColor(0, 0, 0, opacity);

        for (var ly = 0; ly < h; ly++)
        {
            var gy = bounds.Top + ly;

            for (var lx = 0; lx < w; lx++)
            {
                var gx = bounds.Left + lx;
                var value = lightGrid[gy, gx];

                if (value == 0)
                {
                    pixelBuffer[ly * w + lx] = darkColor;

                    continue;
                }

                var lightRatio = Math.Min(1.0f, (float)value / HeaFile.MAX_LIGHT_VALUE);
                var alpha = (byte)(opacity * (1.0f - lightRatio));
                pixelBuffer[ly * w + lx] = new SKColor(0, 0, 0, alpha);
            }
        }

        chunk.Image?.Dispose();
        chunk.Image = SKImage.FromBitmap(chunk.Bitmap);
    }
}
