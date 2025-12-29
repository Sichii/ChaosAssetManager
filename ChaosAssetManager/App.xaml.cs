using System.Windows;
using System.Windows.Controls;

namespace ChaosAssetManager;

/// <summary>
///     Interaction logic for App.xaml
/// </summary>
public sealed partial class App
{
    static App()
    {
        // Speed up tooltip display (default is 400ms which feels slow)
        ToolTipService.InitialShowDelayProperty.OverrideMetadata(
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(200));
    }
}