namespace ChaosAssetManager.Model;

public record MapBounds
{
    public required int Height { get; set; }
    public required int Width { get; set; }

    /// <inheritdoc />
    public override string ToString() => $"Width: {Width}, Height: {Height}";
}