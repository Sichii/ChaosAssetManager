using Chaos.Extensions.Common;
using ChaosAssetManager.Model;
using DALib.Definitions;
using DALib.Drawing;
using DALib.Utility;
using SkiaSharp;
using Graphics = DALib.Drawing.Graphics;
using Point = Chaos.Geometry.Point;

namespace ChaosAssetManager.Helpers;

public static class MapEditorRenderUtil
{
    private static readonly Lock Sync = new();
    private static readonly MapImageCache SnowMapImageCache = new();
    private static MapImageCache MapImageCache = new();
    private static SKImage? TabWallImage;

    public static void Clear()
    {
        using var @lock = Sync.EnterScope();

        var mapImageCache = MapImageCache;
        MapImageCache = new MapImageCache();

        mapImageCache.Dispose();

        Tileset = null;
        SnowTileset = null;
        MptPaletteLookup = null;
        BackgroundAnimationTable = null;
        ForegroundAnimationTable = null;
        StcPaletteLookup = null;
        StsPaletteLookup = null;
        Sotp = null;

        ArchiveCache.Clear();
    }

    public static bool IsTransparent(int tileIndex)
    {
        if (tileIndex == 0)
            return false;

        tileIndex--;

        Sotp ??= ArchiveCache.Ia["sotp.dat"]
                             .ToSpan()
                             .ToArray()
                             .Select(value => (TileFlags)value)
                             .ToArray();

        if (tileIndex >= Sotp.Length)
            return false;

        return Sotp[tileIndex]
            .HasFlag(TileFlags.Transparent);
    }

    public static bool IsWall(int tileIndex)
    {
        if (tileIndex == 0)
            return false;

        tileIndex--;

        Sotp ??= ArchiveCache.Ia["sotp.dat"]
                             .ToSpan()
                             .ToArray()
                             .Select(value => (TileFlags)value)
                             .ToArray();

        if (tileIndex >= Sotp.Length)
            return false;

        return Sotp[tileIndex]
            .HasFlag(TileFlags.Wall);
    }

    public static Animation? RenderAnimatedBackground(int tileIndex, bool snowTileSet = false)
    {
        using var @lock = Sync.EnterScope();

        try
        {
            var archive = ArchiveCache.Seo;
            var mapImageCache = snowTileSet ? SnowMapImageCache : MapImageCache;

            Tileset ??= Tileset.FromArchive("tilea.bmp", archive);
            SnowTileset ??= Tileset.FromArchive("tileas.bmp", archive);
            MptPaletteLookup ??= PaletteLookup.FromArchive("mpt", archive);
            BackgroundAnimationTable ??= TileAnimationTable.FromArchive("gndani", archive);

            var tileset = snowTileSet ? SnowTileset : Tileset;

            List<int> tileIndexes = [tileIndex];

            if (BackgroundAnimationTable.TryGetEntry(tileIndex, out var animationEntry))
            {
                var currentIndex = animationEntry.TileSequence.IndexOf((ushort)tileIndex);

                tileIndexes = animationEntry.TileSequence
                                            .Skip(currentIndex)
                                            .Concat(animationEntry.TileSequence.Take(currentIndex))
                                            .Select(num => (int)num)
                                            .ToList();
            }

            var transformer = tileIndexes.Select(index => mapImageCache.BackgroundCache.GetOrCreate(
                                             index,
                                             localIndex =>
                                             {
                                                 var tile = tileset.ElementAtOrDefault(localIndex);

                                                 if (tile is null)
                                                     return null!;

                                                 if ((tile.PixelWidth == 0) || (tile.PixelHeight == 0))
                                                     return null!;

                                                 var palette = MptPaletteLookup.GetPaletteForId(localIndex + 2);
                                                 var image = Graphics.RenderTile(tile, palette);

                                                 return image;
                                             }))

                                         // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
                                         .Where(frame => frame is not null && (frame.Handle != nint.Zero));

            var frames = new SKImageCollection(transformer);

            if (frames.IsNullOrEmpty())
                return null;

            return new Animation(frames, animationEntry?.AnimationIntervalMs);
        } catch
        {
            return null;
        }
    }

