namespace ChaosAssetManager.Helpers;

/// <summary>
///     Coordinates a full, restart-equivalent invalidation of the in-memory asset caches after an import,
///     so editor controls reflect freshly imported data without restarting the application.
/// </summary>
public static class CacheManager
{
    /// <summary>
    ///     Raised after <see cref="RefreshAfterImport" /> has cleared every cache. Consumers that preload archive-derived
    ///     state once (instead of rebuilding when shown) subscribe to rebuild themselves from the now-fresh caches.
    /// </summary>
    public static event Action? Refreshed;

    /// <summary>
    ///     Clears every in-memory game archive and the derived render caches, then raises <see cref="Refreshed" />. Each
    ///     layer rebuilds lazily on next access. Call on the UI thread after an import has patched and saved its archive(s).
    /// </summary>
    public static void RefreshAfterImport()
    {
        //in-memory .dat archives - lazily reloaded from disk on next access
        ArchiveCache.Clear();

        //palette/table/shader render caches used for previews
        RenderUtil.Reset();

        //map render caches (tileset, palette lookup, sotp, image cache)
        MapEditorRenderUtil.Clear();

        //notify preload-once consumers (e.g. map editor tile pickers) to rebuild from the fresh caches
        Refreshed?.Invoke();
    }
}
