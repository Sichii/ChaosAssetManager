using System.Windows;
using Application = System.Windows.Application;

namespace ChaosAssetManager.Helpers;

public static class DpiHelper
{
    public static double GetDpiScaleFactor()
    {
        var presentationSource = PresentationSource.FromVisual(Application.Current.MainWindow!)!;

        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (presentationSource is not null)
        {
            var matrix = presentationSource.CompositionTarget!.TransformToDevice;

            return matrix.M11; //horizontal/vertical dpi scaling are generally the same
        }

        return 1.0;
    }
}