using System.Globalization;
using System.Windows.Data;
using ChaosAssetManager.Definitions;
using ChaosAssetManager.ViewModel;

namespace ChaosAssetManager.Converters;

public sealed class TilesLayerFlagsConverter : IMultiValueConverter
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

        if (editTileType is LayerFlags.LeftForeground or LayerFlags.RightForeground)
            return viewModel.ForegroundTiles;

        return viewModel.BackgroundTiles;
    }

    /// <inheritdoc />
    public object[] ConvertBack(
        object value,
        Type[] targetTypes,
        object parameter,
        CultureInfo culture)
        => throw new NotImplementedException();
}