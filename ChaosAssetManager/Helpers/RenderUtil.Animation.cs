using System.IO;
using Chaos.Extensions.Common;
using ChaosAssetManager.Model;
using DALib.Data;
using DALib.Drawing;
using DALib.Utility;
using SkiaSharp;
using Graphics = DALib.Drawing.Graphics;

namespace ChaosAssetManager.Helpers;

public static partial class RenderUtil
{
    /// <summary>
    ///     Re-renders a data archive entry into an animation suitable for preview or export.
    ///     Returns null for non-renderable entry types (text, audio, seo.dat bitmaps).
    /// </summary>
    public static Animation? TryRenderAnimation(
        DataArchive archive,
        DataArchiveEntry entry,
        string archiveName,
        string archiveRoot)
    {
        var type = Path.GetExtension(entry.EntryName)
                       .ToLower();

        switch (type)
        {
            case ".pal":
                return RenderPalette(entry);
            case ".efa":
                return RenderEfa(entry);
            case ".spf":
                return RenderSpf(entry);
            case ".bmp":
            {
                //seo.dat bitmaps are not renderable as standalone frames
                if (archiveName.EqualsI("seo.dat"))
                    return null;

                return RenderBmp(entry);
            }
            case ".mpf":
                return RenderMpf(archive, entry);
            case ".epf":
                return RenderEpf(
                    archive,
                    entry,
                    archiveName,
                    archiveRoot);
            case ".hpf":
                return RenderHpf(archive, entry);
            default:
                return null;
        }
    }

    /// <summary>
    ///     Re-renders a single tile from a tilea/tileas atlas, including its gndani animation sequence
    ///     if one exists. Returns null when the archive lacks the required palette or tileset data.
    /// </summary>
    public static Animation? TryRenderTile(DataArchive archive, DataArchiveEntry entry, int tileIndex)
    {
        var palettePrefix = entry.EntryName.EqualsI("tilea.bmp") ? "mpt" : "mps";

        var tileset = Tileset.FromArchive(entry.EntryName, archive);
        var paletteLookup = PaletteLookup.FromArchive(palettePrefix, archive);
        var tileAnimationTable = TileAnimationTable.FromArchive("gndani", archive);

        if ((tileIndex < 0) || (tileIndex >= tileset.Count))
            return null;

        List<int> tileIndexes = [tileIndex];

        if (tileAnimationTable.TryGetEntry(tileIndex, out var animationEntry))
            tileIndexes = animationEntry.TileSequence
                                        .Select(i => (int)i)
                                        .ToList();

        var transformer = tileIndexes.Select(index =>
        {
            var tile = tileset[index];
            var palette = paletteLookup.GetPaletteForId(index + 1);
            var image = Graphics.RenderTile(tile, palette);

            return image;
        });

        var frames = new SKImageCollection(transformer);

        if (frames.IsNullOrEmpty())
            return null;

        return new Animation(frames, animationEntry?.AnimationIntervalMs);
    }
}
