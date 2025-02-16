using System.Windows;
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
}