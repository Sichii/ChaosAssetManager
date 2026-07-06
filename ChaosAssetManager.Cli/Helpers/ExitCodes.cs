namespace ChaosAssetManager.Cli.Helpers;

/// <summary>
///     Process exit codes returned by the CLI. Usage/parse errors (missing args, nonexistent files) are handled by
///     System.CommandLine itself and also result in code 1.
/// </summary>
internal static class ExitCodes
{
    public const int Success = 0;
    public const int UsageError = 1;
    public const int ArchiveError = 2;
    public const int EntryNotFound = 3;
}
