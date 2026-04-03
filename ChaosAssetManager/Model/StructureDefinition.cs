using System.Text.Json.Serialization;

namespace ChaosAssetManager.Model;

/// <summary>
/// Represents a structure definition that can be serialized to/from JSON
/// </summary>
public sealed class StructureDefinition
{
    public required string Id { get; set; }
    public required int Width { get; set; }
    public required int Height { get; set; }
    public short[]? BackgroundTiles { get; set; }
    public short[]? LeftForegroundTiles { get; set; }
    public short[]? RightForegroundTiles { get; set; }

    /// <summary>
    /// Determines if this is a foreground structure based on tile content
    /// </summary>
    [JsonIgnore]
    public bool HasForegroundTiles =>
        (LeftForegroundTiles?.Any(t => t != 0) ?? false)
        || (RightForegroundTiles?.Any(t => t != 0) ?? false);
}
