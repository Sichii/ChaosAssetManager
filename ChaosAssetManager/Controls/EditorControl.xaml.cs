using System.Windows;
using DALib.Abstractions;
using DALib.Drawing;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace ChaosAssetManager.Controls;

public partial class EditorControl
{
    private ISavable? CurrentItem;
    private string? CurrentPath;

    public EditorControl() => InitializeComponent();

    private void Load_OnClick(object sender, RoutedEventArgs e)
    {
        (ContentPanel.Content as IDisposable)?.Dispose();
        ContentPanel.Content = null;
        CurrentPath = null;

        var openFileDialog = new OpenFileDialog
        {
            Filter = "DA Graphics|*.efa"
        };

        if (openFileDialog.ShowDialog() == false)
            return;

        if (string.IsNullOrEmpty(openFileDialog.FileName) || (openFileDialog.FileNames.Length > 1))
            return;

        CurrentPath = openFileDialog.FileName;
        var efaFile = EfaFile.FromFile(CurrentPath);
        CurrentItem = efaFile;

        ContentPanel.Content = new EfaEditor(efaFile);
    }

    private void Save_OnClick(object sender, RoutedEventArgs e)
    {
        var saveFileDialog = new SaveFileDialog
        {
            Filter = "DA Graphics|*.efa"
        };

        if (saveFileDialog.ShowDialog() == false)
            return;

        CurrentItem?.Save(saveFileDialog.FileName);
    }
}