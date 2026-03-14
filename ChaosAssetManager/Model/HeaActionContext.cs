namespace ChaosAssetManager.Model;

/// <summary>
///     Stores a snapshot of a rectangular region of the light grid for undo/redo
/// </summary>
public sealed class HeaActionContext
{
    /// <summary>
    ///     The X position of the affected region in the light grid
    /// </summary>
    public required int X { get; init; }

    /// <summary>
    ///     The Y position of the affected region in the light grid
    /// </summary>
    public required int Y { get; init; }

    /// <summary>
    ///     The width of the affected region
    /// </summary>
    public required int Width { get; init; }

    /// <summary>
    ///     The height of the affected region
    /// </summary>
    public required int Height { get; init; }

    /// <summary>
    ///     The light values before the action, row-major [Height * Width]
    /// </summary>
    public required byte[] Before { get; init; }

    /// <summary>
    ///     The light values after the action, row-major [Height * Width]
    /// </summary>
    public required byte[] After { get; init; }

    /// <summary>
    ///     Captures a rectangular region snapshot from the light grid
    /// </summary>
    public static byte[] CaptureRegion(byte[,] grid, int x, int y, int width, int height)
    {
        var gridH = grid.GetLength(0);
        var gridW = grid.GetLength(1);
        var snapshot = new byte[height * width];

        for (var dy = 0; dy < height; dy++)
        {
            var gy = y + dy;

            if (gy < 0 || gy >= gridH)
                continue;

            for (var dx = 0; dx < width; dx++)
            {
                var gx = x + dx;

                if (gx < 0 || gx >= gridW)
                    continue;

                snapshot[dy * width + dx] = grid[gy, gx];
            }
        }

        return snapshot;
    }

    /// <summary>
    ///     Restores a snapshot into the light grid
    /// </summary>
    public static void RestoreRegion(byte[,] grid, byte[] snapshot, int x, int y, int width, int height)
    {
        var gridH = grid.GetLength(0);
        var gridW = grid.GetLength(1);

        for (var dy = 0; dy < height; dy++)
        {
            var gy = y + dy;

            if (gy < 0 || gy >= gridH)
                continue;

            for (var dx = 0; dx < width; dx++)
            {
                var gx = x + dx;

                if (gx < 0 || gx >= gridW)
                    continue;

                grid[gy, gx] = snapshot[dy * width + dx];
            }
        }
    }
}
