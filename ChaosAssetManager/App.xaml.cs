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
        //force OpenTK to use the native Windows backend instead of SDL2
        //SDL2 backend can't create GL contexts on WPF windows
        OpenTK.Toolkit.Init(new OpenTK.ToolkitOptions { Backend = OpenTK.PlatformBackend.PreferNative });

        // Speed up tooltip display (default is 400ms which feels slow)
        ToolTipService.InitialShowDelayProperty.OverrideMetadata(
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(200));
    }
}
