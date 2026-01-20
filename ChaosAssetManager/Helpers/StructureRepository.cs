using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using ChaosAssetManager.Definitions;
using ChaosAssetManager.Model;
using ChaosAssetManager.ViewModel;
using Rectangle = Chaos.Geometry.Rectangle;

namespace ChaosAssetManager.Helpers;

public sealed class StructureRepository
{
    [JsonIgnore]
    private static readonly string PATH = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "structures.json");

    [JsonIgnore]
    public static StructureRepository Instance { get; }

    public List<StructureDefinition> Structures { get; set; } = [];

    static StructureRepository()
    {
        if (!File.Exists(PATH))
        {
            Instance = new StructureRepository();
            Instance.MigrateFromConstants();
            Instance.Save();
        }
        else
            try
            {
                var json = File.ReadAllText(PATH);
                Instance = JsonSerializer.Deserialize<StructureRepository>(json) ?? new StructureRepository();
            } catch
            {
                Instance = new StructureRepository();
            }
    }

    public void Save()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        var json = JsonSerializer.Serialize(this, options);
        File.WriteAllText(PATH, json);
    }

    public void Add(StructureDefinition definition)
    {
        Structures.Add(definition);
        Save();
    }

    public void Update(string originalId, StructureDefinition definition)
    {
        var existing = Structures.FirstOrDefault(s => s.Id == originalId);

        if (existing is not null)
        {
            Structures.Remove(existing);
            Structures.Add(definition);
            Save();
        }
    }

    public void Delete(string id)
    {
        var existing = Structures.FirstOrDefault(s => s.Id == id);

        if (existing is not null)
        {
            Structures.Remove(existing);
            Save();
        }
    }

    public StructureDefinition? GetById(string id) => Structures.FirstOrDefault(s => s.Id == id);

    public bool IdExists(string id) => Structures.Any(s => s.Id == id);

    /// <summary>
    /// Converts a StructureDefinition to a StructureViewModel for display/editing
    /// </summary>
    public static StructureViewModel ToViewModel(StructureDefinition definition)
    {
        var width = definition.Width;
        var height = definition.Height;

        var viewModel = new StructureViewModel
        {
            Id = definition.Id,
            Bounds = new Rectangle(0, 0, width, height)
        };

        //add background tiles
        if (definition.BackgroundTiles is not null)
            for (var i = 0; i < definition.BackgroundTiles.Length; i++)
            {
                var tile = new TileViewModel
                {
                    TileId = definition.BackgroundTiles[i],
                    LayerFlags = LayerFlags.Background
                };

                viewModel.RawBackgroundTiles.Add(tile);
            }

        //add left foreground tiles
        if (definition.LeftForegroundTiles is not null)
            for (var i = 0; i < definition.LeftForegroundTiles.Length; i++)
            {
                var tile = new TileViewModel
                {
                    TileId = definition.LeftForegroundTiles[i],
                    LayerFlags = LayerFlags.LeftForeground
                };

                viewModel.RawLeftForegroundTiles.Add(tile);
            }

        //add right foreground tiles
        if (definition.RightForegroundTiles is not null)
            for (var i = 0; i < definition.RightForegroundTiles.Length; i++)
            {
                var tile = new TileViewModel
                {
                    TileId = definition.RightForegroundTiles[i],
                    LayerFlags = LayerFlags.RightForeground
                };

                viewModel.RawRightForegroundTiles.Add(tile);
            }

        return viewModel;
    }

    /// <summary>
    /// Converts a StructureViewModel back to a StructureDefinition for saving
    /// </summary>
    public static StructureDefinition FromViewModel(StructureViewModel viewModel)
    {
        var definition = new StructureDefinition
        {
            Id = viewModel.Id ?? "Unnamed Structure",
            Width = viewModel.Bounds.Width,
            Height = viewModel.Bounds.Height
        };

        if (viewModel.RawBackgroundTiles.Count > 0)
            definition.BackgroundTiles = viewModel.RawBackgroundTiles.Select(t => t.TileId).ToArray();

        if (viewModel.RawLeftForegroundTiles.Count > 0)
            definition.LeftForegroundTiles = viewModel.RawLeftForegroundTiles.Select(t => t.TileId).ToArray();

        if (viewModel.RawRightForegroundTiles.Count > 0)
            definition.RightForegroundTiles = viewModel.RawRightForegroundTiles.Select(t => t.TileId).ToArray();

        return definition;
    }

    /// <summary>
    /// Migrates hardcoded structures from CONSTANTS to JSON
    /// </summary>
    private void MigrateFromConstants()
    {
        var index = 1;

        //migrate foreground structures
        foreach (var structure in CONSTANTS.FOREGROUND_STRUCTURES)
        {
            var minTileId = GetMinTileId(structure);

            var definition = new StructureDefinition
            {
                Id = $"FG_{index++}_{minTileId}",
                Width = structure.Bounds.Width,
                Height = structure.Bounds.Height
            };

            if (structure.RawBackgroundTiles.Count > 0)
                definition.BackgroundTiles = structure.RawBackgroundTiles.Select(t => t.TileId).ToArray();

            if (structure.RawLeftForegroundTiles.Count > 0)
                definition.LeftForegroundTiles = structure.RawLeftForegroundTiles.Select(t => t.TileId).ToArray();

            if (structure.RawRightForegroundTiles.Count > 0)
                definition.RightForegroundTiles = structure.RawRightForegroundTiles.Select(t => t.TileId).ToArray();

            Structures.Add(definition);
        }

        //migrate background structures
        index = 1;

        foreach (var structure in CONSTANTS.BACKGROUND_STRUCTURES)
        {
            var minTileId = GetMinTileId(structure);

            var definition = new StructureDefinition
            {
                Id = $"BG_{index++}_{minTileId}",
                Width = structure.Bounds.Width,
                Height = structure.Bounds.Height
            };

            if (structure.RawBackgroundTiles.Count > 0)
                definition.BackgroundTiles = structure.RawBackgroundTiles.Select(t => t.TileId).ToArray();

            if (structure.RawLeftForegroundTiles.Count > 0)
                definition.LeftForegroundTiles = structure.RawLeftForegroundTiles.Select(t => t.TileId).ToArray();

            if (structure.RawRightForegroundTiles.Count > 0)
                definition.RightForegroundTiles = structure.RawRightForegroundTiles.Select(t => t.TileId).ToArray();

            Structures.Add(definition);
        }
    }

    private static int GetMinTileId(StructureViewModel structure)
    {
        var allTileIds = structure.RawBackgroundTiles.Select(t => t.TileId)
                                  .Concat(structure.RawLeftForegroundTiles.Select(t => t.TileId))
                                  .Concat(structure.RawRightForegroundTiles.Select(t => t.TileId))
                                  .Where(id => id > 0);

        return allTileIds.Any() ? allTileIds.Min() : 0;
    }
}
