using System.IO;
using System.Windows;
using System.Windows.Controls;
using ChaosAssetManager.Helpers;
using DALib.Data;
using DALib.Definitions;
using DALib.Drawing;
using DALib.Extensions;
using DALib.Utility;
using Microsoft.Win32;
using SkiaSharp;

namespace ChaosAssetManager.Controls;

public sealed partial class EquipmentImportControl
{
    public EquipmentImportControl()
    {
        InitializeComponent();

        PathHelper.ArchivesPathChanged += () => EquipmentImportControl_OnLoaded(this, new RoutedEventArgs());
    }

    private void BrowseInputBtn_OnClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select Equipment Animation Folder",
            InitialDirectory = PathHelper.Instance.EquipmentImportFromPath
        };

        if (dialog.ShowDialog() == true)
        {
            InputPathTxt.Text = dialog.FolderName;
            PathHelper.Instance.EquipmentImportFromPath = dialog.FolderName;
            PathHelper.Instance.Save();
        }
    }

    private void EquipmentImportControl_OnLoaded(object sender, RoutedEventArgs e)
    {
        var archivePath = PathHelper.Instance.ArchivesPath;

        if (string.IsNullOrEmpty(archivePath) || !PathHelper.ArchivePathIsValid(archivePath))
        {
            NotConfiguredMessage.Visibility = Visibility.Visible;
            MainContent.Visibility = Visibility.Collapsed;
            InfoMessage.Visibility = Visibility.Collapsed;
            BottomInfoMessage.Visibility = Visibility.Collapsed;
        } else
        {
            NotConfiguredMessage.Visibility = Visibility.Collapsed;
            MainContent.Visibility = Visibility.Visible;
            InfoMessage.Visibility = Visibility.Visible;
            BottomInfoMessage.Visibility = Visibility.Visible;

            InputPathTxt.Text = PathHelper.Instance.EquipmentImportFromPath ?? string.Empty;
        }
    }

    //associated letters may live in different archives (e.g. p=khanNs, w=khanTz),
    //so we resolve the archive per letter rather than accepting a single archive.
    private static IEnumerable<DataArchiveEntry> GetAllAssociatedEntries(string equipmentLetter, string genderLetter, bool isMale)
    {
        var letters = equipmentLetter.ToLower() switch
        {
            "c" or "g"        => new[] { "c", "g" },
            "e" or "f" or "h" => new[] { "e", "f", "h" },
            "i" or "u"        => new[] { "i", "u" },
            "p" or "w"        => new[] { "p", "w" },
            _                 => new[] { equipmentLetter.ToLower() }
        };

        return letters.SelectMany(letter => GetArchiveForLetter(letter, isMale)
                                     .GetEntries($"{genderLetter}{letter}", ".epf"));
    }

    private static DataArchive GetArchiveForLetter(string letter, bool isMale)
        => letter.ToLower() switch
        {
            "a" or "b" or "c" or "d"                      => isMale ? ArchiveCache.KhanMad : ArchiveCache.KhanWad,
            "e" or "f" or "g" or "h"                      => isMale ? ArchiveCache.KhanMeh : ArchiveCache.KhanWeh,
            "i" or "j" or "k" or "l" or "m"               => isMale ? ArchiveCache.KhanMim : ArchiveCache.KhanWim,
            "n" or "o" or "p" or "q" or "r" or "s"        => isMale ? ArchiveCache.KhanMns : ArchiveCache.KhanWns,
            "t" or "u" or "v" or "w" or "x" or "y" or "z" => isMale ? ArchiveCache.KhanMtz : ArchiveCache.KhanWtz,
            _                                             => throw new InvalidOperationException($"Invalid equipment letter: {letter}")
        };

    private static string GetArchiveNameForLetter(string letter, bool isMale)
    {
        var genderLetter = isMale ? "m" : "w";

        var suffix = letter.ToLower() switch
        {
            "a" or "b" or "c" or "d"                      => "ad",
            "e" or "f" or "g" or "h"                      => "eh",
            "i" or "j" or "k" or "l" or "m"               => "im",
            "n" or "o" or "p" or "q" or "r" or "s"        => "ns",
            "t" or "u" or "v" or "w" or "x" or "y" or "z" => "tz",
            _                                             => throw new InvalidOperationException($"Invalid equipment letter: {letter}")
        };

        return $"khan{genderLetter}{suffix}.dat";
    }

    private static int GetNextEntryId(string equipmentLetter, string genderLetter, bool isMale)
    {
        var entries = GetAllAssociatedEntries(equipmentLetter, genderLetter, isMale)
                      .Select(entry => entry.TryGetNumericIdentifier(out var id, 3) ? id : -1)
                      .ToHashSet();

        for (var i = 1; i < 999; i++)
            if (!entries.Contains(i))
                return i;

        return -1;
    }

    private static int GetNextUnisexEntryId(string equipmentLetter)
    {
        var maleEntries = GetAllAssociatedEntries(equipmentLetter, "m", true)
                          .Select(entry => entry.TryGetNumericIdentifier(out var id, 3) ? id : -1)
                          .ToHashSet();

        var femaleEntries = GetAllAssociatedEntries(equipmentLetter, "w", false)
                            .Select(entry => entry.TryGetNumericIdentifier(out var id, 3) ? id : -1)
                            .ToHashSet();

        // Find first ID that's free in both archives
        for (var i = 1; i < 999; i++)
            if (!maleEntries.Contains(i) && !femaleEntries.Contains(i))
                return i;

        return -1;
    }

    private static string GetPalLetterFromLetter(string letter)
        => letter.ToLower() switch
        {
            "a" => "b",
            "g" => "c",
            "j" => "c",
            "o" => "m",
            "s" => "p",
            "e" => "h",
            "f" => "h",
            _   => letter.ToLower()
        };

    private static string GetSuffixFromFolderName(string folderName)
    {
        var lower = folderName.ToLower();

        return lower switch
        {
            "01"                         => "01",
            "02"                         => "02",
            "03"                         => "03",
            "04"                         => "04",
            _ when lower.StartsWith("b") => "b",
            _ when lower.StartsWith("c") => "c",
            _ when lower.StartsWith("d") => "d",
            _ when lower.StartsWith("e") => "e",
            _ when lower.StartsWith("f") => "f",
            _                            => ""
        };
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

        if (string.IsNullOrEmpty(inputPath) || !Directory.Exists(inputPath))
        {
            Snackbar.MessageQueue!.Enqueue("Please select a valid input path");

            return;
        }

        var selectedItem = (ComboBoxItem)EquipmentTypeCbx.SelectedItem;
        var equipmentLetter = (string)selectedItem.Tag;
        var isMale = MaleRadio.IsChecked == true;
        var isUnisex = UnisexRadio.IsChecked == true;
        var hasDyeablePalette = DyeableToggle.IsChecked == true;

        // Shields are always loaded from khanmns.dat regardless of character gender; vanilla
        // Darkages.exe hardcodes the shield-slot filename prefix to 'm'. Force male-only import
        // so patched files land where the game actually reads them.
        if (equipmentLetter.Equals("s", StringComparison.OrdinalIgnoreCase) && (!isMale || isUnisex))
        {
            isMale = true;
            isUnisex = false;
            Snackbar.MessageQueue!.Enqueue("Shields are only read from khanmns.dat — importing as male.");
        }

        ImportBtn.IsEnabled = false;

        try
        {
            var result = await Task.Run(() =>
            {
                var khanPal = ArchiveCache.KhanPal;
                var paletteLetter = GetPalLetterFromLetter(equipmentLetter);

                var palettizedEpfs = new Dictionary<string, EpfFile>();

                // Load images from subdirectories
                var originalImages = Directory.EnumerateDirectories(inputPath)
                                              .SelectMany(dir => Directory.EnumerateFiles(dir, "*.png"))
                                              .Select(f => new KeyValuePair<string, SKImage>(f, SKImage.FromEncodedData(f)))
                                              .ToList();

                if (originalImages.Count == 0)

                    // Try loading from root if no subdirectories
                    originalImages = Directory.EnumerateFiles(inputPath, "*.png")
                                              .Select(f => new KeyValuePair<string, SKImage>(f, SKImage.FromEncodedData(f)))
                                              .ToList();

                if (originalImages.Count == 0)
                    return (Success: false, Message: "No PNG images found in input folder", EntryId: -1, PaletteId: -1);

                // Create EPF from all images
                var palettized = EpfFile.FromImages(QuantizerOptions.Default, originalImages.Select(kvp => kvp.Value));

                if (hasDyeablePalette)
                {
                    var legend = ArchiveCache.Legend;
                    var colorTable = ColorTable.FromEntry(legend["color0.tbl"]);
                    palettized = palettized.ArrangeColorsForDyeableType(colorTable[0]);
                }

                (var oneBigEpf, var finalPalette) = palettized;

                var pathToFrame = oneBigEpf.Zip(originalImages)
                                           .ToDictionary(pair => pair.Second.Key, pair => pair.First);

                // Group frames by their animation folder
                foreach (var group in pathToFrame.GroupBy(kvp => Path.GetDirectoryName(kvp.Key)))
                {
                    var maxWidth = (short)group.Max(kvp => kvp.Value.PixelWidth);
                    var maxHeight = (short)group.Max(kvp => kvp.Value.PixelHeight);
                    var epf = new EpfFile(maxWidth, maxHeight);

                    foreach ((_, var frame) in group)
                        epf.Add(frame);

                    palettizedEpfs[group.Key!] = epf;
                }

                // If images were loaded from root (no subdirs), put them all in one EPF
                if ((palettizedEpfs.Count == 0) && (pathToFrame.Count > 0))
                {
                    var maxWidth = (short)pathToFrame.Max(kvp => kvp.Value.PixelWidth);
                    var maxHeight = (short)pathToFrame.Max(kvp => kvp.Value.PixelHeight);
                    var epf = new EpfFile(maxWidth, maxHeight);

                    foreach ((_, var frame) in pathToFrame)
                        epf.Add(frame);

                    palettizedEpfs[inputPath] = epf;
                }

                var palettes = PaletteLookup.FromArchive($"pal{paletteLetter}", khanPal);
                int nextEntryId;
                string resultMessage;

                if (isUnisex)
                {
                    // For unisex, find an ID that's free in BOTH archives
                    var maleArchive = GetArchiveForLetter(equipmentLetter, true);
                    var femaleArchive = GetArchiveForLetter(equipmentLetter, false);

                    nextEntryId = GetNextUnisexEntryId(equipmentLetter);

                    if (nextEntryId == -1)
                        return (Success: false, Message: "No free equipment slots found in both archives", EntryId: -1, PaletteId: -1);

                    // Patch EPFs to both archives
                    foreach ((var folderPath, var epfFile) in palettizedEpfs)
                    {
                        var folderName = Path.GetFileName(folderPath);
                        var suffix = GetSuffixFromFolderName(folderName);

                        maleArchive.Patch($"m{equipmentLetter.ToLower()}{nextEntryId:D3}{suffix}.epf", epfFile);
                        femaleArchive.Patch($"w{equipmentLetter.ToLower()}{nextEntryId:D3}{suffix}.epf", epfFile);
                    }

                    // Save both archives
                    var maleArchiveName = GetArchiveNameForLetter(equipmentLetter, true);
                    var femaleArchiveName = GetArchiveNameForLetter(equipmentLetter, false);

                    maleArchive.Save(Path.Combine(archivePath, maleArchiveName));
                    femaleArchive.Save(Path.Combine(archivePath, femaleArchiveName));

                    resultMessage = $"Imported to {maleArchiveName} and {femaleArchiveName}";
                } else
                {
                    // Single gender import
                    var genderLetter = isMale ? "m" : "w";
                    var archive = GetArchiveForLetter(equipmentLetter, isMale);

                    nextEntryId = GetNextEntryId(equipmentLetter, genderLetter, isMale);

                    if (nextEntryId == -1)
                        return (Success: false, Message: "No free equipment slots found", EntryId: -1, PaletteId: -1);

                    // Patch EPFs based on folder names
                    foreach ((var folderPath, var epfFile) in palettizedEpfs)
                    {
                        var folderName = Path.GetFileName(folderPath);
                        var suffix = GetSuffixFromFolderName(folderName);
                        archive.Patch($"{genderLetter}{equipmentLetter.ToLower()}{nextEntryId:D3}{suffix}.epf", epfFile);
                    }

                    var archiveName = GetArchiveNameForLetter(equipmentLetter, isMale);
                    archive.Save(Path.Combine(archivePath, archiveName));

                    resultMessage = $"Imported to {archiveName}";
                }

                // Add palette entry
                var nextPaletteId = palettes.GetNextPaletteId();

                if (isUnisex)
                    palettes.Table.Add(nextEntryId, nextPaletteId);
                else
                {
                    var overrideType = isMale ? KhanPalOverrideType.Male : KhanPalOverrideType.Female;
                    palettes.Table.Add(nextEntryId, nextPaletteId, overrideType);
                }

                palettes.Palettes[nextPaletteId] = finalPalette;

                // Patch palette files
                khanPal.Patch($"pal{paletteLetter}{nextPaletteId:D3}.pal", finalPalette);
                khanPal.Patch($"pal{paletteLetter}.tbl", palettes.Table);
                khanPal.Save(Path.Combine(archivePath, "khanpal.dat"));

                return (Success: true, Message: resultMessage, EntryId: nextEntryId, PaletteId: nextPaletteId);
            });

            if (result.Success)
            {
                //refresh all in-memory archives and render caches so the new equipment is visible without a restart
                CacheManager.RefreshAfterImport();

                Snackbar.MessageQueue!.Enqueue($"{result.Message} (ID: {result.EntryId}, Palette: {result.PaletteId})");
            } else
                Snackbar.MessageQueue!.Enqueue(result.Message);
        } catch (Exception ex)
        {
            Snackbar.MessageQueue!.Enqueue($"Error: {ex.Message}");
        } finally
        {
            ImportBtn.IsEnabled = true;
        }
    }
}