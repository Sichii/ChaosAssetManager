using System.IO;
using System.Windows;
using Chaos.Extensions.Common;
using ChaosAssetManager.Helpers;
using DALib.Data;
using DALib.Definitions;
using DALib.Drawing;
using DALib.Extensions;
using DALib.Utility;
using Microsoft.Win32;
using SkiaSharp;

namespace ChaosAssetManager.Controls;

public sealed partial class PanelSpritesControl
{
    private const int ITEMS_PER_PAGE = 266;
    private const int TARGET_SIZE = 32;

    public PanelSpritesControl()
    {
        InitializeComponent();

        PathHelper.ArchivesPathChanged += () => PanelSpritesControl_OnLoaded(this, new RoutedEventArgs());
    }

    private void PanelSpritesControl_OnLoaded(object sender, RoutedEventArgs e)
    {
        if (!PathHelper.ArchivePathIsValid(PathHelper.Instance.ArchivesPath))
        {
            MainContent.Visibility = Visibility.Collapsed;
            InfoMessage.Visibility = Visibility.Collapsed;
            BottomInfoMessage.Visibility = Visibility.Collapsed;
            NotConfiguredMessage.Visibility = Visibility.Visible;
        }
        else
        {
            NotConfiguredMessage.Visibility = Visibility.Collapsed;
            MainContent.Visibility = Visibility.Visible;
            InfoMessage.Visibility = Visibility.Visible;
            BottomInfoMessage.Visibility = Visibility.Visible;
        }
    }

