using System.IO;
using System.Windows;
using System.Windows.Interop;
using Chaos.Extensions.Common;
using ChaosAssetManager.Controls;
using ChaosAssetManager.Helpers;
using MaterialDesignThemes.Wpf;
using RadioButton = System.Windows.Controls.RadioButton;
using WindowState = System.Windows.WindowState;

namespace ChaosAssetManager;

/// <summary>
///     Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly Dictionary<string, UIElement> _navContentMap = new();
    private Rectangle? RestoreBounds;

    public MainWindow()
    {
        InitializeComponent();

        Width = SystemParameters.PrimaryScreenWidth * 0.625;
        Height = SystemParameters.PrimaryScreenHeight * 0.625;

        // Map navigation items to their corresponding content
        _navContentMap["ArchivesNav"] = ArchivesView;
        _navContentMap["ConvertNav"] = ConvertView;
        _navContentMap["PanelSpritesNav"] = PanelSpritesView;
        _navContentMap["EquipmentImportNav"] = EquipmentImportView;
        _navContentMap["NPCImportNav"] = NPCImportView;
        _navContentMap["EffectEditorNav"] = EffectEditorView;
        _navContentMap["EquipmentEditorNav"] = EquipmentEditorView;
        _navContentMap["NPCEditorNav"] = NPCEditorView;
        _navContentMap["MetaFileEditorNav"] = MetaFileEditorView;
        _navContentMap["PaletteRemapperNav"] = PaletteRemapperView;
        _navContentMap["MapEditorNav"] = MapEditorView;
    }

    private void CloseBtn_OnClick(object sender, RoutedEventArgs e) => SystemCommands.CloseWindow(this);

    private void MainWindow_OnActivated(object? sender, EventArgs e) => UpdateArchivePathLabel();

    private void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
    {
        // Pre-load grid tile for editors
        RenderUtil.Preload();

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
                NavigateTo(ArchivesNav);
                ArchivesView.LoadArchive(args[0]);
            } else if (extension.EqualsI(".map"))
            {
                NavigateTo(MapEditorNav);
                MapEditorView.LoadMap(args[0]);
            } else if (string.IsNullOrEmpty(extension))
            {
                NavigateTo(MetaFileEditorNav);
                MetaFileEditorView.LoadMetaData(args[0]);
            }
        } else if (args.All(arg => Path.GetExtension(arg)
                                       .EqualsI(".map")))
            foreach (var arg in args)
            {
                NavigateTo(MapEditorNav);
                MapEditorView.LoadMap(arg);
            }

        UpdateArchivePathLabel();
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

    private void NavigateTo(RadioButton navButton)
    {
        navButton.IsChecked = true;

        // If navigating to a sub-item, expand its parent
        if ((navButton == EffectEditorNav)
            || (navButton == EquipmentEditorNav)
            || (navButton == NPCEditorNav)
            || (navButton == MetaFileEditorNav)
            || (navButton == MapEditorNav))
            EditorsExpander.IsExpanded = true;
    }

    private void NavItem_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton radioButton)
            return;

        // Hide all content
        foreach (var content in _navContentMap.Values)
            content.Visibility = Visibility.Collapsed;

        // Show selected content
        if (_navContentMap.TryGetValue(radioButton.Name, out var selectedContent))
            selectedContent.Visibility = Visibility.Visible;
    }

    private void SettingsBtn_OnClick(object sender, RoutedEventArgs e)
    {
        var options = new OptionsWindow
        {
            Owner = this
        };

        options.Show();
    }

    private void UpdateArchivePathLabel() => ArchivePathLabel.Text = PathHelper.Instance.ArchivesPath ?? string.Empty;
}