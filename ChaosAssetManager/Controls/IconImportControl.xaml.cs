using System.IO;
using System.Windows;
using ChaosAssetManager.Helpers;
using Microsoft.Win32;

namespace ChaosAssetManager.Controls;

public sealed partial class IconImportControl
{
    public IconImportControl()
    {
        InitializeComponent();

        InputPathTxt.Text = PathHelper.Instance.IconImportFromPath ?? string.Empty;
        PathHelper.ArchivesPathChanged += () => IconImportControl_OnLoaded(this, new RoutedEventArgs());
    }

    private void BrowseInputBtn_OnClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select folder containing PNG icons",
            InitialDirectory = PathHelper.Instance.IconImportFromPath
        };

        if (dialog.ShowDialog() != true)
            return;

        if (Directory.GetFiles(dialog.FolderName, "*.png").Length == 0)
        {
            Snackbar.MessageQueue!.Enqueue("Selected folder does not contain any PNG files");

            return;
        }

        InputPathTxt.Text = dialog.FolderName;
        PathHelper.Instance.IconImportFromPath = dialog.FolderName;
        PathHelper.Instance.Save();
    }

    private async void ImportBtn_OnClick(object sender, RoutedEventArgs e)
    {
        var archivesPath = PathHelper.Instance.ArchivesPath;
        var inputPath = InputPathTxt.Text;

        if (string.IsNullOrEmpty(archivesPath) || !PathHelper.ArchivePathIsValid(archivesPath))
        {
            Snackbar.MessageQueue!.Enqueue("Please set a valid Archives Directory in Settings (gear icon)");

            return;
        }

        if (string.IsNullOrEmpty(inputPath) || !Directory.Exists(inputPath))
        {
            Snackbar.MessageQueue!.Enqueue("Please select an input path");

            return;
        }

        var setoaPath = Path.Combine(archivesPath, IconUtil.ARCHIVE_FILE_NAME);

        if (!File.Exists(setoaPath))
        {
            Snackbar.MessageQueue!.Enqueue($"{IconUtil.ARCHIVE_FILE_NAME} not found in configured Archives Directory");

            return;
        }

        var skill = SkillRadio.IsChecked == true;

        ImportBtn.IsEnabled = false;

        try
        {
            //the options window is not modal, so resolve the archive here rather than on the worker - otherwise a path
            //change between the click and the worker starting would load the new install's setoa but save to setoaPath
            var setoa = ArchiveCache.Setoa;

            var importedCount = await Task.Run(() =>
            {
                var sourceImages = IconUtil.LoadSourceImages(inputPath);

                return IconUtil.ImportIcons(
                    setoa,
                    setoaPath,
                    skill,
                    sourceImages);
            });

            if (importedCount == 0)
            {
                Snackbar.MessageQueue!.Enqueue("No usable PNG images found in the selected folder");

                return;
            }

            //refresh the in-memory archives and render caches so the new icons show without a restart
            CacheManager.RefreshAfterImport();

            Snackbar.MessageQueue!.Enqueue($"Successfully imported {importedCount} icons");
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
        } finally
        {
            ImportBtn.IsEnabled = true;
        }
    }

    private void IconImportControl_OnLoaded(object sender, RoutedEventArgs e)
    {
        if (!PathHelper.ArchivePathIsValid(PathHelper.Instance.ArchivesPath))
        {
            MainContent.Visibility = Visibility.Collapsed;
            InfoMessage.Visibility = Visibility.Collapsed;
            BottomInfoMessage.Visibility = Visibility.Collapsed;
            NotConfiguredMessage.Visibility = Visibility.Visible;
        } else
        {
            NotConfiguredMessage.Visibility = Visibility.Collapsed;
            MainContent.Visibility = Visibility.Visible;
            InfoMessage.Visibility = Visibility.Visible;
            BottomInfoMessage.Visibility = Visibility.Visible;
        }
    }
}
