using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using ChaosAssetManager.Helpers;
using ChaosAssetManager.ViewModel;
using DALib.Data;
using DALib.Drawing;
using MaterialDesignThemes.Wpf;
using SkiaSharp;
using ListViewItem = System.Windows.Controls.ListViewItem;

namespace ChaosAssetManager.Controls;

public sealed partial class PanelSpriteEditorControl
{
    private const int ITEMS_PER_PAGE = 266;
    public ObservableCollection<PanelSpriteViewModel> SpriteViewModels { get; } = [];

    public PanelSpriteEditorControl()
    {
        InitializeComponent();

        PathHelper.ArchivesPathChanged += () => PanelSpriteEditorControl_OnLoaded(this, new RoutedEventArgs());
    }

    private void PanelSpriteEditorControl_OnLoaded(object sender, RoutedEventArgs e)
    {
        if (!PathHelper.ArchivePathIsValid(PathHelper.Instance.ArchivesPath))
        {
            MainContent.Visibility = Visibility.Collapsed;
            NotConfiguredMessage.Visibility = Visibility.Visible;

            return;
        }

        NotConfiguredMessage.Visibility = Visibility.Collapsed;
        MainContent.Visibility = Visibility.Visible;

        PopulatePageList();
    }

    private void PopulatePageList()
    {
        PageListView.Items.Clear();
        ClearSpriteGrid();

        var legend = ArchiveCache.Legend;

        var entries = legend.GetEntries("item", ".epf")
                            .Where(entry => entry.TryGetNumericIdentifier(out var id, 3) && (id >= 1))
                            .OrderBy(entry => entry.TryGetNumericIdentifier(out var id, 3) ? id : int.MaxValue)
                            .ToList();

        foreach (var entry in entries)
        {
            if (!entry.TryGetNumericIdentifier(out var pageId, 3))
                continue;

            PageListView.Items.Add(
                new ListViewItem
                {
                    Content = entry.EntryName,
                    Tag = pageId
                });
        }

        PageCountLabel.Text = $"Pages ({entries.Count})";
    }

