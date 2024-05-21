using System.Collections.Frozen;
using System.IO;
using System.Text;
using DALib.Data;
using DALib.Definitions;
using DALib.Drawing;
using DALib.Utility;
using SkiaSharp;
using Graphics = DALib.Drawing.Graphics;

namespace ChaosAssetManager.Helpers;

public static class RenderUtil
{
    private static IDictionary<int, Palette>? MpfPaletteLookup;
    private static PaletteLookup? HpfPaletteLookup;

    public static AnimatedPreview? RenderBmp(DataArchiveEntry entry)
    {
        using var data = entry.ToStreamSegment();
        var image = SKImage.FromEncodedData(data);
        var frames = new SKImageCollection([image]);

        return new AnimatedPreview(frames);
    }

    public static AnimatedPreview? RenderEfa(DataArchiveEntry entry)
    {
        try
        {
            var efaFile = EfaFile.FromEntry(entry);
            var transformer = efaFile.Select(frame => Graphics.RenderImage(frame));
            var frames = new SKImageCollection(transformer);

            return new AnimatedPreview(frames, efaFile.FrameIntervalMs);
        } catch
        {
            return null;
        }
    }

    public static AnimatedPreview? RenderEpf(
        DataArchive archive,
        DataArchiveEntry entry,
        string archiveName,
        string archiveRoot)
    {


        return null;
    }

    public static AnimatedPreview? RenderHpf(DataArchive archive, DataArchiveEntry entry)
    {
        try
        {
            var hpfFile = HpfFile.FromEntry(entry);
            var paletteLookup = HpfPaletteLookup ??= PaletteLookup.FromArchive("stc", archive);

            if (!entry.TryGetNumericIdentifier(out var identifier, 5))
                return null;

            var palette = paletteLookup.GetPaletteForId(identifier);
            var image = Graphics.RenderImage(hpfFile, palette);
            var frames = new SKImageCollection([image]);

            return new AnimatedPreview(frames);
        } catch
        {
            return null;
        }
    }

    public static AnimatedPreview? RenderMpf(DataArchive archive, DataArchiveEntry entry)
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

            return new AnimatedPreview(frames);
        } catch
        {
            return null;
        }
    }

    public static AnimatedPreview? RenderSpf(DataArchiveEntry entry)
    {
        try
        {
            var spfFile = SpfFile.FromEntry(entry);

            var transformer = spfFile.Select(
                frame => spfFile.Format == SpfFormatType.Colorized
                    ? Graphics.RenderImage(frame)
                    : Graphics.RenderImage(frame, spfFile.PrimaryColors!));
            var frames = new SKImageCollection(transformer);

            return new AnimatedPreview(frames);
        } catch
        {
            return null;
        }
    }

    public static string RenderText(DataArchiveEntry entry)
    {
        var builder = new StringBuilder();
        using var reader = new StreamReader(entry.ToStreamSegment());

        builder.Append(reader.ReadToEnd());

        return builder.ToString();
    }
}