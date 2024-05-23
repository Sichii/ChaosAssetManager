using System.Collections.Frozen;
using ChaosAssetManager.Model;
using DALib.Data;
using DALib.Drawing;
using DALib.Utility;
using Graphics = DALib.Drawing.Graphics;

namespace ChaosAssetManager.Helpers;

public static partial class RenderUtil
{
    private static Animation? RenderLegend01Epf(DataArchive archive, DataArchiveEntry entry)
    {
        try
        {
            var epfFile = EpfFile.FromEntry(entry);
            var palette = Legend01Palette ??= Palette.FromEntry(archive["legend01.pal"]);
            var transformer = epfFile.Select(frame => Graphics.RenderImage(frame, palette));
            using var images = new SKImageCollection(transformer);
            var grid = CreateGrid(images);

            return new Animation(new SKImageCollection([grid]));
        } catch
        {
            return null;
        }
    }

    public static Animation? RenderLegendBackstoryEpf(DataArchive archive, DataArchiveEntry entry)
    {
        try
        {
            if (!entry.TryGetNumericIdentifier(out var identifier))
                return null;

            var paletteLookup = BackstoryPaletteLookup ??= Palette.FromArchive("backpal", archive)
                                                                  .ToFrozenDictionary();

            if (!paletteLookup.TryGetValue(identifier, out var palette))
                return null;

            var epfFile = EpfFile.FromEntry(entry);
            var transformer = epfFile.Select(frame => Graphics.RenderImage(frame, palette));
            var frames = new SKImageCollection(transformer);

            return new Animation(frames);
        } catch
        {
            return null;
        }
    }

    public static Animation? RenderLegendEpf(DataArchive archive, DataArchiveEntry entry)
    {
        try
        {
            var epfFile = EpfFile.FromEntry(entry);
            var palette = LegendPalette ??= Palette.FromEntry(archive["staff.pal"]);
            var transformer = epfFile.Select(frame => Graphics.RenderImage(frame, palette));
            var frames = new SKImageCollection(transformer);

            return new Animation(frames);
        } catch
        {
            return null;
        }
    }

    public static Animation? RenderLegendFieldEpf(DataArchive archive, DataArchiveEntry entry)
    {
        try
        {
            var epfFile = EpfFile.FromEntry(entry);

            if (!entry.TryGetNumericIdentifier(out var identifier))
                return null;

            var paletteLookup = LegendFieldPaletteLookup ??= Palette.FromArchive("field", archive)
                                                                    .ToFrozenDictionary();

            if (!paletteLookup.TryGetValue(identifier, out var palette))
                return null;

            var transformer = epfFile.Select(frame => Graphics.RenderImage(frame, palette));
            var frames = new SKImageCollection(transformer);

            return new Animation(frames);
        } catch
        {
            return null;
        }
    }

    public static Animation? RenderLegendItemEpf(DataArchive archive, DataArchiveEntry entry)
    {
        try
        {
            var epfFile = EpfFile.FromEntry(entry);

            if (!entry.TryGetNumericIdentifier(out var identifier))
                return null;

            var paletteLookup = ItemPaletteLookup ??= PaletteLookup.FromArchive("itempal", "item", archive)
                                                                   .Freeze();

            var transformer = epfFile.Select(
                frame =>
                {
                    var itemId = identifier * 266;
                    var palette = paletteLookup.GetPaletteForId(itemId);

                    return Graphics.RenderImage(frame, palette);
                });
            using var images = new SKImageCollection(transformer);
            var grid = CreateGrid(images);

            return new Animation(new SKImageCollection([grid]));
        } catch
        {
            return null;
        }
    }

    private static Animation? RenderLegendStaffEpf(DataArchive archive, DataArchiveEntry entry)
    {
        try
        {
            var epfFile = EpfFile.FromEntry(entry);
            var palette = StaffPalette ??= Palette.FromEntry(archive["legend.pal"]);
            var transformer = epfFile.Select(frame => Graphics.RenderImage(frame, palette));
            var frames = new SKImageCollection(transformer);

            return new Animation(frames);
        } catch
        {
            return null;
        }
    }
}