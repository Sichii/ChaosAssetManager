using System.IO;
using System.Windows;
using System.Windows.Controls;
using ChaosAssetManager.Helpers;
using DALib.Comparers;
using DALib.Definitions;
using DALib.Drawing;
using DALib.Utility;
using Microsoft.Win32;
using SkiaSharp;

namespace ChaosAssetManager.Controls;

public sealed partial class NPCImportControl
{
    public NPCImportControl() => InitializeComponent();

    private void NPCImportControl_OnLoaded(object sender, RoutedEventArgs e)
    {
        var archivePath = PathHelper.Instance.ArchivesPath;

        if (string.IsNullOrEmpty(archivePath) || !PathHelper.ArchivePathIsValid(archivePath))
        {
            NotConfiguredMessage.Visibility = Visibility.Visible;
            MainContent.Visibility = Visibility.Collapsed;
            InfoMessage.Visibility = Visibility.Collapsed;
            BottomInfoMessage.Visibility = Visibility.Collapsed;
        }
        else
        {
            NotConfiguredMessage.Visibility = Visibility.Collapsed;
            MainContent.Visibility = Visibility.Visible;
            InfoMessage.Visibility = Visibility.Visible;
            BottomInfoMessage.Visibility = Visibility.Visible;

            InputPathTxt.Text = PathHelper.Instance.NPCImportFromPath ?? string.Empty;
        }
    }

