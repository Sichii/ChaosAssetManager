using System.Collections.Frozen;
using ChaosAssetManager.Model;
using DALib.Data;
using DALib.Drawing;
using DALib.Utility;
using Graphics = DALib.Drawing.Graphics;

namespace ChaosAssetManager.Helpers;

public static partial class RenderUtil
{
    private static IDictionary<int, Palette>? SetoaFieldPaletteLookup;
    private static IDictionary<int, Palette>? SetoaGuiPaletteLookup;
    private static IDictionary<int, Palette>? SetoaNslPaletteLookup;

    public static Animation? RenderSetoaFieldEpf(DataArchive archive, DataArchiveEntry entry)
    {
        try
        {
            var epfFile = EpfFile.FromEntry(entry);
            var paletteLookup = SetoaFieldPaletteLookup;

            if (paletteLookup is null)
            {
                SetoaFieldPaletteLookup ??= Palette.FromArchive("field", archive);

                //have to manually set 0 to field000.pal, since for some reason there's a fielde00.pal
                var field0Pal = Palette.FromEntry(archive["field000.pal"]);
                SetoaFieldPaletteLookup[0] = field0Pal;
                SetoaFieldPaletteLookup = SetoaFieldPaletteLookup.ToFrozenDictionary();

                paletteLookup = SetoaFieldPaletteLookup;
            }

            if (!entry.TryGetNumericIdentifier(out var identifier))
                return null;

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

    public static Animation? RenderSetoaGuiEpf(DataArchive archive, DataArchiveEntry entry, int palNum)
    {
        try
        {
            var epfFile = EpfFile.FromEntry(entry);

            var paletteLookup = SetoaGuiPaletteLookup ??= Palette.FromArchive("gui", archive)
                                                                 .ToFrozenDictionary();

            if (!paletteLookup.TryGetValue(palNum, out var palette))
                return null;

            var transformer = epfFile.Select(frame => Graphics.RenderImage(frame, palette));
            var frames = new SKImageCollection(transformer);

            return new Animation(frames);
        } catch
        {
            return null;
        }
    }

    public static Animation? RenderSetoaNslEpf(DataArchive archive, DataArchiveEntry entry, int palNum)
    {
        try
        {
            var epfFile = EpfFile.FromEntry(entry);

            var paletteLookup = SetoaNslPaletteLookup ??= Palette.FromArchive("nsl", archive)
                                                                 .ToFrozenDictionary();

            if (!paletteLookup.TryGetValue(palNum, out var palette))
                return null;

            var transformer = epfFile.Select(frame => Graphics.RenderImage(frame, palette));
            var frames = new SKImageCollection(transformer);

            return new Animation(frames);
        } catch
        {
            return null;
        }
    }

    public static Animation? RenderSetoaGuiGridEpf(DataArchive archive, DataArchiveEntry entry, int palNum)
    {
        try
        {
            var epfFile = EpfFile.FromEntry(entry);

            var paletteLookup = SetoaGuiPaletteLookup ??= Palette.FromArchive("gui", archive)
                                                                 .ToFrozenDictionary();

            if (!paletteLookup.TryGetValue(palNum, out var palette))
                return null;

            var images = epfFile.Select(frame => Graphics.RenderImage(frame, palette));
            using var frames = new SKImageCollection(images);
            var grid = CreateGrid(frames);

            return new Animation(new SKImageCollection([grid]));
        } catch
        {
            return null;
        }
    }
}