using System.IO;
using DALib.Data;
using DALib.Drawing;
using DALib.Extensions;
using DALib.Utility;
using SkiaSharp;
using Graphics = DALib.Drawing.Graphics;

namespace ChaosAssetManager.Helpers;

/// <summary>
///     Shared logic for the skill and spell icon sheets in setoa.dat. All six sheets share gui06.pal, so any
///     change to the palette has to be applied across every sheet at once.
/// </summary>
public static class IconUtil
{
    public const string ARCHIVE_FILE_NAME = "setoa.dat";
    public const string PALETTE_ENTRY_NAME = "gui06.pal";
    public const int ICON_SIZE = 31;

    //DataArchive.Save reads stream segments off the same BaseStream that Patch writes to, so an import running on a
    //worker and a delete running on the ui thread would interleave and write garbage over setoa.dat
    private static readonly Lock ArchiveLock = new();

    /// <summary>
    ///     Whether an import is currently holding the archive. Advisory only - the ui reads it to refuse a delete up
    ///     front instead of blocking; ArchiveLock is what actually guarantees the two never interleave.
    /// </summary>
    public static bool IsImportRunning
    {
        get => Volatile.Read(ref field);
        private set => Volatile.Write(ref field, value);
    }

    /// <summary>
    ///     Normal, learnable, and locked sheets for skills
    /// </summary>
    public static readonly string[] SkillSheets = ["skill001", "skill002", "skill003"];

    /// <summary>
    ///     Normal, learnable, and locked sheets for spells
    /// </summary>
    public static readonly string[] SpellSheets = ["spell001", "spell002", "spell003"];

    /// <summary>
    ///     Every icon sheet, in the order they are pooled for a palette rebuild
    /// </summary>
    public static readonly string[] AllSheets = [..SkillSheets, ..SpellSheets];

    /// <summary>
    ///     Returns the normal/learnable/locked sheet names for the requested family.
    /// </summary>
    public static string[] GetSheetNames(bool skill) => skill ? SkillSheets : SpellSheets;

    /// <summary>
    ///     Reads a sheet out of the archive. Assumes the entry is present - callers check first.
    /// </summary>
    public static EpfFile GetSheet(DataArchive archive, string sheetName) => EpfFile.FromEntry(archive[$"{sheetName}.epf"]);

    /// <summary>
    ///     Reads the shared gui06.pal out of the archive. Assumes the entry is present - callers check first.
    /// </summary>
    public static Palette GetPalette(DataArchive archive) => Palette.FromEntry(archive[PALETTE_ENTRY_NAME]);

    /// <summary>
    ///     Whether a frame holds no icon: absent, zero-length, degenerate at 1px or smaller in either dimension,
    ///     truncated below its declared pixel count, or entirely transparent indexes.
    /// </summary>
    public static bool IsBlank(EpfFrame? frame)
    {
        if (frame is null)
            return true;

        if ((frame.Data.Length == 0) || (frame.PixelWidth <= 1) || (frame.PixelHeight <= 1))
            return true;

        //widened because the shorts multiply past int - Data.Length is an int, so the check below rejects anything
        //that would not fit the slice
        var pixelCount = (long)frame.PixelWidth * frame.PixelHeight;

        //truncated data would index out of range when rendered
        if (frame.Data.Length < pixelCount)
            return true;

        //retail frames routinely carry a whole trailing block past their declared pixels, so only scan the icon itself
        return !frame.Data.AsSpan(0, (int)pixelCount)
                     .ContainsAnyExcept((byte)0);
    }

    /// <summary>
    ///     The placeholder written into a deleted slot. Keeps the slot index occupied so ids never shift.
    /// </summary>
    public static EpfFrame CreateBlankFrame()
        => new()
        {
            Top = 0,
            Left = 0,
            Bottom = 1,
            Right = 1,
            Data = [0]
        };

