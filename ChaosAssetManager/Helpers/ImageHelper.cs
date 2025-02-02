using ChaosAssetManager.ViewModel;
using DALIB_CONSTANTS = DALib.Definitions.CONSTANTS;

namespace ChaosAssetManager.Helpers;

public static class ImageHelper
{
    /// <summary>
    ///     Calculates the width and height (in pixels) of the full rendered map based on background and foreground tiles,
    ///     using isometric draw logic.
    /// </summary>
    /// <param name="backgroundTiles">
    ///     2D array of background tile view models.
    /// </param>
    /// <param name="leftForegroundTiles">
    ///     2D array of left-foreground tile view models.
    /// </param>
    /// <param name="rightForegroundTiles">
    ///     2D array of right-foreground tile view models.
    /// </param>
    /// <returns>
    ///     A tuple (width, height) representing the bounding-box dimensions.
    /// </returns>
    public static (int Width, int Height) CalculateRenderedImageSize(
        ListSegment2D<TileViewModel> backgroundTiles,
        ListSegment2D<TileViewModel> leftForegroundTiles,
        ListSegment2D<TileViewModel> rightForegroundTiles)
    {
        var tilesWide = Math.Max(backgroundTiles.Width, Math.Max(leftForegroundTiles.Width, rightForegroundTiles.Width));
        var tilesHigh = Math.Max(backgroundTiles.Height, Math.Max(leftForegroundTiles.Height, rightForegroundTiles.Height));

        var minY = 0;

        for (var y = 0; y < tilesHigh; y++)
        {
            for (var x = 0; x < tilesWide; x++)
            {
                if ((x < backgroundTiles.Width) && (y < backgroundTiles.Height))
                {
                    var animation = backgroundTiles[x, y].Animation;

                    if (animation is not null)
                    {
                        var tallestFrameHeight = animation.Frames.Max(frame => frame.Height);

                        var bottom = (x + y + 2) * DALIB_CONSTANTS.HALF_TILE_HEIGHT;
                        minY = Math.Min(minY, bottom - tallestFrameHeight);
                    }
                }

                if ((x < leftForegroundTiles.Width) && (y < leftForegroundTiles.Height))
                {
                    var animation = leftForegroundTiles[x, y].Animation;

                    if (animation is not null)
                    {
                        var tallestFrameHeight = animation.Frames.Max(frame => frame.Height);

                        var bottom = (x + y + 2) * DALIB_CONSTANTS.HALF_TILE_HEIGHT;
                        minY = Math.Min(minY, bottom - tallestFrameHeight);
                    }
                }

                if ((x < rightForegroundTiles.Width) && (y < rightForegroundTiles.Height))
                {
                    var animation = rightForegroundTiles[x, y].Animation;

                    if (animation is not null)
                    {
                        var tallestFrameHeight = animation.Frames.Max(frame => frame.Height);

                        var bottom = (x + y + 2) * DALIB_CONSTANTS.HALF_TILE_HEIGHT;
                        minY = Math.Min(minY, bottom - tallestFrameHeight);
                    }
                }
            }
        }

        var maxY = (tilesHigh + tilesWide) * DALIB_CONSTANTS.HALF_TILE_HEIGHT;
        var finalHeight = Math.Abs(minY - maxY);
        var finalWidth = (tilesHigh + tilesWide) * DALIB_CONSTANTS.HALF_TILE_WIDTH;

        return (finalWidth, finalHeight);
    }
}