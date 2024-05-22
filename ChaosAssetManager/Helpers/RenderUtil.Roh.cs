using ChaosAssetManager.Model;
using DALib.Data;
using DALib.Drawing;
using DALib.Utility;
using Graphics = DALib.Drawing.Graphics;

namespace ChaosAssetManager.Helpers;

public static partial class RenderUtil
{
    private static PaletteLookup? RohEfctPaletteLookup;
    private static PaletteLookup? RohMefcPaletteLookup;
    private static PaletteLookup? RohMptPaletteLookup;
    private static PaletteLookup? RohMpsPaletteLookup;
    private static EffectTable? EffectTable;

    public static Animation? RenderRohEfctEpf(DataArchive archive, DataArchiveEntry entry)
    {
        try
        {
            var epfFile = EpfFile.FromEntry(entry);

            if (!entry.TryGetNumericIdentifier(out var identifier))
                return null;

            var paletteLookup = RohEfctPaletteLookup ??= PaletteLookup.FromArchive("effpal", "eff", archive)
                                                                      .Freeze();
            var effectTable = EffectTable ??= EffectTable.FromArchive(archive);

            if (!effectTable.TryGetEntry(identifier, out var effectEntry))
                return null;

            var palette = paletteLookup.GetPaletteForId(identifier);

            // select frames as they are specified in the effect table
            var transformer = effectEntry.Select(frameIndex => Graphics.RenderImage(epfFile[frameIndex], palette));
            var frames = new SKImageCollection(transformer);

            return new Animation(frames);
        } catch
        {
            return null;
        }
    }

    public static Animation? RenderRohMefcEpf(DataArchive archive, DataArchiveEntry entry)
    {
        try
        {
            var epfFile = EpfFile.FromEntry(entry);

            if (!entry.TryGetNumericIdentifier(out var identifier))
                return null;

            var paletteLookup = RohMefcPaletteLookup ??= PaletteLookup.FromArchive("mefcpal", "mefc", archive)
                                                                      .Freeze();

            var palette = paletteLookup.GetPaletteForId(identifier);
            var transformer = epfFile.Select(frame => Graphics.RenderImage(frame, palette));
            var frames = new SKImageCollection(transformer);

            return new Animation(frames);
        } catch
        {
            return null;
        }
    }

    public static Animation? RenderRohTileABmp(DataArchive archive, DataArchiveEntry entry)
    {
        try
        {
            var tileSet = Tileset.FromArchive("tilea", archive);

            var paletteLookup = RohMptPaletteLookup ??= PaletteLookup.FromArchive("mpt", archive)
                                                                     .Freeze();

            return null;
        } catch
        {
            return null;
        }
    }
}