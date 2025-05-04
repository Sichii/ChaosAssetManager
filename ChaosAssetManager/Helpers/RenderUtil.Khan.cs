using System.Diagnostics.CodeAnalysis;
using System.IO;
using Chaos.Extensions.Common;
using ChaosAssetManager.Model;
using DALib.Data;
using DALib.Definitions;
using DALib.Drawing;
using DALib.Utility;
using Graphics = DALib.Drawing.Graphics;

namespace ChaosAssetManager.Helpers;

public static partial class RenderUtil
{
    private static PaletteLookup? KhanBPaletteLookup;
    private static PaletteLookup? KhanCPaletteLookup;
    private static PaletteLookup? KhanEPaletteLookup;
    private static PaletteLookup? KhanFPaletteLookup;
    private static PaletteLookup? KhanHPaletteLookup;
    private static PaletteLookup? KhanIPaletteLookup;
    private static PaletteLookup? KhanLPaletteLookup;
    private static PaletteLookup? KhanPPaletteLookup;
    private static PaletteLookup? KhanUPaletteLookup;
    private static PaletteLookup? KhanWPaletteLookup;
    private static IDictionary<int, Palette>? KhanMPaletteLookup;

    private static Animation? RenderKhanEpf(DataArchiveEntry entry, string archiveRoot)
    {
        var letter = entry.EntryName[1]
                          .ToString()
                          .ToLower();

        var male = entry.EntryName.StartsWithI("m");

        letter = letter switch
        {
            "a" => "b",
            "g" => "c",
            "j" => "c",
            "o" => "m",
            "s" => "p",
            _   => letter
        };

        if (letter.EqualsI("m"))
            return RenderKhanMEpf(entry, archiveRoot);

        if (letter.EqualsI("n"))
            return RenderKhanNEpf(entry, archiveRoot);

        if (!TryLoadKhanPaletteLookup(archiveRoot, letter, out var lookup))
            return null;

        if (!entry.TryGetNumericIdentifier(out var identifier, 3))
            return null;

        var overrideType = male ? KhanPalOverrideType.Male : KhanPalOverrideType.Female;
        var palette = lookup.GetPaletteForId(identifier, overrideType);
        var epfFile = EpfFile.FromEntry(entry);
        var transformer = epfFile.Select(frame => Graphics.RenderImage(frame, palette));
        var images = new SKImageCollection(transformer);

        return new Animation(images, 200);
    }

    private static Animation? RenderKhanMEpf(DataArchiveEntry entry, string archiveRoot)
    {
        if (!TryLoadKhanMPaletteLookup(archiveRoot, out var lookup))
            return null;

        var palettes = lookup.Values;
        var epfFile = EpfFile.FromEntry(entry);

        //we're rendering the frames in a grid, but those frames might have different placements / widths / heights
        var maxWidth = epfFile.Max(frame => frame.PixelWidth + frame.Left);
        var maxHeight = epfFile.Max(frame => frame.PixelHeight + frame.Left);

        var transformer = epfFile.Select(frame =>
        {
            //so we add padding to each frame so that all frames line up in the grid during the animation
            var paddingX = maxWidth - (frame.PixelWidth + frame.Left) + 2;
            var paddingY = maxHeight - (frame.PixelHeight + frame.Left) + 2;

            var transformer2 = palettes.Select(palette => Graphics.RenderImage(frame, palette));
            using var images = new SKImageCollection(transformer2);

            return CreateGrid(images, paddingX, paddingY);
        });
        var images = new SKImageCollection(transformer);

        return new Animation(images, 200);
    }

