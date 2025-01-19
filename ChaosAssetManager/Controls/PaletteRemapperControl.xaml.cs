using System.IO;
using System.Windows;
using Chaos.Extensions.Common;
using ChaosAssetManager.Helpers;
using DALib.Drawing;
using DALib.Extensions;
using DALib.Utility;
using Microsoft.Win32;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace ChaosAssetManager.Controls;

public partial class PaletteRemapperControl
{
    private string? FromPalettePath;
    private List<string>? SelectedFiles;
    private string? ToPalettePath;

    public PaletteRemapperControl() => InitializeComponent();

    private void RemapImagePaletteBtn_OnClick(object sender, RoutedEventArgs e)
    {
        if (SelectedFiles.IsNullOrEmpty() || string.IsNullOrEmpty(FromPalettePath) || string.IsNullOrEmpty(ToPalettePath))
        {
            Snackbar.MessageQueue!.Enqueue("Please select all required files");

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

        var output = new List<(string Path, EpfFile File)>();
        var fromPalette = Palette.FromFile(FromPalettePath);
        var toPalette = Palette.FromFile(ToPalettePath);

        foreach (var file in SelectedFiles)
        {
            var palettized = new Palettized<EpfFile>
            {
                Entity = EpfFile.FromFile(file),
                Palette = fromPalette
            };

            var remapped = palettized.RemapPalette(toPalette);
            output.Add((file, remapped.Entity));
        }

        foreach (var outFile in output)
        {
            var fileName = Path.GetFileName(outFile.Path);
            var targetPath = Path.Combine(openFolderDialog.FolderName, fileName);

            outFile.File.Save(targetPath);
        }

        PathHelper.Instance.PaletteRemapperImageToPath = openFolderDialog.FolderName;
        PathHelper.Instance.Save();

        Snackbar.MessageQueue!.Enqueue("Palette remapping complete");
    }

    private void SelectFromPaletteBtn_OnClick(object sender, RoutedEventArgs e)
    {
        var openFileDialog = new OpenFileDialog
        {
            Filter = "PAL Files|*.pal",
            InitialDirectory = PathHelper.Instance.PaletteRemapperPalFromPath
        };

        if (openFileDialog.ShowDialog() == false)
            return;

        if (openFileDialog.FileNames.Length != 1)
        {
            Snackbar.MessageQueue!.Enqueue("Please select a single palette to render the image(s) with");

            return;
        }

        FromPalettePath = openFileDialog.FileName;

        PathHelper.Instance.PaletteRemapperPalFromPath = Path.GetDirectoryName(FromPalettePath);
        PathHelper.Instance.Save();
    }

    private void SelectImagesBtn_OnClick(object sender, RoutedEventArgs e)
    {
        var fileDialog = new OpenFileDialog
        {
            Multiselect = true,
            Filter = "Images|*.epf",
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
    }
}