using System.IO;
using System.Windows;
using System.Windows.Interop;
using Chaos.Extensions.Common;
using ChaosAssetManager.Controls;
using ChaosAssetManager.Helpers;
using MaterialDesignThemes.Wpf;
using WindowState = System.Windows.WindowState;

namespace ChaosAssetManager;

/// <summary>
///     Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private Rectangle? RestoreBounds;

    public MainWindow()
    {
        InitializeComponent();

        Width = SystemParameters.PrimaryScreenWidth * 0.5;
        Height = SystemParameters.PrimaryScreenHeight * 0.5;
    }

    private void CloseBtn_OnClick(object sender, RoutedEventArgs e) => SystemCommands.CloseWindow(this);

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
        } else if (args.All(arg => Path.GetExtension(arg)
                                       .EqualsI(".map")))
            foreach (var arg in args)
            {
                MainTabControl.SelectedItem = MapEditorTab;
                MapEditorView.LoadMap(arg);
            }
    }

    private void MainWindow_OnStateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Maximized)
        {
            //BorderThickness = new Thickness(0);

            MaximizeBtn.Content = new PackIcon
            {
                Kind = PackIconKind.WindowRestore,
                FontSize = 18
            };

            CustomChrome.CornerRadius = new CornerRadius(0);
            WindowBorder.CornerRadius = new CornerRadius(0);
            WindowBorder.BorderThickness = new Thickness(0);

            // get the HWND for this WPF window
            var h = new WindowInteropHelper(this).Handle;

            // figure out which Screen it's on
            var screen = Screen.FromHandle(h);

            // grab *that* screen's work‐area
            var workingArea = screen.WorkingArea;
            var dpiScale = DpiHelper.GetDpiScaleFactor();

            Top = workingArea.Top;
            Left = workingArea.Left;
            MaxHeight = workingArea.Height / dpiScale + 6;
            MaxWidth = workingArea.Width / dpiScale + 6;
        } else
        {
            //BorderThickness = new Thickness(0);
            MaximizeBtn.Content = new PackIcon
            {
                Kind = PackIconKind.WindowMaximize,
                FontSize = 18
            };

            CustomChrome.CornerRadius = new CornerRadius(15);
            WindowBorder.CornerRadius = new CornerRadius(15);
            WindowBorder.BorderThickness = new Thickness(2);
        }
    }

    private void MaximizeBtn_OnClick(object sender, RoutedEventArgs e)
    {
        if (WindowState == WindowState.Maximized)
        {
            SystemCommands.RestoreWindow(this);

            if (RestoreBounds.HasValue)
            {
                Left = RestoreBounds.Value.Left;
                Top = RestoreBounds.Value.Top;
                Width = RestoreBounds.Value.Width;
                Height = RestoreBounds.Value.Height;
            }
        } else
        {
            RestoreBounds = new Rectangle(
                (int)Left,
                (int)Top,
                (int)Width,
                (int)Height);
            SystemCommands.MaximizeWindow(this);
        }
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
}