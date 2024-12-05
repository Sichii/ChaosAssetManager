using DALib.Utility;

namespace ChaosAssetManager.Model;

public sealed class Animation : IDisposable
{
    public int FrameIntervalMs { get; init; }
    public SKImageCollection Frames { get; init; }

    public Animation(SKImageCollection frames, int? frameIntervalMs = 100)
    {
        frameIntervalMs ??= 100;

        Frames = frames;
        FrameIntervalMs = frameIntervalMs.Value;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Frames.Dispose();
        Frames.Clear();
    }
}