    /// <summary>
    ///     Renders a frame without the Left/Top padding Graphics.RenderImage applies, so the result is exactly
    ///     PixelWidth x PixelHeight and its pixel data can be written straight back into the frame.
    /// </summary>
    public static SKImage RenderFrameUnpadded(EpfFrame frame, Palette palette)
        => Graphics.RenderImage(
            new EpfFrame
            {
                Top = 0,
                Left = 0,
                Right = (short)frame.PixelWidth,
                Bottom = (short)frame.PixelHeight,
                Data = frame.Data
            },
            palette);

    /// <summary>
    ///     Scales an image to the icon cell size. Icons are full-cell art, so there is no trim-and-centre pass.
    /// </summary>
    /// <remarks>
    ///     Takes ownership of <paramref name="image" />. When a resize is needed the input is disposed and a new
    ///     instance is returned; when the input is already icon-sized, the same instance is returned untouched -
    ///     callers must not dispose their own reference after calling this.
    /// </remarks>
    public static SKImage ResizeToIcon(SKImage image)
    {
        if ((image.Width == ICON_SIZE) && (image.Height == ICON_SIZE))
            return image;

        using var source = SKBitmap.FromImage(image);
        using var resized = source.Resize(new SKImageInfo(ICON_SIZE, ICON_SIZE), new SKSamplingOptions(SKFilterMode.Nearest));

        var result = SKImage.FromBitmap(resized);
        image.Dispose();

        return result;
    }

    /// <summary>
    ///     Loads every png in a directory, sorted by filename. Undecodable files are skipped. ImportIcons does the
    ///     resize to the icon cell size, so the images come back at their source dimensions.
    /// </summary>
    public static List<SKImage> LoadSourceImages(string directory)
        => Directory.EnumerateFiles(directory, "*.png")
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .Select(SKImage.FromEncodedData)
                    .OfType<SKImage>()
                    .ToList();

    /// <summary>
    ///     Derives the recolour that turns a normal icon into its learnable (variant 2) or locked (variant 3) form,
    ///     by majority vote over every pixel of every icon already installed. Expressed in colour space rather than
    ///     palette indexes so it stays valid after gui06.pal is rebuilt.
    /// </summary>
    public static Dictionary<SKColor, SKColor> BuildVariantColorMap(DataArchive archive, Palette palette, int variant)
    {
        var tallies = new Dictionary<SKColor, Dictionary<SKColor, int>>();

        foreach (var family in new[] { SkillSheets, SpellSheets })
        {
            var normalEntryName = $"{family[0]}.epf";
            var variantEntryName = $"{family[variant - 1]}.epf";

            if (!archive.Contains(normalEntryName) || !archive.Contains(variantEntryName))
                continue;

            var normal = GetSheet(archive, family[0]);
            var recoloured = GetSheet(archive, family[variant - 1]);
            var frameCount = Math.Min(normal.Count, recoloured.Count);

            for (var index = 0; index < frameCount; index++)
            {
                var normalFrame = normal[index];
                var variantFrame = recoloured[index];

                //geometry, not buffer length - retail frames read back with a trailing block past their declared
                //pixels, so equal lengths would skip nearly every pair and unequal ones can still align pixel for pixel
                if ((normalFrame.PixelWidth != variantFrame.PixelWidth) || (normalFrame.PixelHeight != variantFrame.PixelHeight))
                    continue;

                var source = normalFrame.Data;
                var target = variantFrame.Data;
                var pixelCount = normalFrame.PixelWidth * normalFrame.PixelHeight;

                if ((source.Length < pixelCount) || (target.Length < pixelCount))
                    continue;

                for (var pixel = 0; pixel < pixelCount; pixel++)
                {
                    //index 0 is transparent in both, so it carries no colour information
                    if ((source[pixel] == 0) || (target[pixel] == 0))
                        continue;

                    var sourceColor = palette[source[pixel]];
                    var targetColor = palette[target[pixel]];

                    if (!tallies.TryGetValue(sourceColor, out var counts))
                        tallies[sourceColor] = counts = new Dictionary<SKColor, int>();

                    counts[targetColor] = counts.GetValueOrDefault(targetColor) + 1;
                }
            }
        }

        return tallies.ToDictionary(
            entry => entry.Key,
            entry => entry.Value
                          .MaxBy(count => count.Value)
                          .Key);
    }

