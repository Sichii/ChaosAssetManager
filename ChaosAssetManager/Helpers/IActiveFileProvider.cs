namespace ChaosAssetManager.Helpers;

/// <summary>
///     implemented by nav-hosted controls that have a concept of a "currently loaded" file or
///     archive, so MainWindow can reflect it in the title bar
/// </summary>
public interface IActiveFileProvider
{
    /// <summary>
    ///     the display name of the currently active file/archive for this screen, or null if none
    /// </summary>
    string? ActiveFileName { get; }

    /// <summary>
    ///     raised whenever ActiveFileName changes
    /// </summary>
    event Action? ActiveFileChanged;
}
