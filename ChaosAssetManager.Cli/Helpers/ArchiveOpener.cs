using DALib.Data;

namespace ChaosAssetManager.Cli.Helpers;

/// <summary>
///     Opens a <see cref="DataArchive" />, falling back to the "new format" header layout if the default layout fails to
///     parse. Mirrors the same fallback pattern used throughout the GUI (see ArchiveCache.GetArchive and
///     ArchivesControl.LoadArchive).
/// </summary>
internal static class ArchiveOpener
{
    /// <summary>
    ///     Opens the archive at the given path, trying the default header format first and the "new format" header layout
    ///     second.
    /// </summary>
    /// <param name="path">
    ///     Path to the .dat file.
    /// </param>
    /// <param name="memoryMapped">
    ///     Whether to open the archive memory-mapped. Must be false if the archive will be patched/saved.
    /// </param>
    public static DataArchive Open(string path, bool memoryMapped)
    {
        try
        {
            return DataArchive.FromFile(path, memoryMapped);
        } catch
        {
            //fall back to the "new format" header layout
            return DataArchive.FromFile(path, memoryMapped, true);
        }
    }
}
