using System.Globalization;
using System.Windows.Data;
using ChaosAssetManager.Helpers;
using ChaosAssetManager.ViewModel;

namespace ChaosAssetManager.Converters;

public class TileConverter : IValueConverter
{
    /// <inheritdoc />
    public object? Convert(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture)
    {
        var tileViewModel = (TileViewModel)value!;
        var archiveDir = PathHelper.Instance.MapEditorArchivePath;
        
        var archive = ArchiveCache.GetArchive(archiveDir, tileViewModel.TileType == TileType.Background ? "seo" : "ia");

        return 0;
    }

    /// <inheritdoc />
    public object? ConvertBack(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture)
        => throw new NotImplementedException();
}