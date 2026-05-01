using DALib.Definitions;
using SkiaSharp;

namespace ChaosAssetManager.Helpers;

public static class EfaBlendingExtensions
{
    /// <summary>
    ///     Maps an EFA blending type to the SkiaSharp blend mode required to draw the rendered EFA frame correctly. Additive
    ///     and SelfAlpha frames are written with full alpha by the renderer and rely on <see cref="SKBlendMode.Plus" /> at
    ///     draw time so dark pixels add nothing. SeparateAlpha and PerChannelAlpha frames carry per-pixel alpha and use
    ///     standard <see cref="SKBlendMode.SrcOver" />
    /// </summary>
    public static SKBlendMode ToSKBlendMode(this EfaBlendingType blendingType)
        => blendingType switch
        {
            EfaBlendingType.Additive          => SKBlendMode.Plus,
            EfaBlendingType.SelfAlpha         => SKBlendMode.Plus,
            EfaBlendingType.SeparateAlpha     => SKBlendMode.SrcOver,
            EfaBlendingType.PerChannelAlpha   => SKBlendMode.SrcOver,
            _                                 => SKBlendMode.SrcOver
        };
}
