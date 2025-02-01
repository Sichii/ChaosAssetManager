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

    public static bool IsWall(int tileIndex)
    {
        if (tileIndex == 0)
            return false;

        tileIndex--;

        Sotp ??= ArchiveCache.GetArchive(PathHelper.Instance.MapEditorArchivePath!, "ia.dat")["sotp.dat"]
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
            var archiveRoot = PathHelper.Instance.MapEditorArchivePath!;
            var archive = ArchiveCache.GetArchive(archiveRoot, "seo.dat");
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

            var transformer = tileIndexes.Select(
                                             index => mapImageCache.BackgroundCache.GetOrCreate(
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
            var archiveRoot = PathHelper.Instance.MapEditorArchivePath!;
            var archive = ArchiveCache.GetArchive(archiveRoot, "ia.dat");
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

            var transformer = tileIndexes.Select(
                                             index => mapImageCache.ForegroundCache.GetOrCreate(
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

                                                     var transparent = Sotp[localIndex]
                                                         .HasFlag(TileFlags.Transparent);

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

        using var fill = new SKPaint();
        fill.Color = SKColors.Snow.WithAlpha(128);
        fill.Style = SKPaintStyle.Fill;

        using var outline = new SKPaint();
        outline.Color = SKColors.DimGray;
        outline.Style = SKPaintStyle.Stroke;

        canvas.DrawPath(path, fill);
        canvas.DrawPath(path, outline);

        TabWallImage = SKImage.FromBitmap(bitmap);

        return TabWallImage;
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