    /// <summary>
    ///     Recolours an image through a variant colour map. Colours the map has never seen fall back to the nearest
    ///     mapped source colour by squared RGB distance. An empty map leaves the image unchanged.
    /// </summary>
    public static SKImage ApplyVariantColorMap(SKImage image, Dictionary<SKColor, SKColor> map)
    {
        using var source = SKBitmap.FromImage(image);
        var result = new SKBitmap(source.Width, source.Height);
        var resolved = new Dictionary<SKColor, SKColor>();

        for (var y = 0; y < source.Height; y++)
            for (var x = 0; x < source.Width; x++)
            {
                var pixel = source.GetPixel(x, y);

                //new SKBitmap starts fully transparent, so transparent pixels need no write
                if (pixel.Alpha == 0)
                    continue;

                var opaque = pixel.WithAlpha(byte.MaxValue);

                if (!resolved.TryGetValue(opaque, out var mapped))
                {
                    if (!map.TryGetValue(opaque, out mapped))
                        mapped = NearestMappedColor(opaque, map);

                    resolved[opaque] = mapped;
                }

                result.SetPixel(x, y, mapped);
            }

        using (result)
            return SKImage.FromBitmap(result);
    }

    private static SKColor NearestMappedColor(SKColor color, Dictionary<SKColor, SKColor> map)
    {
        var best = color;
        var bestDistance = int.MaxValue;

        foreach ((var source, var mapped) in map)
        {
            var deltaRed = source.Red - color.Red;
            var deltaGreen = source.Green - color.Green;
            var deltaBlue = source.Blue - color.Blue;
            var distance = deltaRed * deltaRed + deltaGreen * deltaGreen + deltaBlue * deltaBlue;

            if (distance >= bestDistance)
                continue;

            bestDistance = distance;
            best = mapped;
        }

        return best;
    }

    /// <summary>
    ///     Blanks the given icon ids across all three sheets of a family, then saves the archive. Frame counts are
    ///     left alone so the blanked ids stay available for the next import.
    /// </summary>
    public static void DeleteIcons(
        DataArchive archive,
        string archivePath,
        bool skill,
        IReadOnlyList<int> ids)
    {
        lock (ArchiveLock)
        {
            foreach (var sheetName in GetSheetNames(skill))
            {
                var entryName = $"{sheetName}.epf";

                if (!archive.Contains(entryName))
                    continue;

                var sheet = GetSheet(archive, sheetName);

                foreach (var id in ids)
                {
                    if ((id < 0) || (id >= sheet.Count))
                        continue;

                    sheet[id] = CreateBlankFrame();
                }

                archive.Patch(entryName, sheet);
            }

            archive.Save(archivePath);
        }
    }

