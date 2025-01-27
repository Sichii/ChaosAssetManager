using System.Globalization;
using System.Windows.Data;
using ChaosAssetManager.Model;
using Rectangle = Chaos.Geometry.Rectangle;

namespace ChaosAssetManager.Converters;

public class RectangleMapBoundsConverter : IValueConverter
{
    /// <inheritdoc />
    public object? Convert(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture)
    {
        if (value is not Rectangle rect)
            return null;

        return new MapBounds
        {
            Width = rect.Width,
            Height = rect.Height
        };
    }

    /// <inheritdoc />
    public object? ConvertBack(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture)
    {
        if (value is not MapBounds bounds)
            return null;

        return new Rectangle(
            0,
            0,
            bounds.Width,
            bounds.Height);
    }
}