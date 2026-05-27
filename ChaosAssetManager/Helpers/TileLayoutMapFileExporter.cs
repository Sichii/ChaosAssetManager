using ChaosAssetManager.Model;
using DALib.Data;

namespace ChaosAssetManager.Helpers;

/// <summary>
///     Builds a square Dark Ages .map from a spliced layout. Background cells reference the imported tiles
///     (tileset index + 1); every other cell is void (0). Padding to a square makes the Map Editor's
///     dimension inference resolve to the correct bounds.
/// </summary>
public static class TileLayoutMapFileExporter
{
    public static (MapFile Map, int Size) Build(SplicedTileLayout layout, int startingIndex)
    {
        var keptTiles = layout.PlacedTiles
                              .Where(tile => !tile.IsEmpty)
                              .ToList();

        if (keptTiles.Count == 0)
        {
            //defensive; the importer only calls this for a non-empty spliced set
            var empty = new MapFile(1, 1);
            empty.Tiles[0, 0] = new MapTile();

            return (empty, 1);
        }

        //convert each kept tile's staggered (row, col) to map (x, y) via the inverse iso projection
        var coords = new List<(int x, int y, int order)>(keptTiles.Count);

        foreach (var tile in keptTiles)
        {
            var s = tile.Row;
            var d = 2 * tile.Col + (((tile.Row % 2) == 0) ? 1 : 0) + 1;

            coords.Add(((s + d) / 2, (s - d) / 2, tile.Order));
        }

        var minX = coords.Min(coord => coord.x);
        var minY = coords.Min(coord => coord.y);
        var maxX = coords.Max(coord => coord.x);
        var maxY = coords.Max(coord => coord.y);

        var width = maxX - minX + 1;
        var height = maxY - minY + 1;
        var size = Math.Max(width, height);

        var map = new MapFile(size, size);

        //fill every cell with a void tile first (MapFile leaves the Tiles array null-initialized)
        for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
                map.Tiles[x, y] = new MapTile();

        //place each kept tile; background value = tileset index + 1
        foreach ((var x, var y, var order) in coords)
            map.Tiles[x - minX, y - minY] = new MapTile
            {
                Background = (short)(startingIndex + order + 1)
            };

        return (map, size);
    }
}