    private void Page_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PageListView.SelectedItem is not ListViewItem { Tag: int pageId })
            return;

        LoadPage(pageId);
    }

    private void LoadPage(int pageId)
    {
        ClearSpriteGrid();
        DeleteBtn.IsEnabled = false;
        InfoLabel.Text = "Select a page to view its sprites";

        var legend = ArchiveCache.Legend;
        var entryName = $"item{pageId:D3}.epf";

        if (!legend.Contains(entryName))
            return;

        try
        {
            var epf = EpfFile.FromEntry(legend[entryName]);
            var paletteLookup = PaletteLookup.FromArchive("itempal", "item", legend);
            var pageIndex = pageId - 1;

            for (var slot = 0; slot < ITEMS_PER_PAGE; slot++)
            {
                var globalId = pageIndex * ITEMS_PER_PAGE + slot + 1;
                var frame = epf.ElementAtOrDefault(slot);

                var isEmpty = (frame is null)
                              || (frame.Data.Length == 0)
                              || (frame.PixelWidth <= 1)
                              || (frame.PixelHeight <= 1)
                              || frame.Data.All(b => b == 0);

                SKImage? image = null;

                if (!isEmpty)
                {
                    try
                    {
                        var palette = paletteLookup.GetPaletteForId(globalId);
                        image = DALib.Drawing.Graphics.RenderImage(frame!, palette);
                    }
                    catch
                    {
                        isEmpty = true;
                    }
                }

                SpriteViewModels.Add(
                    new PanelSpriteViewModel
                    {
                        GlobalId = globalId,
                        SlotIndex = slot,
                        Image = image,
                        IsEmpty = isEmpty
                    });
            }

            SpriteGrid.ItemsSource = SpriteViewModels;
            InfoLabel.Text = $"Page {pageId} — {SpriteViewModels.Count(vm => !vm.IsEmpty)} sprites";
        }
        catch (Exception ex)
        {
            Snackbar.MessageQueue!.Enqueue($"Error loading page: {ex.Message}");
        }
    }

    private void SpriteGrid_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var selectedItems = SpriteGrid.SelectedItems
                                      .OfType<PanelSpriteViewModel>()
                                      .Where(vm => !vm.IsEmpty)
                                      .ToList();

        DeleteBtn.IsEnabled = selectedItems.Count > 0;

        if (selectedItems.Count == 1)
        {
            var vm = selectedItems[0];

            try
            {
                var legend = ArchiveCache.Legend;
                var paletteLookup = PaletteLookup.FromArchive("itempal", "item", legend);
                var paletteNumber = paletteLookup.Table.GetPaletteNumber(vm.GlobalId);

                InfoLabel.Text = $"Sprite #{vm.GlobalId}  |  Slot: {vm.SlotIndex}  |  Palette: item{paletteNumber:D3}.pal";
            }
            catch
            {
                InfoLabel.Text = $"Sprite #{vm.GlobalId}  |  Slot: {vm.SlotIndex}";
            }
        }
        else if (selectedItems.Count > 1)
            InfoLabel.Text = $"{selectedItems.Count} sprites selected";
        else
        {
            //reset info if only empty slots are selected or nothing
            var currentPage = PageListView.SelectedItem is ListViewItem { Tag: int pid } ? pid : -1;

            if (currentPage > 0)
                InfoLabel.Text = $"Page {currentPage} — {SpriteViewModels.Count(vm => !vm.IsEmpty)} sprites";
            else
                InfoLabel.Text = "Select a page to view its sprites";
        }
    }

    private async void Delete_OnClick(object sender, RoutedEventArgs e)
    {
        var selectedSprites = SpriteGrid.SelectedItems
                                        .OfType<PanelSpriteViewModel>()
                                        .Where(vm => !vm.IsEmpty)
                                        .ToList();

        if (selectedSprites.Count == 0)
            return;

        var msg = selectedSprites.Count == 1
            ? $"Delete Sprite #{selectedSprites[0].GlobalId}?"
            : $"Delete {selectedSprites.Count} sprites?";

        var dialogContent = new StackPanel();

        dialogContent.Children.Add(
            new TextBlock
            {
                Text = msg,
                Margin = new Thickness(16, 16, 16, 0)
            });

        dialogContent.Children.Add(
            new StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                Margin = new Thickness(16),
                Children =
                {
                    new System.Windows.Controls.Button
                    {
                        Content = "Cancel",
                        Margin = new Thickness(0, 0, 8, 0),
                        Command = DialogHost.CloseDialogCommand,
                        CommandParameter = false
                    },
                    new System.Windows.Controls.Button
                    {
                        Content = "Delete",
                        Style = (Style)FindResource("MaterialDesignFlatButton"),
                        Command = DialogHost.CloseDialogCommand,
                        CommandParameter = true
                    }
                }
            });

        var result = await DialogHost.Show(dialogContent, "RootDialog");

        if (result is not true)
            return;

        try
        {
            var ids = selectedSprites.Select(vm => vm.GlobalId)
                                     .ToList();

            var removedPages = DeleteSprites(ids);

            var label = ids.Count == 1
                ? $"Deleted Sprite #{ids[0]}"
                : $"Deleted {ids.Count} sprites";

            Snackbar.MessageQueue!.Enqueue(label);

            //if a page was removed, refresh the page list; otherwise reload current page
            if (PageListView.SelectedItem is ListViewItem { Tag: int pageId } && removedPages.Contains(pageId))
                PopulatePageList();
            else if (PageListView.SelectedItem is ListViewItem { Tag: int currentPageId })
                LoadPage(currentPageId);
        }
        catch (IOException)
        {
            Snackbar.MessageQueue!.Enqueue("Failed to save archive! Close the game and try again.");
        }
        catch (Exception ex)
        {
            Snackbar.MessageQueue!.Enqueue($"Error: {ex.Message}");
        }
    }

    private HashSet<int> DeleteSprites(List<int> spriteIds)
    {
        var legend = ArchiveCache.Legend;
        var legendPath = Path.Combine(PathHelper.Instance.ArchivesPath!, "legend.dat");

        //load palette lookup (not frozen, so we can modify the table)
        var palettes = PaletteLookup.FromArchive("itempal", "item", legend);

        //collect palette numbers before removal, and track which epfs need patching
        var paletteNumbersToCheck = new HashSet<int>();
        var modifiedEpfs = new Dictionary<int, EpfFile>();

        foreach (var spriteId in spriteIds)
        {
            var pageId = ((spriteId - 1) / ITEMS_PER_PAGE) + 1;
            var slotInPage = (spriteId - 1) % ITEMS_PER_PAGE;

            //get palette number before removing
            var paletteNumber = palettes.Table.GetPaletteNumber(spriteId);

            if (paletteNumber > 0)
                paletteNumbersToCheck.Add(paletteNumber);

            //remove from palette table
            palettes.Table.Remove(spriteId);

            //load epf if not already loaded
            var epfEntryName = $"item{pageId:D3}.epf";

            if (!modifiedEpfs.TryGetValue(pageId, out var epf))
            {
                epf = EpfFile.FromEntry(legend[epfEntryName]);
                modifiedEpfs[pageId] = epf;
            }

            //clear the frame
            epf[slotInPage] = new EpfFrame { Data = [] };
        }

        //check which palettes are still in use
        if (paletteNumbersToCheck.Count > 0)
        {
            var allEntries = legend.GetEntries("item", ".epf")
                                   .OrderBy(entry => entry.TryGetNumericIdentifier(out var id, 3) ? id : int.MaxValue)
                                   .ToList();

            var deletedSet = new HashSet<int>(spriteIds);
            var usedPalettes = new HashSet<int>();

            foreach (var entry in allEntries)
            {
                if (!entry.TryGetNumericIdentifier(out var entryPageId, 3) || (entryPageId < 1))
                    continue;

                //use the modified version if we have it, otherwise load from archive
                EpfFile entryEpf;

                if (modifiedEpfs.TryGetValue(entryPageId, out var modified))
                    entryEpf = modified;
                else
                    entryEpf = EpfFile.FromEntry(entry);

                var entryPageIndex = entryPageId - 1;

                for (var slot = 0; slot < entryEpf.Count; slot++)
                {
                    var id = entryPageIndex * ITEMS_PER_PAGE + slot + 1;

                    if (deletedSet.Contains(id))
                        continue;

                    var frame = entryEpf[slot];

                    //skip empty frames
                    if ((frame.Data.Length == 0)
                        || (frame.PixelWidth <= 1)
                        || (frame.PixelHeight <= 1)
                        || frame.Data.All(b => b == 0))
                        continue;

                    var palNum = palettes.Table.GetPaletteNumber(id);

                    if (paletteNumbersToCheck.Contains(palNum))
                        usedPalettes.Add(palNum);
                }

                //early exit if all palettes are confirmed used
                if (usedPalettes.Count == paletteNumbersToCheck.Count)
                    break;
            }

            //remove orphaned palette files
            foreach (var paletteNumber in paletteNumbersToCheck)
            {
                if (usedPalettes.Contains(paletteNumber))
                    continue;

                var palEntryName = $"item{paletteNumber:D3}.pal";

                if (legend.Contains(palEntryName))
                    legend.Remove(legend[palEntryName]);
            }
        }

        //patch or remove modified epfs
        var removedPages = new HashSet<int>();

        foreach ((var pageId, var epf) in modifiedEpfs)
        {
            var epfEntryName = $"item{pageId:D3}.epf";

            //check if all frames in the page are now empty
            var allEmpty = true;

            for (var slot = 0; slot < epf.Count; slot++)
            {
                var frame = epf[slot];

                if ((frame.Data.Length > 0)
                    && (frame.PixelWidth > 1)
                    && (frame.PixelHeight > 1)
                    && !frame.Data.All(b => b == 0))
                {
                    allEmpty = false;

                    break;
                }
            }

            if (allEmpty)
            {
                //remove the entire page from the archive
                if (legend.Contains(epfEntryName))
                    legend.Remove(legend[epfEntryName]);

                removedPages.Add(pageId);
            }
            else
                legend.Patch(epfEntryName, epf);
        }

        //patch palette table and save
        legend.Patch("itempal.tbl", palettes.Table);
        legend.Save(legendPath);

        //invalidate cached palette lookups
        RenderUtil.Reset();

        return removedPages;
    }

    private void ClearSpriteGrid()
    {
        SpriteGrid.ItemsSource = null;

        foreach (var vm in SpriteViewModels)
            vm.Dispose();

        SpriteViewModels.Clear();
    }
}
