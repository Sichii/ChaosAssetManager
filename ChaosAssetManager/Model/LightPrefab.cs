namespace ChaosAssetManager.Model;

/// <summary>
///     Represents a reusable light pattern extracted from .hea files
/// </summary>
public sealed class LightPrefab
{
    public required string Id { get; set; }
    public required int Width { get; set; }
    public required int Height { get; set; }

    /// <summary>
    ///     Flat row-major light intensity data [Height * Width]
    /// </summary>
    public required byte[] Data { get; set; }

    /// <summary>
    ///     Converts this prefab to a LightBrush for stamping
    /// </summary>
    public LightBrush ToBrush()
    {
        var intensities = new byte[Height, Width];

        for (var y = 0; y < Height; y++)
            for (var x = 0; x < Width; x++)
                intensities[y, x] = Data[y * Width + x];

        return new LightBrush { Intensities = intensities };
    }
}
