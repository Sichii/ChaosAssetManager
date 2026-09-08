using ChaosAssetManager.Cli.Helpers;
using DALib.Data;

namespace ChaosAssetManager.Cli.Commands;

/// <summary>
///     Implements the actual archive operations invoked by the CLI commands. Each method mirrors the same DALib calls
///     used by the WPF GUI's ArchivesControl.
/// </summary>
internal static class ArchiveCommandHandlers
{
    /// <summary>
    ///     Lists entries in an archive, optionally filtered by extension.
    /// </summary>
    public static int List(FileInfo archive, string? extension)
    {
        try
        {
            using var da = ArchiveOpener.Open(archive.FullName, true);

            //ArchiveCache/DataArchive entries are unordered as stored on disk, sort for stable/readable output
            var entries = (extension is null
                    ? da.AsEnumerable()
                    : da.GetEntries(extension))
                .OrderBy(entry => entry.EntryName, StringComparer.OrdinalIgnoreCase);

            foreach (var entry in entries)
                Console.WriteLine(entry.EntryName);

            return ExitCodes.Success;
        } catch (Exception ex)
        {
            Console.Error.WriteLine($"error: failed to open archive \"{archive.FullName}\" — {ex.Message}");

            return ExitCodes.ArchiveError;
        }
    }

    /// <summary>
    ///     Extracts every entry in an archive to the given output directory.
    /// </summary>
    public static int ExtractAll(FileInfo archive, DirectoryInfo output)
    {
        try
        {
            using var da = ArchiveOpener.Open(archive.FullName, true);

            Directory.CreateDirectory(output.FullName);
            da.ExtractTo(output.FullName);

            Console.WriteLine($"extracted {da.Count} entries to {output.FullName}");

            return ExitCodes.Success;
        } catch (Exception ex)
        {
            Console.Error.WriteLine($"error: failed to extract archive \"{archive.FullName}\" — {ex.Message}");

            return ExitCodes.ArchiveError;
        }
    }

    /// <summary>
    ///     Extracts one or more named entries from an archive to the given output directory.
    /// </summary>
    public static int ExtractByName(FileInfo archive, string[] names, DirectoryInfo output)
    {
        DataArchive da;

        try
        {
            da = ArchiveOpener.Open(archive.FullName, true);
        } catch (Exception ex)
        {
            Console.Error.WriteLine($"error: failed to open archive \"{archive.FullName}\" — {ex.Message}");

            return ExitCodes.ArchiveError;
        }

        using (da)
        {
            Directory.CreateDirectory(output.FullName);

            var extracted = 0;
            var missing = 0;

            foreach (var name in names)
            {
                if (!da.TryGetValue(name, out var entry))
                {
                    Console.Error.WriteLine($"error: entry \"{name}\" not found in archive");
                    missing++;

                    continue;
                }

                try
                {
                    var outPath = Path.Combine(output.FullName, entry.EntryName);
                    using var outStream = File.Create(outPath);
                    using var entrySegment = entry.ToStreamSegment();

                    entrySegment.CopyTo(outStream);

                    extracted++;
                } catch (Exception ex)
                {
                    Console.Error.WriteLine($"error: failed to extract \"{entry.EntryName}\" — {ex.Message}");
                    missing++;
                }
            }

            Console.WriteLine($"extracted {extracted} of {names.Length} requested entries to {output.FullName}");

            return missing > 0 ? ExitCodes.EntryNotFound : ExitCodes.Success;
        }
    }

    /// <summary>
    ///     Patches one or more files into an archive by entry name (matching the source file's name), then saves the
    ///     archive to the output path (defaulting to the input archive path, i.e. in place).
    /// </summary>
    public static int Patch(FileInfo archive, FileInfo[] files, FileInfo? output)
    {
        try
        {
            //must not be memory-mapped, patched/saved archives need to be fully loaded into memory
            using var da = ArchiveOpener.Open(archive.FullName, false);

            foreach (var file in files)
            {
                var entryName = Path.GetFileName(file.FullName);
                using var entry = new PatchEntry(File.OpenRead(file.FullName));

                da.Patch(entryName, entry);
            }

            var savePath = output?.FullName ?? archive.FullName;
            da.Save(savePath);

            Console.WriteLine($"patched {files.Length} entries, saved to {savePath}");

            return ExitCodes.Success;
        } catch (Exception ex)
        {
            Console.Error.WriteLine($"error: failed to patch archive \"{archive.FullName}\" — {ex.Message}");

            return ExitCodes.ArchiveError;
        }
    }
}
