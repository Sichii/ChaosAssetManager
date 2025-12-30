using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Chaos.Common.Utilities;

namespace ChaosAssetManager.Helpers;

public sealed class PathHelper
{
    [JsonIgnore]
    private static readonly string PATH = $"{nameof(PathHelper)}.json";

    [JsonIgnore]
    public static PathHelper Instance { get; }

    static PathHelper()
    {
        PATH = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, PATH);

        if (!File.Exists(PATH))
            Instance = new PathHelper();
        else
            try
            {
                Instance = JsonSerializerEx.Deserialize<PathHelper>(PATH, JsonSerializerOptions.Default)!;
            } catch
            {
                Instance = new PathHelper();
            }
    }

    public static bool ArchivePathIsValid(string? str)
    {
        if (string.IsNullOrEmpty(str))
            return false;

        var iaPath = Path.Combine(str!, "ia.dat");

        if (!File.Exists(iaPath))
            return false;

        var seoPath = Path.Combine(str!, "seo.dat");

        if (!File.Exists(seoPath))
            return false;

        return true;
    }

    public bool ArchivePathIsValid() => ArchivePathIsValid(ArchivesPath);

    public void Save()
        => JsonSerializerEx.Serialize(
            PATH,
            Instance,
            JsonSerializerOptions.Default,
            false);

    #region From
    public string? ArchiveLoadFromPath { get; set; }
    public string? ArchiveCompileFromPath { get; set; }
    public string? PatchFromPath { get; set; }
    public string? ConvertImageFromPath { get; set; }
    public string? ConvertPalFromPath { get; set; }
    public string? EditorImageFromPath { get; set; }
    public string? EditorPalFromPath { get; set; }
    public string? MetaFileEditorFromPath { get; set; }
    public string? PaletteRemapperImageFromPath { get; set; }
    public string? PaletteRemapperPalFromPath { get; set; }
    public string? ArchiveExtractFromPath { get; set; }
    public string? ArchivesPath { get; set; }
    public string? PanelSpritesArchivePath { get; set; }
    public string? PanelSpritesInputPath { get; set; }
    public string? EquipmentEditorFromPath { get; set; }
    public string? EquipmentEditorPalFromPath { get; set; }
    public string? EquipmentEditorToPath { get; set; }
    public string? EquipmentImportFromPath { get; set; }
    public string? NPCImportFromPath { get; set; }
    #endregion

    #region To
    public string? ArchiveCompileToToPath { get; set; }
    public string? ArchiveCompileToPath { get; set; }
    public string? ArchiveExtractToPath { get; set; }
    public string? ConvertImageToPath { get; set; }
    public string? EditorImageToPath { get; set; }
    public string? MetaFileEditorToPath { get; set; }
    public string? ArchiveExtractToToPath { get; set; }
    public string? PaletteRemapperImageToPath { get; set; }
    public string? PaletteRemapperPalToPath { get; set; }
    public string? ArchiveExtractSelectionToPath { get; set; }
    #endregion
}