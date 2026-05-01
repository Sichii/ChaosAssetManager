using DALib.Definitions;
using DALib.Drawing;
using SkiaSharp;

namespace ChaosAssetManager.Helpers;

public static class PaletteLookupExtensions
{
    /// <summary>
    ///     Resolves a palette for the given id and reports whether the underlying palette number is >= 1000, which marks it
    ///     as luminance-blended. Luminance-blended palettes are returned with per-color alpha set from each color's max
    ///     channel; the resulting EpfFrame must be rendered with <see cref="SKAlphaType.Unpremul" /> so SkiaSharp does not
    ///     treat the unscaled RGB as premultiplied
    /// </summary>
    /// <returns>
    ///     A tuple of the resolved palette and the SKAlphaType that should be passed to
    ///     <see cref="Graphics.RenderImage(EpfFrame, Palette, SKAlphaType)" />
    /// </returns>
    public static (Palette Palette, SKAlphaType AlphaType) GetPaletteAndAlphaType(
        this PaletteLookup lookup,
        int id,
        KhanPalOverrideType khanPalOverrideType = KhanPalOverrideType.None)
    {
        var paletteNumber = lookup.Table.GetPaletteNumber(id, khanPalOverrideType);
        var palette = lookup.GetPaletteForId(id, khanPalOverrideType);
        var alphaType = paletteNumber >= 1000 ? SKAlphaType.Unpremul : SKAlphaType.Premul;

        return (palette, alphaType);
    }
}