    private void BrowseInputBtn_OnClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select NPC Animation Folder",
            InitialDirectory = PathHelper.Instance.NPCImportFromPath
        };

        if (dialog.ShowDialog() == true)
        {
            InputPathTxt.Text = dialog.FolderName;
            PathHelper.Instance.NPCImportFromPath = dialog.FolderName;
            PathHelper.Instance.Save();
        }
    }

    private void FormatTypeCbx_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Attack2Label == null)
            return;

        var selectedItem = (ComboBoxItem)FormatTypeCbx.SelectedItem;
        var formatTag = (string)selectedItem.Tag;
        var isMultipleAttacks = formatTag == "MultipleAttacks";

        var visibility = isMultipleAttacks ? Visibility.Visible : Visibility.Collapsed;

        Attack2Label.Visibility = visibility;
        Attack2CountPanel.Visibility = visibility;
        Attack2IndexPanel.Visibility = visibility;

        Attack3Label.Visibility = visibility;
        Attack3CountPanel.Visibility = visibility;
        Attack3IndexPanel.Visibility = visibility;
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

        // Parse animation settings
        if (!int.TryParse(WalkCountTxt.Text, out var walkFrameCount) ||
            !int.TryParse(WalkIndexTxt.Text, out var walkFrameIndex) ||
            !int.TryParse(AttackCountTxt.Text, out var attackFrameCount) ||
            !int.TryParse(AttackIndexTxt.Text, out var attackFrameIndex) ||
            !int.TryParse(StandingCountTxt.Text, out var standingFrameCount) ||
            !int.TryParse(StandingIndexTxt.Text, out var standingFrameIndex) ||
            !int.TryParse(StopMotionCountTxt.Text, out var stopMotionFrameCount) ||
            !int.TryParse(StopMotionRatioTxt.Text, out var stopMotionRatio))
        {
            Snackbar.MessageQueue!.Enqueue("Please enter valid numbers for animation settings");

            return;
        }

        var selectedItem = (ComboBoxItem)FormatTypeCbx.SelectedItem;
        var formatTag = (string)selectedItem.Tag;
        var formatType = formatTag == "MultipleAttacks" ? MpfFormatType.MultipleAttacks : MpfFormatType.SingleAttack;

        int attack2FrameCount = 0, attack2FrameIndex = 0, attack3FrameCount = 0, attack3FrameIndex = 0;

        if (formatType == MpfFormatType.MultipleAttacks)
        {
            if (!int.TryParse(Attack2CountTxt.Text, out attack2FrameCount) ||
                !int.TryParse(Attack2IndexTxt.Text, out attack2FrameIndex) ||
                !int.TryParse(Attack3CountTxt.Text, out attack3FrameCount) ||
                !int.TryParse(Attack3IndexTxt.Text, out attack3FrameIndex))
            {
                Snackbar.MessageQueue!.Enqueue("Please enter valid numbers for Attack 2/3 settings");

                return;
            }
        }

        ImportBtn.IsEnabled = false;

        try
        {
            var result = await Task.Run(
                () =>
                {
                    var archive = ArchiveCache.Hades;

                    // Load images sorted naturally
                    var inputPaths = Directory.EnumerateFiles(inputPath, "*.png")
                        .Order(NaturalStringComparer.Instance)
                        .ToList();

                    if (inputPaths.Count == 0)
                        return (Success: false, Message: "No PNG images found in input folder", MonsterId: -1, PaletteId: -1);

                    var inputImages = inputPaths.Select(SKImage.FromEncodedData).ToList();

                    // Create MPF
                    var newMpf = MpfFile.FromImages(QuantizerOptions.Default, formatType, inputImages);

                    // Get palettes and find next palette ID
                    var palettes = Palette.FromArchive("mns", archive);
                    var newPaletteId = palettes.Keys.DefaultIfEmpty(0).Max() + 1;

                    // Find next monster ID
                    var nextMonsterId = archive.GetEntries("mns", ".mpf")
                        .Select(entry => entry.TryGetNumericIdentifier(out var id) ? id : -1)
                        .DefaultIfEmpty(0)
                        .Max() + 1;

                    // Set MPF entity properties
                    newMpf.Entity.AttackFrameCount = (byte)attackFrameCount;
                    newMpf.Entity.AttackFrameIndex = (byte)attackFrameIndex;
                    newMpf.Entity.StandingFrameCount = (byte)standingFrameCount;
                    newMpf.Entity.StandingFrameIndex = (byte)standingFrameIndex;
                    newMpf.Entity.WalkFrameCount = (byte)walkFrameCount;
                    newMpf.Entity.WalkFrameIndex = (byte)walkFrameIndex;
                    newMpf.Entity.PaletteNumber = newPaletteId;

                    if (formatType == MpfFormatType.MultipleAttacks)
                    {
                        newMpf.Entity.Attack2FrameCount = (byte)attack2FrameCount;
                        newMpf.Entity.Attack2StartIndex = (byte)attack2FrameIndex;
                        newMpf.Entity.Attack3FrameCount = (byte)attack3FrameCount;
                        newMpf.Entity.Attack3StartIndex = (byte)attack3FrameIndex;
                    }
                    else
                    {
                        newMpf.Entity.Attack2FrameCount = 0;
                        newMpf.Entity.Attack2StartIndex = 0;
                        newMpf.Entity.Attack3FrameCount = 0;
                        newMpf.Entity.Attack3StartIndex = 0;
                    }

                    newMpf.Entity.HeaderType = MpfHeaderType.None;
                    newMpf.Entity.UnknownHeaderBytes = [];
                    newMpf.Entity.OptionalAnimationFrameCount = (byte)stopMotionFrameCount;
                    newMpf.Entity.OptionalAnimationRatio = (byte)stopMotionRatio;
                    newMpf.Entity.PixelWidth = 0;
                    newMpf.Entity.PixelHeight = 0;

                    // Calculate and set center points for each frame
                    var frameCount = newMpf.Entity.Count;

                    for (var i = 0; i < frameCount; i++)
                    {
                        var frame = newMpf.Entity[i];
                        var centerX = (frame.Left + frame.PixelWidth) / 2f;
                        var centerY = (frame.Top + frame.PixelHeight) * 0.98f;
                        frame.CenterX = Convert.ToInt16(centerX);
                        frame.CenterY = Convert.ToInt16(centerY);
                    }

                    // Patch to archive
                    archive.Patch($"mns{nextMonsterId:D3}.mpf", newMpf.Entity);
                    archive.Patch($"mns{newPaletteId:D3}.pal", newMpf.Palette);

                    // Save archive
                    archive.Save(Path.Combine(archivePath, "hades.dat"));

                    return (Success: true, Message: "NPC imported successfully", MonsterId: nextMonsterId, PaletteId: newPaletteId);
                });

            if (result.Success)
                Snackbar.MessageQueue!.Enqueue($"{result.Message} (ID: {result.MonsterId}, Palette: {result.PaletteId})");
            else
                Snackbar.MessageQueue!.Enqueue(result.Message);
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
}