    public static Animation? RenderAnimatedForeground(int tileIndex, bool snowTileSet = false)
    {
        using var @lock = Sync.EnterScope();

        try
        {
            var archive = ArchiveCache.Ia;
            var prefix = snowTileSet ? "sts" : "stc";
            var mapImageCache = snowTileSet ? SnowMapImageCache : MapImageCache;

            ForegroundAnimationTable ??= TileAnimationTable.FromArchive("stcani", archive);
            StcPaletteLookup ??= PaletteLookup.FromArchive("stc", archive);
            StsPaletteLookup ??= PaletteLookup.FromArchive("sts", archive);

            Sotp ??= archive["sotp.dat"]
                     .ToSpan()
                     .ToArray()
                     .Select(value => (TileFlags)value)
                     .ToArray();

            var paletteLookup = snowTileSet ? StsPaletteLookup : StcPaletteLookup;

            List<int> tileIndexes = [tileIndex];

            if (ForegroundAnimationTable.TryGetEntry(tileIndex, out var animationEntry))
            {
                var currentIndex = animationEntry.TileSequence.IndexOf((ushort)tileIndex);

                tileIndexes = animationEntry.TileSequence
                                            .Skip(currentIndex)
                                            .Concat(animationEntry.TileSequence.Take(currentIndex))
                                            .Select(num => (int)num)
                                            .ToList();
            }

            var transformer = tileIndexes.Select(index => mapImageCache.ForegroundCache.GetOrCreate(
                                             index,
                                             localIndex =>
                                             {
                                                 var localEntryName = $"{prefix}{localIndex:D5}.hpf";

                                                 if (!archive.Contains(localEntryName))
                                                     return null!;

                                                 var hpfFile = HpfFile.FromArchive(localEntryName, archive);

                                                 if ((hpfFile.PixelWidth == 0) || (hpfFile.PixelHeight == 0))
                                                     return null!;

                                                 var palette = paletteLookup.GetPaletteForId(localIndex + 1);

                                                 var transparent = IsTransparent(localIndex);

                                                 return Graphics.RenderImage(hpfFile, palette, transparency: transparent);
                                             }))

                                         // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
                                         .Where(frame => frame is not null && (frame.Handle != nint.Zero));

            var frames = new SKImageCollection(transformer);

            if (frames.IsNullOrEmpty())
                return null;

            return new Animation(frames, animationEntry?.AnimationIntervalMs);
        } catch
        {
            return null;
        }
    }

    public static SKImage RenderTabWall()
    {
        if (TabWallImage is not null)
            return TabWallImage;

        using var bitmap = new SKBitmap(CONSTANTS.TILE_WIDTH, CONSTANTS.TILE_HEIGHT + 1);
        var fillColor = SKColors.Snow.WithAlpha(50);

        //fill the diamond row by row using tile row bounds
        for (var row = 0; row < CONSTANTS.TILE_HEIGHT; row++)
        {
            (var startX, var endX) = RenderUtil.GetTileRowBounds(row);

            for (var x = startX; x <= endX; x++)
                bitmap.SetPixel(x, row, fillColor);
        }

        //draw the staircase outline on top
        RenderUtil.DrawTileOutline(
            bitmap,
            0,
            0,
            SKColors.Snow);

        TabWallImage = SKImage.FromBitmap(bitmap);

        return TabWallImage;
    }

