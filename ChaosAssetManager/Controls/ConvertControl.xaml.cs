using System.IO;
using System.Windows;
using Chaos.Extensions.Common;
using ChaosAssetManager.Helpers;
using DALib.Definitions;
using DALib.Drawing;
using DALib.Utility;
using SkiaSharp;
using DataFormats = System.Windows.DataFormats;
using DragDropEffects = System.Windows.DragDropEffects;
using DragEventArgs = System.Windows.DragEventArgs;
using Graphics = DALib.Drawing.Graphics;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace ChaosAssetManager.Controls;

public partial class ConvertControl
{
    private string? CurrentExtension;
    private string? PalettePath;
    private List<string>? SelectedFiles;

    public ConvertControl() => InitializeComponent();

    private void ConvertBtn_OnClick(object sender, RoutedEventArgs e)
    {
        if (SelectedFiles.IsNullOrEmpty())
            return;

        ArgumentNullException.ThrowIfNull(CurrentExtension);

        var targetExtension = ConversionOptionsCmbx.Text.ToLower();
        var isPalettizedSpf = false;

        if (targetExtension.ContainsI(".spf"))
        {
            if (targetExtension.ContainsI("(palettized)"))
                isPalettizedSpf = true;

            targetExtension = targetExtension.ReplaceI("(colorized)", string.Empty)
                                             .ReplaceI("(palettized)", string.Empty)
                                             .Trim();
        }

        if (CurrentExtension.EqualsI(targetExtension))
        {
            Snackbar.MessageQueue!.Enqueue("Files are already in the selected format");

            return;
        }

        var saveFileDialog = new SaveFileDialog
        {
            Filter = $"Images|{targetExtension}",
            InitialDirectory = PathHelper.Instance.ConvertImageToPath
        };

        if (saveFileDialog.ShowDialog() == false)
            return;

        if (string.IsNullOrEmpty(saveFileDialog.FileName))
            return;

        var targetPath = saveFileDialog.FileName;

        using var skImages = GetSkImages();

        var index = 0;
        var fileName = Path.GetFileNameWithoutExtension(targetPath);
        var targetDirectory = Path.GetDirectoryName(targetPath) ?? string.Empty;
        targetExtension = targetExtension == ".jpeg" ? ".jpg" : targetExtension;

        switch (targetExtension)
        {
            case ".png":
            case ".bmp":
            case ".jpg":
            {
                foreach (var image in skImages)
                {
                    var skEncodedImageFormat = Enum.Parse<SKEncodedImageFormat>(targetExtension[1..], true);
                    var path = Path.Combine(targetDirectory, $"{fileName}{index++:D3}{targetExtension}");

                    using var outStream = File.Create(path);
                    using var encoded = image.Encode(skEncodedImageFormat, 100);
                    encoded.SaveTo(outStream);
                }

                break;
            }
            case ".hpf":
            {
                foreach (var image in skImages)
                {
                    var hpfFile = HpfFile.FromImage(QuantizerOptions.Default, image);
                    var hpfPath = Path.Combine(targetDirectory, $"{fileName}{index:D3}.hpf");
                    var palPath = Path.Combine(targetDirectory, $"{fileName}{index++:D3}.pal");

                    hpfFile.Entity.Save(hpfPath);
                    hpfFile.Palette.Save(palPath);
                }

                break;
            }
            case ".efa":
            {
                var efaFile = EfaFile.FromImages(skImages);
                var efaPath = Path.Combine(targetDirectory, $"{fileName}.efa");

                efaFile.Save(efaPath);

                break;
            }
            case ".spf":
            {
                if (isPalettizedSpf)
                {
                    var spfFile = SpfFile.FromImages(QuantizerOptions.Default, skImages);
                    var spfPath = Path.Combine(targetDirectory, $"{fileName}.spf");

                    spfFile.Save(spfPath);
                } else
                {
                    var spfFile = SpfFile.FromImages(skImages);
                    var spfPath = Path.Combine(targetDirectory, $"{fileName}.spf");

                    spfFile.Save(spfPath);
                }

                break;
            }
            case ".epf":
            {
                var epfFile = EpfFile.FromImages(QuantizerOptions.Default, skImages);
                var epfPath = Path.Combine(targetDirectory, $"{fileName}.epf");
                var palPath = Path.Combine(targetDirectory, $"{fileName}.pal");

                epfFile.Entity.Save(epfPath);
                epfFile.Palette.Save(palPath);

                break;
            }
            case ".mpf":
            {
                var mpfFile = MpfFile.FromImages(QuantizerOptions.Default, MpfFormatType.SingleAttack, skImages);
                var mpfPath = Path.Combine(targetDirectory, $"{fileName}.mpf");
                var palPath = Path.Combine(targetDirectory, $"{fileName}.pal");

                mpfFile.Entity.Save(mpfPath);
                mpfFile.Palette.Save(palPath);

                break;
            }
        }

        PathHelper.Instance.ConvertImageToPath = targetDirectory;
        PathHelper.Instance.Save();

        Snackbar.MessageQueue!.Enqueue("Conversion successful!");
    }

