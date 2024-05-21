using System.Windows;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace ChaosAssetManager.Extensions;

public static class ControlExtensions
{
    public static void ShowMessage(this Control control, string message)
        => MessageBox.Show(
            Application.Current.MainWindow!,
            message,
            "Chaos Asset Manager",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
}