using Microsoft.Extensions.ObjectPool;
using SkiaSharp.Views.WPF;

namespace ChaosAssetManager.Helpers;

public sealed class SkGlElementPool : DefaultObjectPool<SKGLElement>
{
    public static SkGlElementPool Instance { get; } = new(ushort.MaxValue);

    /// <inheritdoc />
    public SkGlElementPool()
        : base(new DefaultPooledObjectPolicy<SKGLElement>()) { }

    /// <inheritdoc />
    public SkGlElementPool(int maximumRetained)
        : base(new DefaultPooledObjectPolicy<SKGLElement>(), maximumRetained) { }

    /// <inheritdoc />
    public override void Return(SKGLElement obj)
    {
        base.Return(obj);
        obj.GRContext?.PurgeResources();
    }
}