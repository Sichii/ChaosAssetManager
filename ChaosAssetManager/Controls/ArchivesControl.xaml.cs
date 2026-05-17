using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Chaos.Extensions.Common;
using ChaosAssetManager.Controls.PreviewControls;
using ChaosAssetManager.Helpers;
using ChaosAssetManager.Model;
using DALib.Data;
using MaterialDesignThemes.Wpf;
using SkiaSharp;
using Application = System.Windows.Application;
using Button = System.Windows.Controls.Button;
using DataFormats = System.Windows.DataFormats;
using DragDropEffects = System.Windows.DragDropEffects;
using DragEventArgs = System.Windows.DragEventArgs;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using FolderBrowserDialog = System.Windows.Forms.FolderBrowserDialog;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace ChaosAssetManager.Controls;

public sealed partial class ArchivesControl : IDisposable
{
    private string ArchiveName = string.Empty;
    private string ArchiveRoot = string.Empty;
    public DataArchive? Archive { get; set; }

    public ArchivesControl() => InitializeComponent();

    /// <inheritdoc />
    public void Dispose()
    {
        (Preview?.Content as IDisposable)?.Dispose();
        Archive?.Dispose();
    }

    private void CloseEntryBtn_OnClick(object sender, RoutedEventArgs e)
    {
        var btn = (Button)sender;
        var context = btn.DataContext;

        if (Archive is null || context is not DataArchiveEntry entry)
            return;

        var result = MessageBox.Show(
            Application.Current.MainWindow!,
            $"Are you sure you want to remove the entry \"{entry.EntryName}\"?",
            "Are you sure?",
            MessageBoxButton.YesNo,
            MessageBoxImage.Exclamation);

        if (result == MessageBoxResult.Yes)
        {
            var currentIndex = ArchivesView.SelectedIndex;

            //save expanded nodes
            var currentlyExpandedGroupings = ArchivesView.Items
                                                         .OfType<EntryGrouping>()
                                                         .Where(grouping =>
                                                         {
                                                             if (ArchivesView.ItemContainerGenerator.ContainerFromItem(grouping)
                                                                 is TreeListViewItem { IsExpanded: true })
                                                                 return true;

                                                             return false;
                                                         })
                                                         .ToList();

            Archive.Remove(entry);

            if (currentIndex >= Archive.Count)
                currentIndex--;

            SetViewSource();

            var newExpandedGroupings = ArchivesView.Items
                                                   .OfType<EntryGrouping>()
                                                   .Where(group => currentlyExpandedGroupings.Contains(group))
                                                   .ToList();

            //force UI redraw
            ArchivesView.UpdateLayout();

            //deferred action to allow UI thread to redraw before attempting to find containers generated during redraw
            Dispatcher.BeginInvoke(() =>
            {
                //re-expand nodes
                foreach (var grouping in newExpandedGroupings)
                    if (ArchivesView.ItemContainerGenerator.ContainerFromItem(grouping) is TreeListViewItem tvi)
                        tvi.IsExpanded = true;

                //restore selection
                ArchivesView.SelectedIndex = currentIndex;
            });
        }
    }

    #region Events
    private void ArchivesControl_OnDragEnter(object sender, DragEventArgs e)
        => e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;

    private void ArchivesControl_OnDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            return;

        var files = (string[])e.Data.GetData(DataFormats.FileDrop)!;

        if (files.Length == 0)
            return;

        if (files.Count(file => file.EndsWithI(".dat")) > 1)
        {
            ShowMessage("Please only drop one archive at a time!");

            return;
        }

        var archivePath = files.FirstOrDefault(file => file.EndsWithI(".dat"));

        if (archivePath is not null)
        {
            LoadArchive(archivePath);

            PathHelper.Instance.ArchiveLoadFromPath = Path.GetDirectoryName(archivePath);
            PathHelper.Instance.Save();

            ShowMessage("Archive loaded successfully!");

            return;
        }

