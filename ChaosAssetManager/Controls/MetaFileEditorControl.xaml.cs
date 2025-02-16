using System.IO;
using System.Windows;
using System.Windows.Controls;
using Chaos.Wpf.Observables;
using ChaosAssetManager.Extensions;
using ChaosAssetManager.Helpers;
using ChaosAssetManager.ViewModel;
using DALib.Data;
using Button = System.Windows.Controls.Button;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace ChaosAssetManager.Controls;

public sealed partial class MetaFileEditorControl
{
    public MetaFileViewModel? MetaFileViewModel { get; set; }

    public MetaFileEditorControl() => InitializeComponent();

    private void AddEntryBtn_OnClick(object sender, RoutedEventArgs e)
    {
        var button = (Button)sender;
        var parent = button.FindVisualParent<TreeViewItem>();

        if (parent is null)
            return;

        var currentEntry = (MetaFileEntryViewModel)parent.DataContext;
        var currentEntryIndex = MetaFileViewModel!.Entries.IndexOf(currentEntry);

        if (currentEntryIndex == -1)
            return;

        currentEntryIndex++;
        var newEntry = new MetaFileEntry("NEW_ENTRY", [""]);
        MetaFileViewModel!.Entries.Insert(currentEntryIndex, new MetaFileEntryViewModel(newEntry));
    }

    private void AddPropertyBtn_OnClick(object sender, RoutedEventArgs e)
    {
        var button = (Button)sender;
        var treeViewItem = button.FindVisualParent<TreeViewItem>();
        var parent = treeViewItem?.FindVisualParent<TreeViewItem>();

        if (parent is null)
            return;

        var currentProperty = (BindableString)button.DataContext;
        var currentEntry = (MetaFileEntryViewModel)parent.DataContext;
        var currentPropertyIndex = parent.Items.IndexOf(currentProperty);

        if (currentPropertyIndex == -1)
            return;

        currentPropertyIndex++;
        currentEntry.Properties.Insert(currentPropertyIndex, new BindableString());
    }

    private void Load_OnClick(object sender, RoutedEventArgs e)
    {
        var fileDialog = new OpenFileDialog
        {
            InitialDirectory = PathHelper.Instance.MetaFileEditorFromPath
        };

        if (fileDialog.ShowDialog() == false)
            return;

        if (string.IsNullOrEmpty(fileDialog.FileName) || (fileDialog.FileNames.Length > 1))
            return;

        var metaFile = MetaFile.FromFile(fileDialog.FileName, true);

        MetaFileTreeView.ItemsSource = null;
        MetaFileViewModel = new MetaFileViewModel(metaFile);
        MetaFileTreeView.ItemsSource = MetaFileViewModel.Entries;

        PathHelper.Instance.MetaFileEditorFromPath = Path.GetDirectoryName(fileDialog.FileName);
        PathHelper.Instance.Save();
    }

    private void RemoveEntryBtn_OnClick(object sender, RoutedEventArgs e)
    {
        var button = (Button)sender;
        var parent = button.FindVisualParent<TreeViewItem>();

        if (parent is null)
            return;

        var currentEntry = (MetaFileEntryViewModel)parent.DataContext;
        MetaFileViewModel!.Entries.Remove(currentEntry);
    }

    private void RemovePropertyBtn_OnClick(object sender, RoutedEventArgs e)
    {
        var button = (Button)sender;
        var treeViewItem = button.FindVisualParent<TreeViewItem>();
        var parent = treeViewItem?.FindVisualParent<TreeViewItem>();

        if (parent is null)
            return;

        var currentProperty = (BindableString)button.DataContext;
        var currentEntry = (MetaFileEntryViewModel)parent.DataContext;

        currentEntry.Properties.Remove(currentProperty);
    }

    private void Save_OnClick(object sender, RoutedEventArgs e)
    {
        if (MetaFileViewModel is null)
            return;

        var metaFile = new MetaFile();

        foreach (var entryViewModel in MetaFileViewModel.Entries)
        {
            var entry = new MetaFileEntry(entryViewModel.Key, entryViewModel.Properties.Select(str => str.String));
            metaFile.Add(entry);
        }

        var saveDialog = new SaveFileDialog
        {
            InitialDirectory = PathHelper.Instance.MetaFileEditorToPath
        };

        if (saveDialog.ShowDialog() == false)
            return;

        metaFile.Save(saveDialog.FileName);

        PathHelper.Instance.MetaFileEditorToPath = Path.GetDirectoryName(saveDialog.FileName);
        PathHelper.Instance.Save();
    }
}