using System.Collections.Concurrent;
using System.IO;
using DALib.Data;
using DALib.Extensions;

namespace ChaosAssetManager.Helpers;

public sealed class ArchiveCache
{
    private static ConcurrentDictionary<string, DataArchive> Cache { get; } = new(StringComparer.OrdinalIgnoreCase);

    public static DataArchive GetArchive(string root, string archiveName)
    {
        archiveName = archiveName.WithExtension(".dat");

        return Cache.GetOrAdd(archiveName, static (fileName, rootDir) => DataArchive.FromFile(Path.Combine(rootDir, fileName)), root);
    }
    
    public static void Clear() => Cache.Clear();
}