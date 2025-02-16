using System.Windows;
using ChaosAssetManager.Helpers;
using ChaosAssetManager.ViewModel;

namespace ChaosAssetManager.Controls;

public partial class OptionsWindow
{
    public OptionsViewModel ViewModel { get; }

    public OptionsWindow()
    {
        ViewModel = new OptionsViewModel();

        InitializeComponent();
    }

    private void ArchivesDirectoryBtn_OnClick(object sender, RoutedEventArgs e)
    {
        using var dialog = new FolderBrowserDialog();
        dialog.Description = "Select the directory where the archives are located";
        dialog.ShowNewFolderButton = false;
        dialog.SelectedPath = ViewModel.ArchivesPath;

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            if (PathHelper.ArchivePathIsValid(dialog.SelectedPath))
                ViewModel.ArchivesPath = dialog.SelectedPath;
    }

    private void SaveBtn_OnClick(object sender, RoutedEventArgs e)
    {
        ViewModel.Save();
        Close();
    }
}