    private void BrowseInputBtn_OnClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select folder containing PNG panel sprites",
            InitialDirectory = PathHelper.Instance.PanelSpritesInputPath
        };

        if (dialog.ShowDialog() != true)
            return;

        var pngFiles = Directory.GetFiles(dialog.FolderName, "*.png");

        if (pngFiles.Length == 0)
        {
            Snackbar.MessageQueue!.Enqueue("Selected folder does not contain any PNG files");

            return;
        }

        InputPathTxt.Text = dialog.FolderName;
        PathHelper.Instance.PanelSpritesInputPath = dialog.FolderName;
        PathHelper.Instance.Save();
    }

    private async void ImportBtn_OnClick(object sender, RoutedEventArgs e)
    {
        var archivePath = PathHelper.Instance.ArchivesPath;
        var inputPath = InputPathTxt.Text;

        if (string.IsNullOrEmpty(archivePath) || !PathHelper.ArchivePathIsValid(archivePath))
        {
            Snackbar.MessageQueue!.Enqueue("Please set a valid Archives Directory in Settings (gear icon)");

            return;
        }

        if (string.IsNullOrEmpty(inputPath))
        {
            Snackbar.MessageQueue!.Enqueue("Please select an input path");

            return;
        }

        var legendPath = Path.Combine(archivePath, "legend.dat");

        if (!File.Exists(legendPath))
        {
            Snackbar.MessageQueue!.Enqueue("legend.dat not found in configured Archives Directory");

            return;
        }

        var hasDyeablePalette = DyeableToggle.IsChecked == true;

        ImportBtn.IsEnabled = false;

        try
        {
            var importedCount = await Task.Run(
                () =>
                {
                    var legend = ArchiveCache.Legend;

                    // Find all empty slots in existing EPF files
                    var emptySlots = FindEmptySlots(legend);

                    // Load and process input images
                    var inputImages = Directory.EnumerateFiles(inputPath, "*.png")
                        .Select(SKImage.FromEncodedData)
                        .Select(TrimResizeAndCenter)
                        .ToList();

                    // Group images by color count to respect palette limits
                    var imageGroups = GroupImagesByColorCount(inputImages);

                    // Create EPF files from image groups
                    var epfs = imageGroups.Select(group => EpfFile.FromImages(QuantizerOptions.Default, group))
                        .ToList();

                    if (hasDyeablePalette)
                    {
                        var colorTable = ColorTable.FromEntry(legend["color0.tbl"]);

                        for (var i = 0; i < epfs.Count; i++)
                            epfs[i] = epfs[i].ArrangeColorsForDyeableType(colorTable[0]);
                    }

                    var palettes = PaletteLookup.FromArchive("itempal", "item", legend);
                    var nextPaletteId = palettes.GetNextPaletteId();

                    // Track which EPF files we need to update
                    var modifiedEpfs = new Dictionary<int, EpfFile>();

                    // Get existing EPF entries for reference
                    var existingEntries = legend.GetEntries("item", ".epf")
                        .ToDictionary(
                            entry => entry.TryGetNumericIdentifier(out var id, 3) ? id : -1,
                            entry => entry);

                    var count = 0;
                    var emptySlotIndex = 0;

                    //overflow starts on a new page after all existing pages, since
                    //FindEmptySlots already accounts for every slot on existing pages
                    var maxExistingPageId = existingEntries.Keys.Where(k => k > 0)
                        .DefaultIfEmpty(0)
                        .Max();

                    var nextOverflowSlot = maxExistingPageId * ITEMS_PER_PAGE;

                    foreach (var palettizedEpf in epfs)
                    {
                        // Save the palette for this group
                        var paletteId = nextPaletteId++;
                        legend.Patch($"item{paletteId:D3}.pal", palettizedEpf.Palette);

                        foreach (var frame in palettizedEpf.Entity)
                        {
                            int globalSlot;

                            if (emptySlotIndex < emptySlots.Count)
                            {
                                //use an existing empty slot
                                globalSlot = emptySlots[emptySlotIndex++];
                            }
                            else
                            {
                                //all empty slots used, append after all existing pages
                                globalSlot = nextOverflowSlot++;
                            }

                            var pageId = (globalSlot / ITEMS_PER_PAGE) + 1;
                            var slotInPage = globalSlot % ITEMS_PER_PAGE;

                            // Get or create the EPF file for this page
                            if (!modifiedEpfs.TryGetValue(pageId, out var targetEpf))
                            {
                                if (existingEntries.TryGetValue(pageId, out var existingEntry))
                                    targetEpf = EpfFile.FromEntry(existingEntry);
                                else
                                    targetEpf = new EpfFile(TARGET_SIZE, TARGET_SIZE);

                                modifiedEpfs[pageId] = targetEpf;
                            }

                            // Ensure the EPF has enough slots
                            while (targetEpf.Count <= slotInPage)
                                targetEpf.Add(new EpfFrame { Data = [] });

                            // Set the frame
                            targetEpf[slotInPage] = frame;

                            // Update palette table (1-based slot numbers)
                            palettes.Table.Add(globalSlot + 1, paletteId);

                            count++;
                        }
                    }

                    // Save all modified EPF files
                    foreach ((var pageId, var modifiedEpf) in modifiedEpfs)
                        legend.Patch($"item{pageId:D3}.epf", modifiedEpf);

                    legend.Patch("itempal.tbl", palettes.Table);
                    legend.Save(legendPath);

                    return count;
                });

            Snackbar.MessageQueue!.Enqueue($"Successfully imported {importedCount} panel sprites");
        }
        catch (Exception ex)
        {
            Snackbar.MessageQueue!.Enqueue($"Error: {ex.Message}");
        }
        finally
        {
            ImportBtn.IsEnabled = true;
        }
    }

    private static List<int> FindEmptySlots(DataArchive archive)
    {
        var emptySlots = new List<int>();

        var entries = archive.GetEntries("item", ".epf")
            .OrderBy(entry => entry.TryGetNumericIdentifier(out var id) ? id : -1)
            .ToList();

        foreach (var entry in entries)
        {
            var pageId = entry.TryGetNumericIdentifier(out var id, 3) ? id : -1;

            if (pageId < 1)
                continue;

            var pageIndex = pageId - 1;
            var epf = EpfFile.FromEntry(entry);

            for (var slot = 0; slot < ITEMS_PER_PAGE; slot++)
            {
                var globalSlot = pageIndex * ITEMS_PER_PAGE + slot;
                var frame = epf.ElementAtOrDefault(slot);

                // Consider slot empty if: no frame, empty data, tiny dimensions, or all zeros
                if (frame is null
                    || (frame.Data.Length == 0)
                    || (frame.PixelWidth <= 1)
                    || (frame.PixelHeight <= 1)
                    || frame.Data.All(b => b == 0))
                    emptySlots.Add(globalSlot);
            }
        }

        // Sort to ensure we fill lowest slots first
        emptySlots.Sort();

        return emptySlots;
    }

    private static List<List<SKImage>> GroupImagesByColorCount(List<SKImage> images)
    {
        var groups = new List<List<SKImage>>();
        var currentGroup = new List<SKImage>();
        var currentColors = new HashSet<SKColor>();

        foreach (var image in images)
        {
            var imageColors = GetUniqueColors(image);

            var combinedColors = new HashSet<SKColor>(currentColors);

            combinedColors.UnionWith(imageColors);

            if ((combinedColors.Count > CONSTANTS.COLORS_PER_PALETTE) && (currentGroup.Count > 0))
            {
                groups.Add(currentGroup);
                currentGroup = [];
                currentColors.Clear();

                currentColors.UnionWith(imageColors);
            }
            else
                currentColors = combinedColors;

            currentGroup.Add(image);
        }

        if (currentGroup.Count > 0)
            groups.Add(currentGroup);

        return groups;
    }

    private static HashSet<SKColor> GetUniqueColors(SKImage image)
    {
        using var bitmap = SKBitmap.FromImage(image);
        var colors = new HashSet<SKColor>();

        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                var pixel = bitmap.GetPixel(x, y);

                if (pixel.Alpha > 0)
                    colors.Add(pixel.WithAlpha(255));
            }
        }

        return colors;
    }

    private static SKImage TrimResizeAndCenter(SKImage image)
    {
        using var sourceBitmap = SKBitmap.FromImage(image);

        var minX = sourceBitmap.Width;
        var minY = sourceBitmap.Height;
        var maxX = 0;
        var maxY = 0;

        for (var y = 0; y < sourceBitmap.Height; y++)
        {
            for (var x = 0; x < sourceBitmap.Width; x++)
            {
                if (sourceBitmap.GetPixel(x, y).Alpha > 0)
                {
                    minX = Math.Min(minX, x);
                    minY = Math.Min(minY, y);
                    maxX = Math.Max(maxX, x);
                    maxY = Math.Max(maxY, y);
                }
            }
        }

        if ((minX > maxX) || (minY > maxY))
        {
            var blankBitmap = new SKBitmap(TARGET_SIZE, TARGET_SIZE);

            return SKImage.FromBitmap(blankBitmap);
        }

        var trimmedWidth = maxX - minX + 1;
        var trimmedHeight = maxY - minY + 1;

        using var trimmedBitmap = new SKBitmap(trimmedWidth, trimmedHeight);

        using (var canvas = new SKCanvas(trimmedBitmap))
        {
            canvas.DrawImage(
                image,
                new SKRect(minX, minY, maxX + 1, maxY + 1),
                new SKRect(0, 0, trimmedWidth, trimmedHeight));
        }

        int finalWidth, finalHeight;
        SKBitmap bitmapToCenter;

        if ((trimmedWidth > TARGET_SIZE) || (trimmedHeight > TARGET_SIZE))
        {
            var scale = Math.Min((float)TARGET_SIZE / trimmedWidth, (float)TARGET_SIZE / trimmedHeight);
            finalWidth = (int)(trimmedWidth * scale);
            finalHeight = (int)(trimmedHeight * scale);

            var sampling = new SKSamplingOptions(SKFilterMode.Nearest);
            bitmapToCenter = trimmedBitmap.Resize(new SKImageInfo(finalWidth, finalHeight), sampling);
        }
        else
        {
            finalWidth = trimmedWidth;
            finalHeight = trimmedHeight;
            bitmapToCenter = trimmedBitmap.Copy();
        }

        var resultBitmap = new SKBitmap(TARGET_SIZE, TARGET_SIZE);

        using (var canvas = new SKCanvas(resultBitmap))
        {
            canvas.Clear(SKColors.Transparent);
            var offsetX = (TARGET_SIZE - finalWidth) / 2;
            var offsetY = (TARGET_SIZE - finalHeight) / 2;
            canvas.DrawBitmap(bitmapToCenter, offsetX, offsetY);
        }

        bitmapToCenter.Dispose();

        return SKImage.FromBitmap(resultBitmap);
    }
}