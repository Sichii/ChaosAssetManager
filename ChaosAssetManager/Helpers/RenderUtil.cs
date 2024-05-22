using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
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
using Control = System.Windows.Controls.Control;
using Graphics = DALib.Drawing.Graphics;
using HorizontalAlignment = System.Windows.HorizontalAlignment;

namespace ChaosAssetManager.Helpers;

public static partial class RenderUtil
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
        var entryName = entry.EntryName;

        //not real bmps
        if (entryName.EqualsI("tilea.bmp") || entryName.EqualsI("tileas.bmp"))
            return null;

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
            case "national.dat":
            {
                return RenderNationalEpf(archive, entry, archiveRoot);
            }
            case "roh.dat":
            {
                if (entry.EntryName.StartsWithI("efct"))
                    return RenderRohEfctEpf(archive, entry);

                if (entry.EntryName.StartsWithI("mefc"))
                    return RenderRohMefcEpf(archive, entry);

                return null;
            }
        }

        return null;
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

            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            var transformer = spfFile.Select(
                                         frame => spfFile.Format == SpfFormatType.Colorized
                                             ? Graphics.RenderImage(frame)
                                             : Graphics.RenderImage(frame, spfFile.PrimaryColors!))
                                     .Where(frame => frame is not null);

            var frames = new SKImageCollection(transformer);

            return new Animation(frames);
        } catch
        {
            return null;
        }
    }

    public static Control RenderText(DataArchiveEntry entry)
    {
        var builder = new StringBuilder();
        using var reader = new StreamReader(entry.ToStreamSegment());

        builder.Append(reader.ReadToEnd());

        var text = new TextBlock
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

        var scrollViewer = new ScrollViewer
        {
            CanContentScroll = true,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = text
        };

        return scrollViewer;
    }

    private static bool TryLoadLegendPalFromRoot(string archiveRoot, [NotNullWhen(true)] out Palette? legendPal)
    {
        legendPal = LegendPalette;

        if (legendPal is not null)
            return true;

        var legendPath = Path.Combine(archiveRoot, "legend.dat");

        if (!File.Exists(legendPath))
            return false;

        using var archive = DataArchive.FromFile(legendPath);
        LegendPalette = Palette.FromEntry(archive["legend.pal"]);
        legendPal = LegendPalette;

        return true;
    }
}