        if (Archive is null)
        {
            ShowMessage("Please load an archive first!");

            return;
        }

        var patched = false;

        ArchivesView.ItemsSource = null;

        foreach (var file in files)
        {
            PatchArchive(file);
            patched = true;
        }

        SetViewSource();
        ArchivesView.SelectedItems.Clear();

        foreach (var fileName in files)
        {
            var entryName = Path.GetFileName(fileName);
            var entry = Archive![entryName];
            ArchivesView.SelectedItems.Add(entry);
        }

        if (patched)
        {
            var firstPath = files.First();

            PathHelper.Instance.PatchFromPath = Path.GetDirectoryName(firstPath);
            PathHelper.Instance.Save();

            ShowMessage("Archive patched successfully!");
        }
    }

    private void ArchivesView_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        (Preview.Content as IDisposable)?.Dispose();
        Preview.Content = null;

        ArgumentNullException.ThrowIfNull(Archive);

        var numSelectedItems = ArchivesView.SelectedItems.Count;

        ExtractSelectionBtn.IsEnabled = numSelectedItems != 0;
        SaveRenderBtn.IsEnabled = numSelectedItems != 0;

        if (numSelectedItems == 1)
        {
            if (ArchivesView.SelectedItem is not DataArchiveEntry selectedEntry)
                return;

            if (selectedEntry.EntryName.EqualsI("tilea.bmp") || selectedEntry.EntryName.EqualsI("tileas.bmp"))
                Preview.Content = new TileViewerControl(Archive, selectedEntry);
            else
                Preview.Content = new EntryPreviewControl(
                    Archive,
                    selectedEntry,
                    ArchiveName,
                    ArchiveRoot);
        }
    }

    private void CompileBtn_OnClick(object sender, RoutedEventArgs e)
    {
        using var folderBrowserDialog = new FolderBrowserDialog();
        folderBrowserDialog.InitialDirectory = PathHelper.Instance.ArchiveCompileFromPath!;

        if (folderBrowserDialog.ShowDialog() == DialogResult.OK)
        {
            var saveFileDialog = new SaveFileDialog
            {
                Filter = "Data Archive (*.dat)|*.dat",
                InitialDirectory = PathHelper.Instance.ArchiveCompileToPath
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                DataArchive.Compile(folderBrowserDialog.SelectedPath, saveFileDialog.FileName);

                PathHelper.Instance.ArchiveCompileFromPath = folderBrowserDialog.SelectedPath;
                PathHelper.Instance.ArchiveCompileToPath = Path.GetDirectoryName(saveFileDialog.FileName);
                PathHelper.Instance.Save();

                ShowMessage("Archive compiled successfully!");
            }
        }
    }

    private void CompileToBtn_OnClick(object sender, RoutedEventArgs e)
    {
        var saveFileDialog = new SaveFileDialog
        {
            Filter = "Data Archive (*.dat)|*.dat",
            InitialDirectory = PathHelper.Instance.ArchiveCompileToToPath
        };

        if (saveFileDialog.ShowDialog() == true)
            try
            {
                Archive?.Save(saveFileDialog.FileName);

                PathHelper.Instance.ArchiveCompileToToPath = Path.GetDirectoryName(saveFileDialog.FileName);
                PathHelper.Instance.Save();

                ShowMessage("Archive compiled successfully!");
            } catch
            {
                ShowMessage("Failed to compile archive! (Target in use?)", TimeSpan.FromSeconds(2));
            }
    }

    private void ExtractBtn_OnClick(object sender, RoutedEventArgs e)
    {
        var openFileDialog = new OpenFileDialog
        {
            Filter = "Data Archive (*.dat)|*.dat",
            InitialDirectory = PathHelper.Instance.ArchiveExtractFromPath
        };

        if (openFileDialog.ShowDialog() == true)
        {
            using var folderBrowserDialog = new FolderBrowserDialog();
            folderBrowserDialog.InitialDirectory = PathHelper.Instance.ArchiveExtractToPath!;

            if (folderBrowserDialog.ShowDialog() == DialogResult.OK)
            {
                using var archive = DataArchive.FromFile(openFileDialog.FileName);
                archive.ExtractTo(folderBrowserDialog.SelectedPath);

                PathHelper.Instance.ArchiveExtractFromPath = Path.GetDirectoryName(openFileDialog.FileName);
                PathHelper.Instance.ArchiveExtractToPath = folderBrowserDialog.SelectedPath;
                PathHelper.Instance.Save();

                ShowMessage("Archive extracted successfully!");
            }
        }
    }

    private void ExtractSelectionBtn_OnClick(object sender, RoutedEventArgs e)
    {
        using var folderBrowserDialog = new FolderBrowserDialog();
        folderBrowserDialog.InitialDirectory = PathHelper.Instance.ArchiveExtractSelectionToPath!;

        if (folderBrowserDialog.ShowDialog() == DialogResult.OK)
            foreach (DataArchiveEntry entry in ArchivesView.SelectedItems)
            {
                var outPath = Path.Combine(folderBrowserDialog.SelectedPath, entry.EntryName);
                using var outStream = File.Create(outPath);
                using var entrySegment = entry.ToStreamSegment();

                entrySegment.CopyTo(outStream);

                PathHelper.Instance.ArchiveExtractSelectionToPath = folderBrowserDialog.SelectedPath;
                PathHelper.Instance.Save();

                ShowMessage("Entries extracted successfully!");
            }
    }

    private async void SaveRenderBtn_OnClick(object sender, RoutedEventArgs e)
    {
        if (Archive is null)
            return;

        //tile viewer hijacks the single-entry case
        if (Preview.Content is TileViewerControl tileViewer)
        {
            await SaveRenderForTile(tileViewer);

            return;
        }

        var selectedItems = ArchivesView.SelectedItems
                                        .OfType<DataArchiveEntry>()
                                        .ToList();

        if (selectedItems.Count == 0)
            return;

        if (selectedItems.Count == 1)
            await SaveRenderForSingleEntry(selectedItems[0]);
        else
            SaveRenderForBatch(selectedItems);
    }

    private async Task SaveRenderForTile(TileViewerControl tileViewer)
    {
        var tileIndex = tileViewer.SelectedTileIndex;

        if (tileIndex is null)
        {
            ShowMessage("Select a tile first.");

            return;
        }

        if (ArchivesView.SelectedItem is not DataArchiveEntry atlasEntry)
            return;

        using var animation = RenderUtil.TryRenderTile(Archive!, atlasEntry, tileIndex.Value);

        if (animation is null || animation.Frames.Count == 0)
        {
            ShowMessage("Failed to render tile.");

            return;
        }

        var nameHint = $"{Path.GetFileNameWithoutExtension(atlasEntry.EntryName)}_tile{tileIndex.Value:D3}";

        if (animation.Frames.Count == 1)
            SaveSingleFrameViaDialog(animation.Frames[0], nameHint);
        else
            await SaveMultiFrameViaSelector(animation, nameHint);
    }

    private async Task SaveRenderForSingleEntry(DataArchiveEntry entry)
    {
        using var animation = RenderUtil.TryRenderAnimation(
            Archive!,
            entry,
            ArchiveName,
            ArchiveRoot);

        if (animation is null || animation.Frames.Count == 0)
        {
            ShowMessage("Selected entry has no renderable preview.");

            return;
        }

        var nameHint = Path.GetFileNameWithoutExtension(entry.EntryName);

        if (animation.Frames.Count == 1)
            SaveSingleFrameViaDialog(animation.Frames[0], nameHint);
        else
            await SaveMultiFrameViaSelector(animation, nameHint);
    }

    private void SaveSingleFrameViaDialog(SKImage frame, string nameHint)
    {
        var saveFileDialog = new SaveFileDialog
        {
            Filter = "PNG Image (*.png)|*.png",
            FileName = $"{nameHint}.png",
            InitialDirectory = PathHelper.Instance.SaveRenderToFilePath
        };

        if (saveFileDialog.ShowDialog() != true)
            return;

        try
        {
            WriteFrameToPng(frame, saveFileDialog.FileName);
        } catch (Exception ex)
        {
            ShowMessage($"Failed to save {Path.GetFileName(saveFileDialog.FileName)} — {ex.Message}.", TimeSpan.FromSeconds(3));

            return;
        }

        PathHelper.Instance.SaveRenderToFilePath = Path.GetDirectoryName(saveFileDialog.FileName);
        PathHelper.Instance.Save();

        ShowMessage("Saved 1 frame.");
    }

    private async Task SaveMultiFrameViaSelector(Animation animation, string nameHint)
    {
        var selector = new SaveRenderFrameSelectorControl(animation, nameHint);
        var picked = await selector.ShowAsync();

        if (picked is null || picked.Count == 0)
            return;

        using var folderBrowserDialog = new FolderBrowserDialog();
        folderBrowserDialog.InitialDirectory = PathHelper.Instance.SaveRenderToPath!;

        if (folderBrowserDialog.ShowDialog() != DialogResult.OK)
            return;

        foreach (var frameIndex in picked)
        {
            var fileName = $"{nameHint}_{frameIndex:D3}.png";
            var outPath = Path.Combine(folderBrowserDialog.SelectedPath, fileName);

            try
            {
                WriteFrameToPng(animation.Frames[frameIndex], outPath);
            } catch (Exception ex)
            {
                ShowMessage($"Failed to save {fileName} — {ex.Message}.", TimeSpan.FromSeconds(3));

                return;
            }
        }

        PathHelper.Instance.SaveRenderToPath = folderBrowserDialog.SelectedPath;
        PathHelper.Instance.Save();

        ShowMessage($"Saved {picked.Count} frames.");
    }

    private void SaveRenderForBatch(List<DataArchiveEntry> entries)
    {
        using var folderBrowserDialog = new FolderBrowserDialog();
        folderBrowserDialog.InitialDirectory = PathHelper.Instance.SaveRenderToPath!;

        if (folderBrowserDialog.ShowDialog() != DialogResult.OK)
            return;

        var totalFrames = 0;
        var savedEntries = 0;
        var skippedEntries = 0;

        foreach (var entry in entries)
        {
            using var animation = RenderUtil.TryRenderAnimation(
                Archive!,
                entry,
                ArchiveName,
                ArchiveRoot);

            if (animation is null || animation.Frames.Count == 0)
            {
                skippedEntries++;

                continue;
            }

            var nameHint = Path.GetFileNameWithoutExtension(entry.EntryName);

            for (var i = 0; i < animation.Frames.Count; i++)
            {
                var fileName = $"{nameHint}_{i:D3}.png";
                var outPath = Path.Combine(folderBrowserDialog.SelectedPath, fileName);

                try
                {
                    WriteFrameToPng(animation.Frames[i], outPath);
                } catch (Exception ex)
                {
                    ShowMessage($"Failed to save {fileName} — {ex.Message}.", TimeSpan.FromSeconds(3));

                    return;
                }

                totalFrames++;
            }

            savedEntries++;
        }

        PathHelper.Instance.SaveRenderToPath = folderBrowserDialog.SelectedPath;
        PathHelper.Instance.Save();

        var msg = skippedEntries == 0
            ? $"Saved {totalFrames} frames from {savedEntries} entries."
            : $"Saved {totalFrames} frames from {savedEntries} entries. ({skippedEntries} skipped.)";

        ShowMessage(msg);
    }

    private static void WriteFrameToPng(SKImage frame, string outPath)
    {
        using var data = frame.Encode(SKEncodedImageFormat.Png, 100);
        using var outStream = File.Create(outPath);
        data.SaveTo(outStream);
    }

    private void ExtractToBtn_OnClick(object sender, RoutedEventArgs e)
    {
        using var folderBrowserDialog = new FolderBrowserDialog();
        folderBrowserDialog.InitialDirectory = PathHelper.Instance.ArchiveExtractToToPath!;

        if (folderBrowserDialog.ShowDialog() == DialogResult.OK)
        {
            Archive?.ExtractTo(folderBrowserDialog.SelectedPath);

            PathHelper.Instance.ArchiveExtractToToPath = folderBrowserDialog.SelectedPath;
            PathHelper.Instance.Save();

            ShowMessage("Archive extracted successfully!");
        }
    }

    private void Load_OnClick(object sender, RoutedEventArgs e)
    {
        var openFileDialog = new OpenFileDialog
        {
            Filter = "Data Archive (*.dat)|*.dat",
            InitialDirectory = PathHelper.Instance.ArchiveLoadFromPath
        };

        if (openFileDialog.ShowDialog() == true)
        {
            LoadArchive(openFileDialog.FileName);

            PathHelper.Instance.ArchiveLoadFromPath = Path.GetDirectoryName(openFileDialog.FileName);
            PathHelper.Instance.Save();

            ShowMessage("Archive loaded successfully!");
        }
    }

    private void PatchBtn_OnClick(object sender, RoutedEventArgs e)
    {
        var openFileDialog = new OpenFileDialog
        {
            Filter = "All Files (*.*)|*.*",
            Multiselect = true,
            InitialDirectory = PathHelper.Instance.PatchFromPath
        };

        if (openFileDialog.ShowDialog() == true)
        {
            ArchivesView.ItemsSource = null;

            foreach (var file in openFileDialog.FileNames)
                PatchArchive(file);

            SetViewSource();
            ArchivesView.SelectedItems.Clear();

            foreach (var fileName in openFileDialog.FileNames)
            {
                var entryName = Path.GetFileName(fileName);
                var entry = Archive![entryName];
                ArchivesView.SelectedItems.Add(entry);
            }

            PathHelper.Instance.PatchFromPath = Path.GetDirectoryName(openFileDialog.FileName);
            PathHelper.Instance.Save();

            ShowMessage("Archive patched successfully!");
        }
    }
    #endregion

    #region Functionality
    private void ShowMessage(string message, TimeSpan? time = null)
        => Snackbar.MessageQueue?.Enqueue(
            message,
            null,
            null,
            null,
            false,
            true,
            time ?? TimeSpan.FromMilliseconds(500));

    public void LoadArchive(string path)
    {
        Archive?.Dispose();
        RenderUtil.Reset();

        try
        {
            Archive = DataArchive.FromFile(path, false);
        } catch
        {
            Archive = DataArchive.FromFile(path, false, true);
        }

        ArchiveName = Path.GetFileName(path);
        ArchiveRoot = Path.GetDirectoryName(path) ?? string.Empty;

        SetViewSource();

        CompileToBtn.IsEnabled = true;
        ExtractToBtn.IsEnabled = true;
        PatchBtn.IsEnabled = true;
        ExtractSelectionBtn.IsEnabled = false;
        SaveRenderBtn.IsEnabled = false;
    }

    private void PatchArchive(string path)
    {
        RenderUtil.Reset();

        var entryName = Path.GetFileName(path);

        using var entry = new PatchEntry(File.OpenRead(path));

        Archive!.Patch(entryName, entry);
    }

    private void SetViewSource()
    {
        if (Archive is not null)
        {
            Archive.Sort();

            ArchivesView.ItemsSource = new CollectionView(
                Archive.OrderBy(entry => Path.GetExtension(entry.EntryName), StringComparer.OrdinalIgnoreCase)
                       .GroupBy(entry => Path.GetExtension(entry.EntryName), StringComparer.OrdinalIgnoreCase)
                       .Select(group => new EntryGrouping(group.Key, group)));
        }
    }
    #endregion
}