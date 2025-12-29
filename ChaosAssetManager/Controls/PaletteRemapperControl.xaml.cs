using System.IO;
using System.Windows;
using Chaos.Extensions.Common;
using ChaosAssetManager.Helpers;
using DALib.Definitions;
using DALib.Drawing;
using DALib.Extensions;
using DALib.Utility;
using Microsoft.Win32;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace ChaosAssetManager.Controls;

// ReSharper disable once ClassCanBeSealed.Global
public partial class PaletteRemapperControl
{
    private List<string>? SelectedFiles;
    private List<string>? SelectedPalettes;
    private string? ToPalettePath;

    public PaletteRemapperControl() => InitializeComponent();

    public static Palette ArrangeColorsForDyeableType(Palette palette, ColorTableEntry? defaultDyeColors = null)
    {
        defaultDyeColors ??= ColorTableEntry.Empty;

        var paletteWithoutDyeColors = palette.Distinct()
                                             .ToList();

        paletteWithoutDyeColors = paletteWithoutDyeColors.Except(defaultDyeColors.Colors)
                                                         .ToList();

        if ((paletteWithoutDyeColors.Count + 6) > CONSTANTS.COLORS_PER_PALETTE)
            throw new InvalidOperationException("Palette does not have enough space for dye colors.");

        //take colors up to the dye index start
        var newPalette = new Palette(paletteWithoutDyeColors.Take(CONSTANTS.PALETTE_DYE_INDEX_START));

        //copy dye colors into dyeable indexes
        for (var i = 0; i < defaultDyeColors.Colors.Length; i++)
            newPalette[CONSTANTS.PALETTE_DYE_INDEX_START + i] = defaultDyeColors.Colors[i];

        //take remaining colors and insert after dye index end
        var index = CONSTANTS.PALETTE_DYE_INDEX_START + 6;

        foreach (var color in paletteWithoutDyeColors.Skip(CONSTANTS.PALETTE_DYE_INDEX_START))
            newPalette[index++] = color;

        return newPalette;
    }

    private Palette MergePalettes(IEnumerable<Palette> palettes)
    {
        var uniqueColors = palettes.SelectMany(p => p)
                                   .Distinct()
                                   .ToList();

        if (uniqueColors.Count > CONSTANTS.COLORS_PER_PALETTE)
            throw new InvalidOperationException("Merged palette has more than 256 colors");

        return new Palette(uniqueColors);
    }

    private IEnumerable<Palettized<EpfFile>> RearrangeDyeColors(IEnumerable<Palettized<EpfFile>> palettizedImages)
    {
        var palettized = palettizedImages.ToList();

        if (!PathHelper.Instance.ArchivePathIsValid())
            throw new InvalidOperationException("Please set the archives path in the settings");

        var legendDat = ArchiveCache.Legend;
        var entry = legendDat["color0.tbl"];
        var colorTable = ColorTable.FromEntry(entry);
        var defaultColorEntry = colorTable[0];
        var toPalette = ArrangeColorsForDyeableType(palettized[0].Palette, defaultColorEntry);

        foreach (var palettizedImage in palettized)
            yield return palettizedImage.RemapPalette(toPalette);
    }

    private void RearrangeDyeColorsToggle_OnChecked(object sender, RoutedEventArgs e) => ToPalettePath = null;

    private void RemapEpfImages(List<(string First, string Second)> palettizedPaths, string toFolderName)
    {
        Palette toPalette;

        var palettizedImages = palettizedPaths.Select(
                                                  x => new Palettized<EpfFile>
                                                  {
                                                      Entity = EpfFile.FromFile(x.First),
                                                      Palette = Palette.FromFile(x.Second)
                                                  })
                                              .ToList();

        //set toPalette to the merged palette if the toggle is checked
        if (MergePalettesToggle.IsChecked ?? false)
            try
            {
                toPalette = MergePalettes(palettizedImages.Select(p => p.Palette));
            } catch (InvalidOperationException ex)
            {
                Snackbar.MessageQueue!.Enqueue(ex.Message);

                return;
            }
        else //otherwise, set toPalette to the selected palette
            toPalette = Palette.FromFile(ToPalettePath!);

        //normal remapping operation
        palettizedImages = palettizedImages.Select(p => p.RemapPalette(toPalette))
                                           .ToList();

        if (RearrangeDyeColorsToggle.IsChecked ?? false)
            try
            {
                palettizedImages = RearrangeDyeColors(palettizedImages)
                    .ToList();
            } catch (InvalidOperationException ex)
            {
                Snackbar.MessageQueue!.Enqueue(ex.Message);

                return;
            }

        foreach ((var palettizedImage, (var oldEpfPath, var oldPalettePath)) in palettizedImages.Zip(palettizedPaths))
        {
            var epfFileName = Path.GetFileName(oldEpfPath);
            var paletteFileName = Path.GetFileName(oldPalettePath);
            var epfPath = Path.Combine(toFolderName, epfFileName);
            var palettePath = Path.Combine(toFolderName, paletteFileName);

            palettizedImage.Entity.Save(epfPath);
            palettizedImage.Palette.Save(palettePath);
        }
    }

