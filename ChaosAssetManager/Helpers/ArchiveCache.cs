using System.Collections.Concurrent;
using System.IO;
using DALib.Data;
using DALib.Extensions;

namespace ChaosAssetManager.Helpers;

public sealed class ArchiveCache
{
    private static ConcurrentDictionary<string, DataArchive> Cache { get; } = new(StringComparer.OrdinalIgnoreCase);

    private static string Root => PathHelper.Instance.ArchivesPath!;

    public static DataArchive Cious => GetArchive(Root, "cious");
    public static DataArchive Hades => GetArchive(Root, "hades");
    public static DataArchive Ia => GetArchive(Root, "ia");
    public static DataArchive KhanMad => GetArchive(Root, "khanmad");
    public static DataArchive KhanMeh => GetArchive(Root, "khanmeh");
    public static DataArchive KhanMim => GetArchive(Root, "khanmim");
    public static DataArchive KhanMns => GetArchive(Root, "khanmns");
    public static DataArchive KhanMtz => GetArchive(Root, "khanmtz");
    public static DataArchive KhanPal => GetArchive(Root, "khanpal");
    public static DataArchive KhanWad => GetArchive(Root, "khanwad");
    public static DataArchive KhanWeh => GetArchive(Root, "khanweh");
    public static DataArchive KhanWim => GetArchive(Root, "khanwim");
    public static DataArchive KhanWns => GetArchive(Root, "khanwns");
    public static DataArchive KhanWtz => GetArchive(Root, "khanwtz");
    public static DataArchive Legend => GetArchive(Root, "legend");
    public static DataArchive Misc => GetArchive(Root, "misc");
    public static DataArchive National => GetArchive(Root, "national");
    public static DataArchive Roh => GetArchive(Root, "roh");
    public static DataArchive Seo => GetArchive(Root, "seo");
    public static DataArchive Setoa => GetArchive(Root, "setoa");

    public static void Clear()
    {
        foreach (var value in Cache.Values)
            value.Dispose();

        Cache.Clear();
    }

    public static DataArchive GetArchive(string root, string archiveName)
    {
        archiveName = archiveName.WithExtension(".dat");

        return Cache.GetOrAdd(
            archiveName,
            static (fileName, rootDir) =>
            {
                try
                {
                    return DataArchive.FromFile(Path.Combine(rootDir, fileName), false);
                } catch
                {
                    return DataArchive.FromFile(Path.Combine(rootDir, fileName), false, true);
                }
            },
            root);
    }
}