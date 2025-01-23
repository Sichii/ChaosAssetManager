using Chaos.Extensions.Common;
using ChaosAssetManager.Model;
using DALib.Drawing;
using DALib.Utility;
using Graphics = DALib.Drawing.Graphics;

namespace ChaosAssetManager.Helpers;

public static class MapEditorRenderUtil
{
    private static PaletteLookup? MptPaletteLookup;
    private static TileAnimationTable? BackgroundAnimationTable;
    
    public static Animation? RenderAnimatedBackground(int tileIndex, bool snowTileSet = false)
    {
        try
        {
            var archiveRoot = PathHelper.Instance.MapEditorArchivePath!;
            var archive = ArchiveCache.GetArchive(archiveRoot, "seo.dat");
            
            var tileSetName = snowTileSet ? "tileas.bmp" : "tilea.bmp";
            var tileset = Tileset.FromArchive(tileSetName, archive);
            MptPaletteLookup ??= PaletteLookup.FromArchive("mpt", archive);
            BackgroundAnimationTable ??= TileAnimationTable.FromArchive("gndani", archive);
            List<int> tileIndexes = [tileIndex];

            if (BackgroundAnimationTable.TryGetEntry(tileIndex, out var animationEntry))
                tileIndexes = animationEntry.TileSequence
                                            .Select(i => (int)i)
                                            .ToList();

            var transformer = tileIndexes.Select(
                index =>
                {
                    var tile = tileset[index];
                    var palette = MptPaletteLookup.GetPaletteForId(index + 1);
                    var image = Graphics.RenderTile(tile, palette);

                    return image;
                });

            var frames = new SKImageCollection(transformer);

            if (frames.IsNullOrEmpty())
                return null;

            return new Animation(frames, animationEntry?.AnimationIntervalMs);
        } catch
        {
            return null;
        }
    }
}