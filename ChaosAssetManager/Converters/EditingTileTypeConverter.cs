using System.Globalization;
using System.Windows.Data;
using ChaosAssetManager.Definitions;
using ChaosAssetManager.ViewModel;

namespace ChaosAssetManager.Converters;

public sealed class EditingTileTypeConverter : IMultiValueConverter
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

        if (editTileType == LayerFlags.Background)
            return viewModel.BackgroundTiles;

        return viewModel.ForegroundTiles;
    }

    /// <inheritdoc />
    public object[] ConvertBack(
        object value,
        Type[] targetTypes,
        object parameter,
        CultureInfo culture)
        => throw new NotImplementedException();
}