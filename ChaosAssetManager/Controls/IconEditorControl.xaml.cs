using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Chaos.Extensions.Common;
using ChaosAssetManager.Helpers;
using ChaosAssetManager.ViewModel;
using MaterialDesignThemes.Wpf;
using SkiaSharp;
using Button = System.Windows.Controls.Button;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using ListViewItem = System.Windows.Controls.ListViewItem;
using Orientation = System.Windows.Controls.Orientation;

namespace ChaosAssetManager.Controls;

public sealed partial class IconEditorControl
{
    public ObservableCollection<PanelSpriteViewModel> IconViewModels { get; } = [];

    public IconEditorControl()
    {
        InitializeComponent();

        PathHelper.ArchivesPathChanged += () => IconEditorControl_OnLoaded(this, new RoutedEventArgs());
    }

    private void ClearIconGrid()
    {
        IconGrid.ItemsSource = null;

        foreach (var viewModel in IconViewModels)
            viewModel.Dispose();

        IconViewModels.Clear();
    }

    private bool TryGetSelectedFamily(out bool skill)
    {
        skill = false;

        if (FamilyListView.SelectedItem is not ListViewItem { Tag: string tag })
            return false;

        skill = tag.EqualsI("skill");

        return true;
    }

    private async void Delete_OnClick(object sender, RoutedEventArgs e)
    {
        if (!TryGetSelectedFamily(out var skill))
            return;

        var selectedIcons = IconGrid.SelectedItems
                                    .OfType<PanelSpriteViewModel>()
                                    .Where(viewModel => !viewModel.IsEmpty)
                                    .ToList();

        if (selectedIcons.Count == 0)
            return;

        //checked before the dialog so the user is not asked to confirm a delete that would then be refused
        if (IconUtil.IsImportRunning)
        {
            Snackbar.MessageQueue!.Enqueue("An icon import is in progress — wait for it to finish before deleting icons.");

            return;
        }

        var message = selectedIcons.Count == 1
            ? $"Delete Icon #{selectedIcons[0].GlobalId}?"
            : $"Delete {selectedIcons.Count} icons?";

        var dialogContent = new StackPanel();

        dialogContent.Children.Add(
            new TextBlock
            {
                Text = message,
                Margin = new Thickness(
                    16,
                    16,
                    16,
                    0)
            });

        dialogContent.Children.Add(
            new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(16),
                Children =
                {
                    new Button
                    {
                        Content = "Cancel",
                        Margin = new Thickness(
                            0,
                            0,
                            8,
                            0),
                        Command = DialogHost.CloseDialogCommand,
                        CommandParameter = false
                    },
                    new Button
                    {
                        Content = "Delete",
                        Style = (Style)FindResource("MaterialDesignFlatButton"),
                        Command = DialogHost.CloseDialogCommand,
                        CommandParameter = true
                    }
                }
            });

        var archivesPath = PathHelper.Instance.ArchivesPath;

        var result = await DialogHost.Show(dialogContent, "RootDialog");

        if (result is not true)
            return;

        //the options window is not modal, so the path can change while the confirmation is open
        if (!string.Equals(archivesPath, PathHelper.Instance.ArchivesPath, StringComparison.OrdinalIgnoreCase))
        {
            Snackbar.MessageQueue!.Enqueue("Archives directory changed — delete cancelled");

            return;
        }

        //re-checked because an import could have started while the dialog was open - DeleteIcons would otherwise
        //block the ui thread on the archive lock until the import finishes
        if (IconUtil.IsImportRunning)
        {
            Snackbar.MessageQueue!.Enqueue("An icon import is in progress — wait for it to finish before deleting icons.");

            return;
        }

