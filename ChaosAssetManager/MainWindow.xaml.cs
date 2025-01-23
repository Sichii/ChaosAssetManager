using System.Windows;

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
}