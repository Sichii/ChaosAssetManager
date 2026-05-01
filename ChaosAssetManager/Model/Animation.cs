using SkiaSharp;

namespace ChaosAssetManager.Model;

public sealed class Animation : IDisposable
{
    public int FrameIntervalMs { get; init; }
    public List<SKImage> Frames { get; init; }

    /// <summary>
    ///     Optional blend mode to use when drawing this animation's frames. When unset, callers should fall back to their
    ///     default (typically SrcOver). Used for EFA additive/self-alpha frames that must be drawn with SKBlendMode.Plus
    /// </summary>
    public SKBlendMode? BlendMode { get; init; }

    public Animation(IEnumerable<SKImage> frames, int? frameIntervalMs = 100)
    {
        frameIntervalMs ??= 100;

        Frames = frames.ToList();
        FrameIntervalMs = frameIntervalMs.Value;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var frame in Frames)
            frame.Dispose();

        Frames.Clear();
    }
}