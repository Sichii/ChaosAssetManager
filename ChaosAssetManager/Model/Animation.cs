using SkiaSharp;

namespace ChaosAssetManager.Model;

public sealed class Animation : IDisposable
{
    public int FrameIntervalMs { get; init; }
    public List<SKImage> Frames { get; init; }

    public Animation(IEnumerable<SKImage> frames, int? frameIntervalMs = 100)
    {
        frameIntervalMs ??= 100;

        Frames = frames.ToList();
        FrameIntervalMs = frameIntervalMs.Value;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var frame in Frames.ToList())
            frame.Dispose();

        Frames.Clear();
    }
}