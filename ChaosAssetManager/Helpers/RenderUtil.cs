using System.Collections.Frozen;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Chaos.Extensions.Common;
using ChaosAssetManager.Model;
using DALib.Data;
using DALib.Definitions;
using DALib.Drawing;
using DALib.Utility;
using SkiaSharp;
using Application = System.Windows.Application;
using Brushes = System.Windows.Media.Brushes;
using Graphics = DALib.Drawing.Graphics;
using HorizontalAlignment = System.Windows.HorizontalAlignment;

namespace ChaosAssetManager.Helpers;

public static class RenderUtil
{
    private static IDictionary<int, Palette>? MpfPaletteLookup;
    private static PaletteLookup? StcPaletteLookup;
    private static PaletteLookup? StsPaletteLookup;
    private static IDictionary<int, Palette>? BackstoryPaletteLookup;
    private static IDictionary<int, Palette>? FieldPaletteLookup;
    private static Palette? LegendPalette;
    private static Palette? Legend01Palette;
    private static Palette? StaffPalette;
    private static PaletteLookup? ItemPaletteLookup;
    private static TileAnimationTable? TileAnimationTable;

    private static SKImage CreateGrid(ICollection<SKImage> images, int paddingX = 2, int paddingY = 2)
    {
        var width = images.Max(image => image.Width);
        var height = images.Max(image => image.Height);
        var rows = 14;
        var columns = 19;

        using var grid = new SKBitmap(width * columns + (columns - 1) * paddingX, height * rows + (rows - 1) * paddingY);
        using var canvas = new SKCanvas(grid);

        foreach ((var image, var index) in images.Select((image, i) => (image, i)))
        {
            var x = index % columns * (width + paddingX);
            var y = index / columns * (height + paddingY);

            canvas.DrawImage(image, x, y);
        }

        return SKImage.FromBitmap(grid);
    }

    public static Animation? RenderBmp(DataArchiveEntry entry)
    {
        using var data = entry.ToStreamSegment();
        var image = SKImage.FromEncodedData(data);

        if (image is null)
            return null;

        var frames = new SKImageCollection([image]);

        return new Animation(frames);
    }

    public static Animation? RenderEfa(DataArchiveEntry entry)
    {
        try
        {
            var efaFile = EfaFile.FromEntry(entry);
            var transformer = efaFile.Select(frame => Graphics.RenderImage(frame, efaFile.BlendingType));
            var frames = new SKImageCollection(transformer);

            return new Animation(frames, efaFile.FrameIntervalMs);
        } catch
        {
            return null;
        }
    }

    public static Animation? RenderHpf(DataArchive archive, DataArchiveEntry entry)
    {
        try
        {
            StcPaletteLookup ??= PaletteLookup.FromArchive("stc", archive)
                                              .Freeze();

            StsPaletteLookup ??= PaletteLookup.FromArchive("sts", archive)
                                              .Freeze();
            TileAnimationTable ??= TileAnimationTable.FromArchive("stcani", archive);

            var hpfFile = HpfFile.FromEntry(entry);
            List<HpfFile> hpfFiles = [hpfFile];
            PaletteLookup paletteLookup;
            TileAnimationEntry? tileAnimationEntry = null;

            //if we fail to get an identifier, return null
            if (!entry.TryGetNumericIdentifier(out var identifier))
                return null;

            if (entry.EntryName.StartsWithI("stc"))
            {
                //use stc palettes
                paletteLookup = StcPaletteLookup;

                //if there's an animation entry for this tile, get the tile sequence
                //load those hpf files from the archive and use those as the frames to render
                if (TileAnimationTable.TryGetEntry(identifier, out var aniEntry))
                    hpfFiles = aniEntry.Select(tileId => HpfFile.FromEntry(archive[$"stc{tileId:D5}.hpf"]))
                                       .ToList();
            } else
                paletteLookup = StsPaletteLookup;

            var palette = paletteLookup.GetPaletteForId(identifier + 1);
            var maxHeight = hpfFiles.Max(hpf => hpf.PixelHeight);

            var transformer = hpfFiles.Select(
                frame =>
                {
                    //since hpf files are rendered from the bottom up
                    //we need to offset the tops of short images so that all the bottoms align
                    var yOffset = maxHeight - frame.PixelHeight;

                    return Graphics.RenderImage(frame, palette, yOffset);
                });
            var frames = new SKImageCollection(transformer);

            //if there's an animation entry, use the interval from that
            return new Animation(frames, tileAnimationEntry?.AnimationIntervalMs);
        } catch
        {
            return null;
        }
    }

    public static Animation? RenderMpf(DataArchive archive, DataArchiveEntry entry)
    {
        try
        {
            var mpfFile = MpfFile.FromEntry(entry);

            var paletteLookup = MpfPaletteLookup ??= Palette.FromArchive("mns", archive)
                                                            .ToFrozenDictionary();

            if (!paletteLookup.TryGetValue(mpfFile.PaletteNumber, out var palette))
                return null;

            var transformer = mpfFile.Select(frame => Graphics.RenderImage(frame, palette));
            var frames = new SKImageCollection(transformer);

            return new Animation(frames);
        } catch
        {
            return null;
        }
    }

    public static Animation? RenderSpf(DataArchiveEntry entry)
    {
        try
        {
            var spfFile = SpfFile.FromEntry(entry);

            var transformer = spfFile.Select(
                frame => spfFile.Format == SpfFormatType.Colorized
                    ? Graphics.RenderImage(frame)
                    : Graphics.RenderImage(frame, spfFile.PrimaryColors!));
            var frames = new SKImageCollection(transformer);

            return new Animation(frames);
        } catch
        {
            return null;
        }
    }

    public static TextBlock RenderText(DataArchiveEntry entry)
    {
        var builder = new StringBuilder();
        using var reader = new StreamReader(entry.ToStreamSegment());

        builder.Append(reader.ReadToEnd());

        return new TextBlock
        {
            Text = builder.ToString(),
            TextWrapping = TextWrapping.Wrap,
            Style = Application.Current.Resources["MaterialDesignTextBlock"] as Style,
            Foreground = Brushes.White,
            Padding = new Thickness(10),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Margin = new Thickness(0)
        };
    }

    #region Epf Rendering
    public static Animation? RenderEpf(
        DataArchive archive,
        DataArchiveEntry entry,
        string archiveName,
        string archiveRoot)
    {
        switch (archiveName.ToLower())
        {
            case "legend.dat":
            {
                if (entry.EntryName.StartsWithI("bkstory"))
                    return RenderLegendBackstoryEpf(archive, entry);

                if (entry.EntryName.StartsWithI("item"))
                    return RenderLegendItemEpf(archive, entry);

                if (entry.EntryName.StartsWithI("field"))
                    return RenderLegendFieldEpf(archive, entry);

                if (entry.EntryName.StartsWithI("skill") || entry.EntryName.StartsWithI("spell"))
                    return RenderLegend01Epf(archive, entry);

                if (entry.EntryName.StartsWithI("staff"))
                    return RenderLegendStaffEpf(archive, entry);

                return RenderLegendEpf(archive, entry);
            }
        }

        return null;
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

    public static Animation? RenderLegendFieldEpf(DataArchive archive, DataArchiveEntry entry)
    {
        try
        {
            var epfFile = EpfFile.FromEntry(entry);

            if (!entry.TryGetNumericIdentifier(out var identifier))
                return null;

            var paletteLookup = FieldPaletteLookup ??= Palette.FromArchive("field", archive)
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
    #endregion
}