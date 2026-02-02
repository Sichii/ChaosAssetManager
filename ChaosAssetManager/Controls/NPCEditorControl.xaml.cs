using System.IO;
using System.Windows;
using System.Windows.Controls;
using ChaosAssetManager.Helpers;
using DALib.Data;
using DALib.Drawing;
using ListViewItem = System.Windows.Controls.ListViewItem;

namespace ChaosAssetManager.Controls;

public sealed partial class NPCEditorControl
{
    private string? CurrentEntryName;

    public NPCEditorControl()
    {
        InitializeComponent();

        PathHelper.ArchivesPathChanged += () => NPCEditorControl_OnLoaded(this, new RoutedEventArgs());
    }

    private void NPC_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (NPCListView.SelectedItem is not ListViewItem listViewItem)
            return;

        //first time loading - tag is DataArchiveEntry, convert to MpfFile
        if (listViewItem.Tag is DataArchiveEntry entry)
        {
            var mpfFile = MpfFile.FromEntry(entry);
            listViewItem.Tag = mpfFile;
        }

        if (listViewItem is { Tag: MpfFile mpf, Content: string entryName })
            LoadNPC(entryName, mpf);
    }

    private void NPCEditorControl_OnLoaded(object sender, RoutedEventArgs e)
    {
        if (!PathHelper.ArchivePathIsValid(PathHelper.Instance.ArchivesPath))
        {
            MainContent.Visibility = Visibility.Collapsed;
            NotConfiguredMessage.Visibility = Visibility.Visible;

            return;
        }

        NotConfiguredMessage.Visibility = Visibility.Collapsed;
        MainContent.Visibility = Visibility.Visible;

        PopulateNPCList();
    }

    private void NPCEditorControl_OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible && PathHelper.ArchivePathIsValid(PathHelper.Instance.ArchivesPath))
            PopulateNPCList();
    }

    private void LoadNPC(string entryName, MpfFile mpfFile)
    {
        //dispose previous content
        (ContentPanel.Content as IDisposable)?.Dispose();
        ContentPanel.Content = null;
        CurrentEntryName = entryName;

        var archive = ArchiveCache.Hades;

        //get palette
        var palettes = Palette.FromArchive("mns", archive);

        if (!palettes.TryGetValue(mpfFile.PaletteNumber, out var palette))
        {
            if (palettes.Count > 0)
                palette = palettes.Values.First();
            else
            {
                Snackbar.MessageQueue!.Enqueue($"Could not find palette for {entryName}");

                return;
            }
        }

        ContentPanel.Content = new NPCContentEditorControl(mpfFile, palette);
    }

    private void PopulateNPCList()
    {
        NPCListView.Items.Clear();

        var archive = ArchiveCache.Hades;

        //find all mns*.mpf entries and sort numerically
        var npcEntries = archive.Where(e => e.EntryName.StartsWith("mns", StringComparison.OrdinalIgnoreCase)
                                            && e.EntryName.EndsWith(".mpf", StringComparison.OrdinalIgnoreCase))
                                .OrderBy(e => e.TryGetNumericIdentifier(out var num) ? num : int.MaxValue)
                                .ToList();

        foreach (var entry in npcEntries)
            NPCListView.Items.Add(
                new ListViewItem
                {
                    Content = entry.EntryName,
                    Tag = entry
                });
    }

    private void Save_OnClick(object sender, RoutedEventArgs e)
    {
        if (ContentPanel.Content is not NPCContentEditorControl editor)
        {
            Snackbar.MessageQueue!.Enqueue("No NPC loaded to save");

            return;
        }

        if (string.IsNullOrEmpty(CurrentEntryName))
        {
            Snackbar.MessageQueue!.Enqueue("No NPC selected");

            return;
        }

        var archive = ArchiveCache.Hades;

        //save MPF file
        archive.Patch(CurrentEntryName, editor.MpfFile);

        //save archive to disk
        var archivePath = Path.Combine(PathHelper.Instance.ArchivesPath!, "hades.dat");
        archive.Save(archivePath);

        Snackbar.MessageQueue!.Enqueue($"Saved {CurrentEntryName}");
    }
}