    private static Animation? RenderKhanNEpf(DataArchiveEntry entry, string archiveRoot)
    {
        if (!TryLoadDyePalettes(archiveRoot, out var lookup))
            return null;

        var epfFile = EpfFile.FromEntry(entry);

        //we're rendering the frames in a grid, but those frames might have different placements / widths / heights
        var maxWidth = epfFile.Max(frame => frame.PixelWidth + frame.Left);
        var maxHeight = epfFile.Max(frame => frame.PixelHeight + frame.Left);

        var transformer = epfFile.Select(frame =>
        {
            //so we add padding to each frame so that all frames line up in the grid during the animation
            var paddingX = maxWidth - (frame.PixelWidth + frame.Left) + 2;
            var paddingY = maxHeight - (frame.PixelHeight + frame.Left) + 2;

            //pants use dye colors, but only 0-15
            //after 15, the color would interfere with the body shape, so it's impossible
            var transformer2 = Enumerable.Range(0, 16)
                                         .Select(dyeIndex =>
                                         {
                                             //we want to create 1 image in the grid per dye color
                                             var palette = lookup[dyeIndex];

                                             return Graphics.RenderImage(frame, palette);
                                         });

            using var images = new SKImageCollection(transformer2);

            return CreateGrid(images, paddingX, paddingY);
        });

        var images = new SKImageCollection(transformer);

        return new Animation(images, 200);
    }

    private static bool TryLoadKhanMPaletteLookup(string archiveRoot, [NotNullWhen(true)] out IDictionary<int, Palette>? lookup)
    {
        //these palettes have no table, and represent the different body colors
        //the numeric identifiers of the palettes line up with the numeric values for body colors in the game
        lookup = KhanMPaletteLookup;

        if (lookup is not null)
            return true;

        var khanPalPath = Path.Combine(archiveRoot, "khanpal.dat");

        if (!File.Exists(khanPalPath))
        {
            lookup = null;

            return false;
        }

        using var kahnPal = DataArchive.FromFile(khanPalPath);
        lookup = KhanMPaletteLookup = Palette.FromArchive("palm", kahnPal);

        return true;
    }

    private static bool TryLoadKhanPaletteLookup(string archiveRoot, string letter, [NotNullWhen(true)] out PaletteLookup? lookup)
    {
        letter = letter.ToLower();
        var khanPalPath = Path.Combine(archiveRoot, "khanpal.dat");

        if (!File.Exists(khanPalPath))
        {
            lookup = null;

            return false;
        }

        var khanPal = new Lazy<DataArchive>(() => DataArchive.FromFile(khanPalPath));

        try
        {
            var pattern = $"pal{letter}";

            lookup = letter switch
            {
                "b" => KhanBPaletteLookup ??= PaletteLookup.FromArchive(pattern, khanPal.Value)
                                                           .Freeze(),
                "c" => KhanCPaletteLookup ??= PaletteLookup.FromArchive(pattern, khanPal.Value)
                                                           .Freeze(),
                "e" => KhanEPaletteLookup ??= PaletteLookup.FromArchive(pattern, khanPal.Value)
                                                           .Freeze(),
                "f" => KhanFPaletteLookup ??= PaletteLookup.FromArchive(pattern, khanPal.Value)
                                                           .Freeze(),
                "h" => KhanHPaletteLookup ??= PaletteLookup.FromArchive(pattern, khanPal.Value)
                                                           .Freeze(),
                "i" => KhanIPaletteLookup ??= PaletteLookup.FromArchive(pattern, khanPal.Value)
                                                           .Freeze(),
                "l" => KhanLPaletteLookup ??= PaletteLookup.FromArchive(pattern, khanPal.Value)
                                                           .Freeze(),
                "p" => KhanPPaletteLookup ??= PaletteLookup.FromArchive(pattern, khanPal.Value)
                                                           .Freeze(),
                "u" => KhanUPaletteLookup ??= PaletteLookup.FromArchive(pattern, khanPal.Value)
                                                           .Freeze(),
                "w" => KhanWPaletteLookup ??= PaletteLookup.FromArchive(pattern, khanPal.Value)
                                                           .Freeze(),
                _ => null
            };

            return lookup != null;
        } finally
        {
            if (khanPal.IsValueCreated)
                khanPal.Value.Dispose();
        }
    }
}