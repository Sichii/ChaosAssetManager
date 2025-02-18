using System.IO;
using System.Windows;
using Chaos.Extensions.Common;
using ChaosAssetManager.Controls;
using MaterialDesignThemes.Wpf;

namespace ChaosAssetManager;

/// <summary>
///     Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        Width = SystemParameters.PrimaryScreenWidth * 0.5;
        Height = SystemParameters.PrimaryScreenHeight * 0.5;
    }

    private void CloseBtn_OnClick(object sender, RoutedEventArgs e) => SystemCommands.CloseWindow(this);

    private void MainWindow_OnStateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Maximized)
        {
            BorderThickness = new Thickness(7);

            MaximizeBtn.Content = new PackIcon
            {
                Kind = PackIconKind.WindowRestore,
                FontSize = 18
            };
        } else
        {
            BorderThickness = new Thickness(0);

            MaximizeBtn.Content = new PackIcon
            {
                Kind = PackIconKind.WindowMaximize,
                FontSize = 18
            };
        }
    }

    private void MaximizeBtn_OnClick(object sender, RoutedEventArgs e)
    {
        if (WindowState == WindowState.Maximized)
            SystemCommands.RestoreWindow(this);
        else
            SystemCommands.MaximizeWindow(this);
    }

    private void MinimizeBtn_OnClick(object sender, RoutedEventArgs e) => SystemCommands.MinimizeWindow(this);

    private void SettingsBtn_OnClick(object sender, RoutedEventArgs e)
    {
        var options = new OptionsWindow
        {
            Owner = this
        };

        options.Show();
    }

    private void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
    {
        var args = Environment.GetCommandLineArgs()
                              .Skip(1)
                              .ToArray();

        //if we're given a single arg and it's a dat file
        //open it up in the archive viewer
        if (args.Length == 1)
        {
            var extension = Path.GetExtension(args[0]);

            if (extension.EqualsI(".dat"))
            {
                MainTabControl.SelectedItem = ArchivesTab;
                ArchivesView.LoadArchive(args[0]);
            } else if (extension.EqualsI(".map"))
            {
                MainTabControl.SelectedItem = MapEditorTab;
                MapEditorView.LoadMap(args[0]);
            } else if (string.IsNullOrEmpty(extension))
            {
                MainTabControl.SelectedItem = MetaFileEditorTab;
                MetaFileEditorView.LoadMetaData(args[0]);
            }
        } else if (args.All(
                       arg => Path.GetExtension(arg)
                                  .EqualsI(".map")))
            foreach (var arg in args)
            {
                MainTabControl.SelectedItem = MapEditorTab;
                MapEditorView.LoadMap(arg);
            }
    }
}