    private void RemapHpfImages(List<(string First, string Second)> palettizedPaths, string toFolderName)
    {
        Palette toPalette;

        var palettizedImages = palettizedPaths.Select(
                                                  x => new Palettized<HpfFile>
                                                  {
                                                      Entity = HpfFile.FromFile(x.First),
                                                      Palette = Palette.FromFile(x.Second)
                                                  })
                                              .ToList();

        //set toPalette to the merged palette if the toggle is checked
        if (MergePalettesToggle.IsChecked ?? false)
            try
            {
                toPalette = MergePalettes(palettizedImages.Select(p => p.Palette));
            } catch (InvalidOperationException ex)
            {
                Snackbar.MessageQueue!.Enqueue(ex.Message);

                return;
            }
        else //otherwise, set toPalette to the selected palette
            toPalette = Palette.FromFile(ToPalettePath!);

        //normal remapping operation
        palettizedImages = palettizedImages.Select(p => p.RemapPalette(toPalette))
                                           .ToList();

        foreach ((var palettizedImage, (var oldEpfPath, var oldPalettePath)) in palettizedImages.Zip(palettizedPaths))
        {
            var epfFileName = Path.GetFileName(oldEpfPath);
            var paletteFileName = Path.GetFileName(oldPalettePath);
            var epfPath = Path.Combine(toFolderName, epfFileName);
            var palettePath = Path.Combine(toFolderName, paletteFileName);

            palettizedImage.Entity.Save(epfPath);
            palettizedImage.Palette.Save(palettePath);
        }
    }

    private void RemapImagePaletteBtn_OnClick(object sender, RoutedEventArgs e)
    {
        if (SelectedFiles.IsNullOrEmpty()
            || SelectedPalettes.IsNullOrEmpty()
            || (string.IsNullOrEmpty(ToPalettePath) && !MergePalettesToggle.IsChecked!.Value))
        {
            Snackbar.MessageQueue!.Enqueue("Please select all required files or options");

            return;
        }

        var openFolderDialog = new OpenFolderDialog
        {
            InitialDirectory = PathHelper.Instance.PaletteRemapperImageToPath
        };

        if (openFolderDialog.ShowDialog() == false)
            return;

        if (openFolderDialog.FolderNames.Length != 1)
        {
            Snackbar.MessageQueue!.Enqueue("Please select a single folder to save to");

            return;
        }

        if (string.IsNullOrEmpty(openFolderDialog.FolderName))
            return;

        var isEpf = SelectedFiles.First()
                                 .EndsWithI(".epf");

        var palettizedPaths = SelectedFiles.Zip(SelectedPalettes)
                                           .ToList();

        if (isEpf)
            RemapEpfImages(palettizedPaths, openFolderDialog.FolderName);
        else
            RemapHpfImages(palettizedPaths, openFolderDialog.FolderName);

        PathHelper.Instance.PaletteRemapperImageToPath = openFolderDialog.FolderName;
        PathHelper.Instance.Save();

        Snackbar.MessageQueue!.Enqueue("Palette remapping complete");
    }

    private void SelectImagesBtn_OnClick(object sender, RoutedEventArgs e)
    {
        var fileDialog = new OpenFileDialog
        {
            Multiselect = true,
            Filter = "Images|*.epf;*.hpf",
            InitialDirectory = PathHelper.Instance.PaletteRemapperImageFromPath
        };

        if (fileDialog.ShowDialog() == false)
        {
            SelectedFiles = [];

            return;
        }

        if (fileDialog.FileNames.Length == 0)
        {
            SelectedFiles = [];

            return;
        }

        SelectedFiles = fileDialog.FileNames.ToList();

        var first = SelectedFiles.First();

        PathHelper.Instance.PaletteRemapperImageFromPath = Path.GetDirectoryName(first);
        PathHelper.Instance.Save();
    }

    private void SelectPalettesBtn_OnClick(object sender, RoutedEventArgs e)
    {
        var openFileDialog = new OpenFileDialog
        {
            Filter = "PAL Files|*.pal",
            InitialDirectory = PathHelper.Instance.PaletteRemapperPalFromPath,
            Multiselect = true
        };

        if (openFileDialog.ShowDialog() == false)
            return;

        var fileNames = openFileDialog.FileNames;

        // ReSharper disable once ConvertIfStatementToSwitchStatement
        if (fileNames.Length == 0)
            return;

        if (fileNames.Length == 1)
            SelectedPalettes = fileNames.ToList();
        else if (SelectedFiles is null)
            Snackbar.MessageQueue!.Enqueue("Please select images first");
        else
        {
            var orderedPaths = new List<string>();

            foreach (var imagePath in SelectedFiles)
            {
                var fileName = Path.GetFileNameWithoutExtension(imagePath);

                if (fileNames.All(fn => Path.GetFileNameWithoutExtension(fn) != fileName))
                {
                    Snackbar.MessageQueue!.Enqueue($"{fileName} does not have a corresponding palette");

                    return;
                }

                orderedPaths.Add(imagePath);
            }

            SelectedPalettes = orderedPaths;
        }

        PathHelper.Instance.PaletteRemapperPalFromPath = Path.GetDirectoryName(SelectedPalettes![0]);
        PathHelper.Instance.Save();
    }

    private void SelectToPaletteBtn_OnClick(object sender, RoutedEventArgs e)
    {
        var openFileDialog = new OpenFileDialog
        {
            Filter = "PAL Files|*.pal",
            InitialDirectory = PathHelper.Instance.PaletteRemapperPalToPath
        };

        if (openFileDialog.ShowDialog() == false)
            return;

        if (openFileDialog.FileNames.Length != 1)
        {
            Snackbar.MessageQueue!.Enqueue("Please select a single palette to remap the image(s) to");

            return;
        }

        if (openFileDialog.FileNames.Length == 0)
            return;

        ToPalettePath = openFileDialog.FileName;

        PathHelper.Instance.PaletteRemapperPalToPath = Path.GetDirectoryName(ToPalettePath);
        PathHelper.Instance.Save();

        RearrangeDyeColorsToggle.IsChecked = false;
    }
}