    /// <summary>
    ///     Imports icons into a family: derives the learnable and locked variants, places them into blank ids before
    ///     appending, rebuilds gui06.pal across every sheet, and saves the archive. Returns the number of icons written.
    ///     Takes ownership of sourceImages as they are resized - callers must not use or dispose them afterward. Every
    ///     image this method holds is disposed before returning, on all paths.
    /// </summary>
    public static int ImportIcons(
        DataArchive archive,
        string archivePath,
        bool skill,
        IReadOnlyList<SKImage> sourceImages)
    {
        if (sourceImages.Count == 0)
            return 0;

        //ResizeToIcon takes ownership and may return the same instance, so sourceImages stays owned either way
        var resizedImages = sourceImages.Select(ResizeToIcon).ToList();

        //owned is seeded before the validation so the throws below dispose too - it only touches memory, never the archive
        var owned = new HashSet<SKImage>(resizedImages);

        //held for the whole method, so a delete arriving mid-import blocks instead of interleaving with Patch/Save
        using var archiveScope = ArchiveLock.EnterScope();

        IsImportRunning = true;

        try
        {
            //fail fast before any mutation - a half-rebuilt palette across six sheets is worse than not rebuilding at all
            if (!archive.Contains(PALETTE_ENTRY_NAME))
                throw new InvalidOperationException($"{PALETTE_ENTRY_NAME} is missing from {ARCHIVE_FILE_NAME}");

            foreach (var sheetName in AllSheets)
            {
                var entryName = $"{sheetName}.epf";

                if (!archive.Contains(entryName))
                    throw new InvalidOperationException($"{entryName} is missing from {ARCHIVE_FILE_NAME}");
            }

            var currentPalette = GetPalette(archive);

            //derive the recolours from the icons already installed, while the current palette still applies
            var learnableMap = BuildVariantColorMap(archive, currentPalette, 2);
            var lockedMap = BuildVariantColorMap(archive, currentPalette, 3);

            //gui06.pal is shared, so every sheet gets re-indexed even when only one family gains icons
            var sheets = AllSheets.ToDictionary(name => name, name => GetSheet(archive, name));
            var targetSheetNames = GetSheetNames(skill);
            var normalSheet = sheets[targetSheetNames[0]];

            //fill blanks lowest-first, then append past the end
            var placements = new List<int>();

            for (var id = 0; (id < normalSheet.Count) && (placements.Count < resizedImages.Count); id++)
                if (IsBlank(normalSheet[id]))
                    placements.Add(id);

            var appendId = normalSheet.Count;

            while (placements.Count < resizedImages.Count)
                placements.Add(appendId++);

            //grow all three sheets of the family together so frame counts stay equal
            foreach (var sheetName in targetSheetNames)
            {
                var sheet = sheets[sheetName];

                while (sheet.Count < appendId)
                    sheet.Add(CreateBlankFrame());
            }

            var pooled = new List<SKImage>();
            var slots = new List<(string SheetName, int Id)>();

            foreach (var sheetName in AllSheets)
            {
                var sheet = sheets[sheetName];

                for (var id = 0; id < sheet.Count; id++)
                {
                    if (IsBlank(sheet[id]))
                        continue;

                    var rendered = RenderFrameUnpadded(sheet[id], currentPalette);
                    pooled.Add(rendered);
                    owned.Add(rendered);
                    slots.Add((sheetName, id));
                }
            }

            var existingCount = pooled.Count;

            foreach (var image in resizedImages)
            {
                var learnable = ApplyVariantColorMap(image, learnableMap);
                owned.Add(learnable);

                var locked = ApplyVariantColorMap(image, lockedMap);
                owned.Add(locked);

                pooled.Add(image);
                pooled.Add(learnable);
                pooled.Add(locked);
            }

            for (var index = 0; index < resizedImages.Count; index++)
                for (var variant = 0; variant < targetSheetNames.Length; variant++)
                    slots.Add((targetSheetNames[variant], placements[index]));

            ImageProcessor.PreserveNonTransparentBlacks(pooled);

            //PreserveNonTransparentBlacks replaces entries in place without disposing what it replaced
            owned.UnionWith(pooled);

            using var quantized = ImageProcessor.QuantizeMultiple(QuantizerOptions.Default, pooled.ToArray());
            (var images, var newPalette) = quantized;

            for (var index = 0; index < slots.Count; index++)
            {
                (var sheetName, var id) = slots[index];
                var sheet = sheets[sheetName];
                var data = images[index]
                    .GetPalettizedPixelData(newPalette);

                if (index < existingCount)

                    //existing frame keeps its geometry, only the indexes change
                    sheet[id].Data = data;
                else
                    sheet[id] = new EpfFrame
                    {
                        Top = 0,
                        Left = 0,
                        Right = ICON_SIZE,
                        Bottom = ICON_SIZE,
                        Data = data
                    };
            }

            foreach (var sheetName in AllSheets)
                archive.Patch($"{sheetName}.epf", sheets[sheetName]);

            archive.Patch(PALETTE_ENTRY_NAME, newPalette);
            archive.Save(archivePath);

            return resizedImages.Count;
        } finally
        {
            IsImportRunning = false;

            foreach (var image in owned)
                image.Dispose();
        }
    }
}
