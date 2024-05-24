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
    private static Palette? StaffPalette;
    private static PaletteLookup? ItemPaletteLookup;
    private static TileAnimationTable? TileAnimationTable;

    private static SKImage CreateGrid(ICollection<SKImage> images, int paddingX = 2, int paddingY = 2)
    {
        var width = images.Max(image => image.Width);
        var height = images.Max(image => image.Height);
        var columns = Math.Min(20, images.Count);
        var rows = (int)(images.Count / (float)columns + 1);

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
            case "setoa.dat":
            {
                if (entry.EntryName.StartsWithI("field"))
                    return RenderSetoaFieldEpf(archive, entry);

                //exceptions to rules
                if (entry.EntryName.StartsWithI("dlgcre01"))
                    return RenderSetoaGuiEpf(archive, entry, 8);

                if (entry.EntryName.StartsWithI("gbicon02") || entry.EntryName.StartsWithI("mernum"))
                    return RenderSetoaGuiGridEpf(archive, entry, 0);

                if (entry.EntryName.StartsWithI("emot00") || entry.EntryName.StartsWithI("emotdlg"))
                    return RenderSetoaGuiEpf(archive, entry, 0);

                if (entry.EntryName.StartsWithI("lsbackm"))
                    return RenderSetoaGuiEpf(archive, entry, 0);

                if (entry.EntryName.StartsWithI("setup12")
                    || entry.EntryName.StartsWithI("setup13")
                    || entry.EntryName.StartsWithI("setup14"))
                    return RenderSetoaGuiEpf(archive, entry, 0);

                //1
                if (entry.EntryName.StartsWithI("gbicon12") || entry.EntryName.StartsWithI("orb"))
                    return RenderSetoaGuiEpf(archive, entry, 1);

                //2
                if (entry.EntryName.StartsWithI("gbicon01") || entry.EntryName.StartsWithI("gbicon03"))
                    return RenderSetoaGuiGridEpf(archive, entry, 2);

                //3
                if (entry.EntryName.StartsWithI("emot") || entry.EntryName.StartsWithI("equip02") || entry.EntryName.StartsWithI("mouse"))
                    return RenderSetoaGuiEpf(archive, entry, 3);

                if (entry.EntryName.StartsWithI("legends"))
                    return RenderSetoaGuiGridEpf(archive, entry, 3);

                //4
                if (entry.EntryName.StartsWithI("lback")
                    || entry.EntryName.StartsWithI("dlgcre")
                    || entry.EntryName.StartsWithI("lback")
                    || entry.EntryName.StartsWithI("lod0")
                    || entry.EntryName.StartsWithI("setup"))
                    return RenderSetoaGuiEpf(archive, entry, 4);

                //5
                if (entry.EntryName.StartsWithI("nation"))
                    return RenderSetoaGuiGridEpf(archive, entry, 5);

                //6
                if (entry.EntryName.StartsWithI("skill0") || entry.EntryName.StartsWithI("spell0"))
                    return RenderSetoaGuiGridEpf(archive, entry, 6);

                //7
                if (entry.EntryName.StartsWithI("lodbk"))
                    return RenderSetoaGuiEpf(archive, entry, 7);

                //8 dlgcre (at top)

                //9
                if (entry.EntryName.StartsWithI("staff"))
                    return RenderSetoaGuiEpf(archive, entry, 9);

                //10
                if (entry.EntryName.StartsWithI("lsback") || entry.EntryName.StartsWithI("lss"))
                    return RenderSetoaGuiEpf(archive, entry, 10);

                if (entry.EntryName.StartsWithI("leicon"))
                    return RenderSetoaGuiGridEpf(archive, entry, 10);

                //1
                if (entry.EntryName.StartsWithI("ldi"))
                    return RenderSetoaGuiEpf(archive, entry, 11);

                //12
                if (entry.EntryName.StartsWithI("lwmap") || entry.EntryName.StartsWithI("tmapv"))
                    return RenderSetoaGuiEpf(archive, entry, 12);

                //13
                if (entry.EntryName.StartsWithI("bw_back") || entry.EntryName.StartsWithI("bw_check"))
                    return RenderSetoaGuiEpf(archive, entry, 13);

                //14
                if (entry.EntryName.StartsWithI("kdesc") || entry.EntryName.StartsWithI("key") || entry.EntryName.StartsWithI("khotkey"))
                    return RenderSetoaGuiEpf(archive, entry, 14);

                //15
                if (entry.EntryName.StartsWithI("lg_"))
                    return RenderSetoaGuiEpf(archive, entry, 15);

                //16
                if (entry.EntryName.StartsWithI("bw_flag"))
                    return RenderSetoaGuiEpf(archive, entry, 16);

                //17
                if (entry.EntryName.StartsWithI("album_b") || entry.EntryName.EqualsI("album.epf"))
                    return RenderSetoaGuiEpf(archive, entry, 17);

                //default to 0
                return RenderSetoaGuiEpf(archive, entry, 0);
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

    public static void Reset()
    {
        MpfPaletteLookup = null;
        StcPaletteLookup = null;
        StsPaletteLookup = null;
        BackstoryPaletteLookup = null;
        LegendFieldPaletteLookup = null;
        LegendPalette = null;
        Legend01Palette = null;
        StaffPalette = null;
        ItemPaletteLookup = null;
        TileAnimationTable = null;
        RohEfctPaletteLookup = null;
        RohMefcPaletteLookup = null;
        RohMptPaletteLookup = null;
        RohMpsPaletteLookup = null;
        EffectTable = null;
        SetoaFieldPaletteLookup = null;
        SetoaGuiPaletteLookup = null;
        SetoaNslPaletteLookup = null;
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