        try
        {
            var ids = selectedIcons.Select(viewModel => viewModel.GlobalId)
                                   .ToList();

            var archivePath = Path.Combine(archivesPath!, IconUtil.ARCHIVE_FILE_NAME);

            IconUtil.DeleteIcons(
                ArchiveCache.Setoa,
                archivePath,
                skill,
                ids);

            RenderUtil.Reset();

            Snackbar.MessageQueue!.Enqueue(ids.Count == 1 ? $"Deleted Icon #{ids[0]}" : $"Deleted {ids.Count} icons");

            LoadFamily(skill);
        } catch (IOException)
        {
            //the patches landed in the cached archive before the save failed - drop it so the retry starts from disk
            ArchiveCache.Clear();

            Snackbar.MessageQueue!.Enqueue("Failed to save archive! Close the game and try again.");
        } catch (Exception ex)
        {
            //any throw past the first patch leaves the cached archive dirty
            ArchiveCache.Clear();

            Snackbar.MessageQueue!.Enqueue($"Error: {ex.Message}");
        }
    }

    private void LoadFamily(bool skill)
    {
        ClearIconGrid();
        DeleteBtn.IsEnabled = false;

        //an import saves the archive on a worker thread via a StreamSegment over the shared stream - reading
        //the same archive here concurrently would interleave seeks and corrupt the save
        if (IconUtil.IsImportRunning)
        {
            InfoLabel.Text = "An icon import is in progress — reopen the Icon Editor when it finishes.";

            return;
        }

        try
        {
            //resolving the archive throws when setoa.dat is absent, so it cannot sit outside the try
            var setoa = ArchiveCache.Setoa;
            var normalSheetName = IconUtil.GetSheetNames(skill)[0];
            var normalEntryName = $"{normalSheetName}.epf";

            if (!setoa.Contains(normalEntryName))
            {
                InfoLabel.Text = $"{normalEntryName} not found in {IconUtil.ARCHIVE_FILE_NAME}";

                return;
            }

            if (!setoa.Contains(IconUtil.PALETTE_ENTRY_NAME))
            {
                InfoLabel.Text = $"{IconUtil.PALETTE_ENTRY_NAME} not found in {IconUtil.ARCHIVE_FILE_NAME}";

                return;
            }

            var sheet = IconUtil.GetSheet(setoa, normalSheetName);
            var palette = IconUtil.GetPalette(setoa);

            for (var id = 0; id < sheet.Count; id++)
            {
                var frame = sheet[id];
                var isEmpty = IconUtil.IsBlank(frame);
                SKImage? image = null;

                if (!isEmpty)
                    try
                    {
                        image = IconUtil.RenderFrameUnpadded(frame, palette);
                    } catch
                    {
                        isEmpty = true;
                    }

                IconViewModels.Add(
                    new PanelSpriteViewModel
                    {
                        GlobalId = id,
                        SlotIndex = id,
                        Image = image,
                        IsEmpty = isEmpty
                    });
            }

            IconGrid.ItemsSource = IconViewModels;
            UpdateSummaryLabel(skill);
        } catch (Exception ex)
        {
            var message = $"Error loading icons: {ex.Message}";

            InfoLabel.Text = message;
            Snackbar.MessageQueue!.Enqueue(message);
        }
    }

    private void UpdateSummaryLabel(bool skill)
    {
        var label = skill ? "Skill" : "Spell";

        InfoLabel.Text = $"{label} icons — {IconViewModels.Count(viewModel => !viewModel.IsEmpty)} of {IconViewModels.Count}";
    }

    private void Family_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!TryGetSelectedFamily(out var skill))
            return;

        LoadFamily(skill);
    }

    private void IconEditorControl_OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible && PathHelper.ArchivePathIsValid(PathHelper.Instance.ArchivesPath))
            ReloadSelectedFamily();
    }

    private void IconEditorControl_OnLoaded(object sender, RoutedEventArgs e)
    {
        if (!PathHelper.ArchivePathIsValid(PathHelper.Instance.ArchivesPath))
        {
            MainContent.Visibility = Visibility.Collapsed;
            NotConfiguredMessage.Visibility = Visibility.Visible;

            return;
        }

        NotConfiguredMessage.Visibility = Visibility.Collapsed;
        MainContent.Visibility = Visibility.Visible;

        //Loaded fires for collapsed controls at startup, so IsVisibleChanged drives the first load instead
        if (IsVisible)
            ReloadSelectedFamily();
    }

    private void ReloadSelectedFamily()
    {
        //setting the index fires Family_OnSelectionChanged, so only reload directly when it is already set
        if (FamilyListView.SelectedIndex < 0)
            FamilyListView.SelectedIndex = 0;
        else if (TryGetSelectedFamily(out var skill))
            LoadFamily(skill);
    }

    private void IconGrid_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var selectedIcons = IconGrid.SelectedItems
                                    .OfType<PanelSpriteViewModel>()
                                    .Where(viewModel => !viewModel.IsEmpty)
                                    .ToList();

        DeleteBtn.IsEnabled = selectedIcons.Count > 0;

        if (selectedIcons.Count == 1)
            InfoLabel.Text = $"Icon #{selectedIcons[0].GlobalId}";
        else if (selectedIcons.Count > 1)
            InfoLabel.Text = $"{selectedIcons.Count} icons selected";
        else if (TryGetSelectedFamily(out var skill))
            UpdateSummaryLabel(skill);
    }
}