    /// <summary>
    ///     Draws only the specified edges of an isometric tile outline (staircase diamond).
    ///     Edges: topRight = top-to-right, bottomRight = right-to-bottom, bottomLeft = bottom-to-left, topLeft = left-to-top.
    /// </summary>
    public static void DrawTileOutlineEdges(
        SKBitmap bitmap,
        int tileLeft,
        int tileTop,
        SKColor color,
        bool topRight,
        bool bottomRight,
        bool bottomLeft,
        bool topLeft)
    {
        using var canvas = new SKCanvas(bitmap);
        using var paint = new SKPaint();
        paint.Color = color;
        paint.Style = SKPaintStyle.Stroke;

        var rightTwo = new SKPoint(2, 0);
        var leftTwo = new SKPoint(-2, 0);
        var downOne = new SKPoint(0, 1);
        var upOne = new SKPoint(0, -1);

        var startPoint = new SKPoint(tileLeft + 28, tileTop);

        if (topRight)
        {
            using var path = new SKPath();
            var pt = startPoint;
            path.MoveTo(pt);

            for (var i = 0; i < CONSTANTS.HALF_TILE_HEIGHT; i++)
            {
                path.LineTo(pt += rightTwo);
                path.MoveTo(pt += downOne);
            }

            canvas.DrawPath(path, paint);
        }

        //advance start point past top-right edge
        startPoint += new SKPoint(CONSTANTS.HALF_TILE_WIDTH, CONSTANTS.HALF_TILE_HEIGHT);
        startPoint += new SKPoint(-2, -1); //correction

        //bridge the right corner notch when either adjacent edge is drawn
        if (topRight || bottomRight)
        {
            using var path = new SKPath();
            var pt = startPoint;
            path.MoveTo(pt);
            path.LineTo(pt + rightTwo);

            canvas.DrawPath(path, paint);
        }

        if (bottomRight)
        {
            using var path = new SKPath();
            var pt = startPoint;
            path.MoveTo(pt);

            for (var i = 0; i < (CONSTANTS.HALF_TILE_HEIGHT - 1); i++)
            {
                path.MoveTo(pt += downOne);
                path.LineTo(pt += leftTwo);
            }

            canvas.DrawPath(path, paint);
        }

        //advance past bottom-right edge
        startPoint += new SKPoint(-CONSTANTS.HALF_TILE_WIDTH + 2, CONSTANTS.HALF_TILE_HEIGHT - 1);

        if (bottomLeft)
        {
            using var path = new SKPath();
            var pt = startPoint;
            path.MoveTo(pt);

            for (var i = 0; i < CONSTANTS.HALF_TILE_HEIGHT; i++)
            {
                path.LineTo(pt += leftTwo);
                path.MoveTo(pt += upOne);
            }

            canvas.DrawPath(path, paint);
        }

        //advance past bottom-left edge
        startPoint += new SKPoint(-CONSTANTS.HALF_TILE_WIDTH, -CONSTANTS.HALF_TILE_HEIGHT);
        startPoint += new SKPoint(2, 1); //correction

        //bridge the left corner notch when either adjacent edge is drawn
        if (bottomLeft || topLeft)
        {
            using var path = new SKPath();
            var pt = startPoint;
            path.MoveTo(pt);
            path.LineTo(pt + leftTwo);

            canvas.DrawPath(path, paint);
        }

        if (topLeft)
        {
            using var path = new SKPath();
            var pt = startPoint;
            path.MoveTo(pt);

            for (var i = 0; i < (CONSTANTS.HALF_TILE_HEIGHT - 1); i++)
            {
                path.MoveTo(pt += upOne);
                path.LineTo(pt += rightTwo);
            }

            canvas.DrawPath(path, paint);
        }
    }

    public static SKImage RenderTileOutline()
    {
        using var bitmap = new SKBitmap(CONSTANTS.TILE_WIDTH, 28);
        using var canvas = new SKCanvas(bitmap);

        Span<Point> vertices =
        [
            new(CONSTANTS.HALF_TILE_WIDTH, 0),
            new(CONSTANTS.TILE_WIDTH, CONSTANTS.HALF_TILE_HEIGHT),
            new(CONSTANTS.HALF_TILE_WIDTH, 28),
            new(0, CONSTANTS.HALF_TILE_HEIGHT)
        ];

        using var path = new SKPath();
        path.MoveTo(vertices[0].X, vertices[0].Y);

        foreach (var vertex in vertices[1..])
            path.LineTo(vertex.X, vertex.Y);

        path.Close();

        using var outline = new SKPaint();
        outline.Color = SKColors.DimGray;
        outline.Style = SKPaintStyle.Stroke;

        canvas.DrawPath(path, outline);

        return SKImage.FromBitmap(bitmap);
    }

    #region Background
    private static PaletteLookup? MptPaletteLookup;
    private static TileAnimationTable? BackgroundAnimationTable;
    private static Tileset? Tileset;
    private static Tileset? SnowTileset;
    #endregion

    #region Foreground
    private static TileAnimationTable? ForegroundAnimationTable;
    private static PaletteLookup? StcPaletteLookup;
    private static PaletteLookup? StsPaletteLookup;
    private static TileFlags[]? Sotp;
    #endregion
}