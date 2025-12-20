using System.IO;
using System.Windows;
using ChaosAssetManager.Helpers;
using ChaosAssetManager.ViewModel;
using Microsoft.Win32;

namespace ChaosAssetManager.Controls;

public partial class OptionsWindow
{
    public OptionsViewModel ViewModel { get; }
    public Visibility MapAssociationVisibility => IsExtensionAssociated(".map") ? Visibility.Collapsed : Visibility.Visible;
    public Visibility DatAssociationVisibility => IsExtensionAssociated(".dat") ? Visibility.Collapsed : Visibility.Visible;

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

    private void AssociateMapBtn_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            AssociateFileExtension(".map", "ChaosAssetManager.MapFile", "Map File");
            AssociateMapBtn.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Failed to associate .map files: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void AssociateDatBtn_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            AssociateFileExtension(".dat", "ChaosAssetManager.DatFile", "Archive File");
            AssociateDatBtn.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Failed to associate .dat files: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static void AssociateFileExtension(string extension, string progId, string description)
    {
        var exePath = Environment.ProcessPath!;

        using (var extensionKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{extension}"))
            extensionKey.SetValue(string.Empty, progId);

        using (var progIdKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{progId}"))
        {
            progIdKey.SetValue(string.Empty, description);

            using (var commandKey = progIdKey.CreateSubKey(@"shell\open\command"))
                commandKey.SetValue(string.Empty, $"\"{exePath}\" \"%1\"");
        }
    }

    private static bool IsExtensionAssociated(string extension)
    {
        // Check Windows 8+ UserChoice first (used by "Open with" dialog)
        using (var userChoiceKey = Registry.CurrentUser.OpenSubKey($@"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\{extension}\UserChoice"))
        {
            if (userChoiceKey?.GetValue("ProgId") is string userChoiceProgId)
            {
                var command = GetCommandForProgId(userChoiceProgId);

                if (IsValidChaosAssetManagerCommand(command))
                    return true;
            }
        }

        // Check traditional HKCU\Software\Classes association
        using (var extensionKey = Registry.CurrentUser.OpenSubKey($@"Software\Classes\{extension}"))
        {
            if (extensionKey?.GetValue(string.Empty) is string progId)
            {
                var command = GetCommandForProgId(progId);

                if (IsValidChaosAssetManagerCommand(command))
                    return true;
            }
        }

        return false;
    }

    private static bool IsValidChaosAssetManagerCommand(string? command)
    {
        if (command is null || !command.Contains("ChaosAssetManager", StringComparison.OrdinalIgnoreCase))
            return false;

        // Extract the executable path from the command
        // Format is typically: "C:\path\to\app.exe" "%1" or C:\path\to\app.exe "%1"
        string exePath;

        if (command.StartsWith('"'))
        {
            var endQuote = command.IndexOf('"', 1);

            if (endQuote == -1)
                return false;

            exePath = command[1..endQuote];
        }
        else
        {
            var space = command.IndexOf(' ');
            exePath = space == -1 ? command : command[..space];
        }

        return File.Exists(exePath);
    }

    private static string? GetCommandForProgId(string progId)
    {
        // Check HKCU first
        using (var commandKey = Registry.CurrentUser.OpenSubKey($@"Software\Classes\{progId}\shell\open\command"))
        {
            if (commandKey?.GetValue(string.Empty) is string command)
                return command;
        }

        // Fall back to HKLM
        using (var commandKey = Registry.LocalMachine.OpenSubKey($@"Software\Classes\{progId}\shell\open\command"))
        {
            if (commandKey?.GetValue(string.Empty) is string command)
                return command;
        }

        return null;
    }
}