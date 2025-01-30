using ChaosAssetManager.ViewModel;

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
    /// <param name="halfTileWidth">
    ///     Half of the tile width (your isometric "half width").
    /// </param>
    /// <param name="halfTileHeight">
    ///     Half of the tile height (your isometric "half height").
    /// </param>
    /// <returns>
    ///     A tuple (width, height) representing the bounding-box dimensions.
    /// </returns>
    public static (int Width, int Height) CalculateRenderedImageSize(
        ListSegment2D<TileViewModel> backgroundTiles,
        ListSegment2D<TileViewModel> leftForegroundTiles,
        ListSegment2D<TileViewModel> rightForegroundTiles,
        int halfTileWidth,
        int halfTileHeight)
    {
        // Dimensions in tiles (assuming all three arrays have the same size)
        var tilesWide = Math.Max(backgroundTiles.Width, Math.Max(leftForegroundTiles.Width, rightForegroundTiles.Width));
        var tilesHigh = Math.Max(backgroundTiles.Height, Math.Max(leftForegroundTiles.Height, rightForegroundTiles.Height));

        // Track overall min/max in world/pixel coordinates
        var minX = float.MaxValue;
        var minY = float.MaxValue;
        var maxX = float.MinValue;
        var maxY = float.MinValue;

        //--------------------------------------------------------------------------
        // 1) BACKGROUND TILES
        //    Draw logic:
        //      drawX = bgInitialDrawX + x * halfTileWidth
        //      drawY = bgInitialDrawY + x * halfTileHeight
        //--------------------------------------------------------------------------

        float bgDrawX = (tilesHigh - 1) * halfTileWidth;
        float bgDrawY = 0;

        for (var y = 0; y < tilesHigh; y++)
        {
            for (var x = 0; x < tilesWide; x++)
            {
                if ((x >= backgroundTiles.Width) || (y >= backgroundTiles.Height))
                    continue;

                var tileViewModel = backgroundTiles[x, y];
                var frame = tileViewModel.CurrentFrame;

                if (frame != null)
                {
                    var drawX = bgDrawX + x * halfTileWidth;
                    var drawY = bgDrawY + x * halfTileHeight;

                    var tileRight = drawX + frame.Width;
                    var tileBottom = drawY + frame.Height;

                    // Update bounding box
                    if (drawX < minX)
                        minX = drawX;

                    if (drawY < minY)
                        minY = drawY;

                    if (tileRight > maxX)
                        maxX = tileRight;

                    if (tileBottom > maxY)
                        maxY = tileBottom;
                }
            }

            // Move to next row in isometric space
            bgDrawX -= halfTileWidth;
            bgDrawY += halfTileHeight;
        }

        //--------------------------------------------------------------------------
        // 2) FOREGROUND TILES (LEFT + RIGHT)
        //    Draw logic for left tile:
        //      drawX = fgInitialDrawX + x * halfTileWidth
        //      drawY = fgInitialDrawY + (x + 1) * halfTileHeight - frame.Height + halfTileHeight
        //
        //    Draw logic for right tile:
        //      drawX = fgInitialDrawX + (x + 1) * halfTileWidth
        //      drawY = fgInitialDrawY + (x + 1) * halfTileHeight - frame.Height + halfTileHeight
        //--------------------------------------------------------------------------

        float fgDrawX = (tilesHigh - 1) * halfTileWidth;
        float fgDrawY = 0;

        for (var y = 0; y < tilesHigh; y++)
        {
            for (var x = 0; x < tilesWide; x++)
            {
                if ((x < leftForegroundTiles.Width) && (y < leftForegroundTiles.Height))
                {
                    var leftTile = leftForegroundTiles[x, y];

                    // LEFT FOREGROUND
                    if (leftTile is { CurrentFrame: not null, TileId: >= 13 } && ((leftTile.TileId % 10000) > 1))
                    {
                        var drawX = fgDrawX + x * halfTileWidth;
                        var drawY = fgDrawY + (x + 1) * halfTileHeight - leftTile.CurrentFrame.Height + halfTileHeight;

                        var tileRight = drawX + leftTile.CurrentFrame.Width;
                        var tileBottom = drawY + leftTile.CurrentFrame.Height;

                        if (drawX < minX)
                            minX = drawX;

                        if (drawY < minY)
                            minY = drawY;

                        if (tileRight > maxX)
                            maxX = tileRight;

                        if (tileBottom > maxY)
                            maxY = tileBottom;
                    }
                }

                if ((x < rightForegroundTiles.Width) && (y < rightForegroundTiles.Height))
                {
                    var rightTile = rightForegroundTiles[x, y];

                    // RIGHT FOREGROUND
                    if (rightTile is { CurrentFrame: not null, TileId: >= 13 } && ((rightTile.TileId % 10000) > 1))
                    {
                        var drawX = fgDrawX + (x + 1) * halfTileWidth;
                        var drawY = fgDrawY + (x + 1) * halfTileHeight - rightTile.CurrentFrame.Height + halfTileHeight;

                        var tileRight = drawX + rightTile.CurrentFrame.Width;
                        var tileBottom = drawY + rightTile.CurrentFrame.Height;

                        if (drawX < minX)
                            minX = drawX;

                        if (drawY < minY)
                            minY = drawY;

                        if (tileRight > maxX)
                            maxX = tileRight;

                        if (tileBottom > maxY)
                            maxY = tileBottom;
                    }
                }
            }

            // Move to next row in isometric space
            fgDrawX -= halfTileWidth;
            fgDrawY += halfTileHeight;
        }

        // If nothing was drawn, return (0, 0)
        if ((minX > maxX) || (minY > maxY))
            return (0, 0);

        // Convert bounding box to integer dimensions
        var finalWidth = (int)Math.Ceiling(maxX - minX);
        var finalHeight = (int)Math.Ceiling(maxY - minY);

        return (finalWidth, finalHeight);
    }
}