    private void ConvertControl_OnDragEnter(object sender, DragEventArgs e)
        => e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;

    private void ConvertControl_OnDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            return;

        var files = (string[])e.Data.GetData(DataFormats.FileDrop)!;

        if (files.Length == 0)
            return;

        var first = files[0];

        PathHelper.Instance.ConvertImageFromPath = Path.GetDirectoryName(first);
        PathHelper.Instance.Save();

        TryLoadFiles(files);
    }

    private SKImageCollection GetSkImages()
    {
        ArgumentNullException.ThrowIfNull(SelectedFiles);

        var transformer = CurrentExtension switch
        {
            ".png"  => SelectedFiles.Select(SKImage.FromEncodedData),
            ".bmp"  => SelectedFiles.Select(SKImage.FromEncodedData),
            ".jpg"  => SelectedFiles.Select(SKImage.FromEncodedData),
            ".jpeg" => SelectedFiles.Select(SKImage.FromEncodedData),
            ".efa" => SelectedFiles.SelectMany(
                path =>
                {
                    var efaFile = EfaFile.FromFile(path);

                    return efaFile.Select(frame => Graphics.RenderImage(frame, efaFile.BlendingType));
                }),
            ".spf" => SelectedFiles.SelectMany(
                path =>
                {
                    var spfFile = SpfFile.FromFile(path);

                    return spfFile.Select(
                        frame => spfFile.Format == SpfFormatType.Colorized
                            ? Graphics.RenderImage(frame)
                            : Graphics.RenderImage(frame, spfFile.PrimaryColors!));
                }),
            ".hpf" => SelectedFiles.Select(
                path =>
                {
                    ArgumentNullException.ThrowIfNull(PalettePath);

                    var hpfFile = HpfFile.FromFile(path);
                    var palette = Palette.FromFile(PalettePath);

                    return Graphics.RenderImage(hpfFile, palette);
                }),
            ".mpf" => SelectedFiles.SelectMany(
                path =>
                {
                    ArgumentNullException.ThrowIfNull(PalettePath);

                    var mpfFile = MpfFile.FromFile(path);
                    var palette = Palette.FromFile(PalettePath);

                    return mpfFile.Select(frame => Graphics.RenderImage(frame, palette));
                }),
            ".epf" => SelectedFiles.SelectMany(
                path =>
                {
                    ArgumentNullException.ThrowIfNull(PalettePath);

                    var epfFile = EpfFile.FromFile(path);
                    var palette = Palette.FromFile(PalettePath);

                    return epfFile.Select(frame => Graphics.RenderImage(frame, palette));
                }),
            _ => throw new NotSupportedException()
        };

        return new SKImageCollection(transformer);
    }

    private void SelectFilesBtn_OnClick(object sender, RoutedEventArgs e)
    {
        var fileDialog = new OpenFileDialog
        {
            Multiselect = true,
            Filter = "Images|*.png;*.bmp;*.jpg;*.jpeg;*.epf;*.efa;*.hpf;*.spf;*.mpf",
            InitialDirectory = PathHelper.Instance.ConvertImageFromPath
        };

        if ((fileDialog.ShowDialog() == false) || (fileDialog.FileNames.Length == 0))
        {
            SelectedFiles = [];

            return;
        }

        PathHelper.Instance.ConvertImageFromPath = Path.GetDirectoryName(fileDialog.FileNames[0]);
        PathHelper.Instance.Save();

        TryLoadFiles(fileDialog.FileNames);
    }

    private void TryLoadFiles(string[] files)
    {
        //hide conversion options
        //re show them if this method succeeds
        ConversionPanel.Visibility = Visibility.Hidden;

        SelectedFiles = null;
        CurrentExtension = null;
        PalettePath = null;

        var distinctExtensions = files.Select(Path.GetExtension)
                                      .Distinct()
                                      .ToList();

        if (distinctExtensions.Count != 1)
        {
            Snackbar.MessageQueue!.Enqueue("Please select files of the same type");
            SelectedFiles = [];

            return;
        }

        var extension = distinctExtensions[0]!.ToLower();

        if (extension is ".epf" or ".hpf" or ".mpf")
        {
            var openFileDialog = new OpenFileDialog
            {
                Multiselect = true,
                Filter = "PAL Files|*.pal",
                InitialDirectory = PathHelper.Instance.ConvertPalFromPath
            };

            if (openFileDialog.ShowDialog() == false)
                return;

            if (openFileDialog.FileNames.Length != 1)
            {
                Snackbar.MessageQueue!.Enqueue("Please select a single palette to render the image(s) with");

                return;
            }

            PalettePath = openFileDialog.FileNames[0];

            PathHelper.Instance.ConvertPalFromPath = Path.GetDirectoryName(PalettePath);
            PathHelper.Instance.Save();
        }

        SelectedFiles = files.ToList();
        CurrentExtension = extension;

        ConversionPanel.Visibility = Visibility.Visible;
    }
}