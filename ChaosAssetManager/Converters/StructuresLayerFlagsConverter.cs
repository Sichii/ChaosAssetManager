using System.Globalization;
using System.Windows.Data;
using ChaosAssetManager.Definitions;
using ChaosAssetManager.ViewModel;

namespace ChaosAssetManager.Converters;

public sealed class StructuresLayerFlagsConverter : IMultiValueConverter
{
    /// <inheritdoc />
    public object Convert(
        object[] values,
        Type targetType,
        object parameter,
        CultureInfo culture)
    {
        var editTileType = (LayerFlags)values[0];
        var viewModel = (MapEditorViewModel)values[1];

        if (editTileType is LayerFlags.Foreground)
            return viewModel.ForegroundStructures;

        return viewModel.BackgroundStructures;
    }

    /// <inheritdoc />
    public object[] ConvertBack(
        object value,
        Type[] targetTypes,
        object parameter,
        CultureInfo culture)
        => throw new NotImplementedException();
}