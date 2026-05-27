using SkiaSharp;

namespace ChaosAssetManager.Model;

/// <summary>
///     Describes how a spliced source image's tiles line up in isometric space. Produced by the tile splicer
///     and consumed both by the importer (via <see cref="OrderedImages" />) and by the layout-map renderer.
/// </summary>
public sealed class SplicedTileLayout(IReadOnlyList<PlacedTile> placedTiles)
{
    public IReadOnlyList<PlacedTile> PlacedTiles { get; } = placedTiles;

    //non-empty tiles in placement order — the exact flat list the importer consumes
    public IReadOnlyList<SKImage> OrderedImages
        => PlacedTiles.Where(tile => !tile.IsEmpty)
                      .OrderBy(tile => tile.Order)
                      .Select(tile => tile.Image!)
                      .ToList();
}

/// <summary>
///     A single cell of the spliced grid. Kept cells carry the diamond-masked <see cref="Image" /> and a 0-based
///     <see cref="Order" />; empty cells (fully transparent or black in the source) carry no image and exist only
///     so the map can show them as gaps.
/// </summary>
public sealed class PlacedTile(int tileLeft, int tileTop, int row, int col, bool isEmpty, SKImage? image, int order)
{
    public int TileLeft { get; } = tileLeft;
    public int TileTop { get; } = tileTop;
    public int Row { get; } = row;
    public int Col { get; } = col;
    public bool IsEmpty { get; } = isEmpty;
    public SKImage? Image { get; } = image;
    public int Order { get; } = order;
}
