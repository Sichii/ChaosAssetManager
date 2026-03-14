using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using ChaosAssetManager.Model;

namespace ChaosAssetManager.Helpers;

public sealed class LightPrefabRepository
{
    private static readonly string RESOURCES_DIR = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources");

    [JsonIgnore]
    private static readonly string PATH = Path.Combine(RESOURCES_DIR, "lightprefabs.json");

    [JsonIgnore]
    public static LightPrefabRepository Instance { get; }

    [JsonIgnore]
    private int NextIndex;

    public List<LightPrefab> Prefabs { get; set; } = [];

    static LightPrefabRepository()
    {
        if (!File.Exists(PATH))
        {
            Instance = new LightPrefabRepository();
            Instance.Save();
        }
        else
            try
            {
                var json = File.ReadAllText(PATH);
                Instance = JsonSerializer.Deserialize<LightPrefabRepository>(json) ?? new LightPrefabRepository();
            } catch
            {
                Instance = new LightPrefabRepository();
            }

        Instance.InitializeIndexes();
        Instance.EnsureBuiltInPrefabs();
    }

    private void InitializeIndexes()
    {
        var maxIndex = 0;

        foreach (var prefab in Prefabs)
        {
            if (int.TryParse(prefab.Id.Split('_').LastOrDefault(), out var idx))
                maxIndex = Math.Max(maxIndex, idx);
        }

        NextIndex = maxIndex + 1;
    }

    /// <summary>
    ///     Ensures the two canonical game light patterns are always available as built-in prefabs.
    ///     These were extracted from the game's .hea files:
    ///     - Lantern: 64x62, max 31, small torch/lantern light (74 instances across all maps)
    ///     - Area Light: 111x118, max 32, large area illumination (448 instances across all maps)
    /// </summary>
    private void EnsureBuiltInPrefabs()
    {
        var dirty = false;

        if (Prefabs.All(p => p.Id != "BuiltIn_Lantern"))
        {
            var lanternData = TryLoadBuiltInData("prefab_lantern.bin");

            if (lanternData is not null)
            {
                Prefabs.Insert(
                    0,
                    new LightPrefab
                    {
                        Id = "BuiltIn_Lantern",
                        Width = 64,
                        Height = 62,
                        Data = lanternData
                    });

                dirty = true;
            }
        }

        if (Prefabs.All(p => p.Id != "BuiltIn_AreaLight"))
        {
            var areaLightData = TryLoadBuiltInData("prefab_arealight.bin");

            if (areaLightData is not null)
            {
                Prefabs.Insert(
                    Prefabs.Count > 0 && Prefabs[0].Id == "BuiltIn_Lantern" ? 1 : 0,
                    new LightPrefab
                    {
                        Id = "BuiltIn_AreaLight",
                        Width = 111,
                        Height = 118,
                        Data = areaLightData
                    });

                dirty = true;
            }
        }

        if (dirty)
            Save();
    }

    private static byte[]? TryLoadBuiltInData(string fileName)
    {
        var path = Path.Combine(RESOURCES_DIR, fileName);

        if (!File.Exists(path))
            return null;

        try
        {
            return File.ReadAllBytes(path);
        }
        catch
        {
            return null;
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

    public void Add(LightPrefab prefab)
    {
        Prefabs.Add(prefab);
        Save();
    }

    public void Delete(string id)
    {
        //don't allow deleting built-in prefabs
        if (id.StartsWith("BuiltIn_"))
            return;

        var existing = Prefabs.FirstOrDefault(p => p.Id == id);

        if (existing is not null)
        {
            Prefabs.Remove(existing);
            Save();
        }
    }

    public string GenerateId(int width, int height) => $"LP_{width}x{height}_{NextIndex++}";

    /// <summary>
    ///     Extracts unique light patterns from an in-memory light grid using flood-fill
    /// </summary>
    public List<LightPrefab> ExtractFromGrid(byte[,] grid)
    {
        var gridH = grid.GetLength(0);
        var gridW = grid.GetLength(1);

        //flood-fill to find connected light blobs
        var visited = new bool[gridH, gridW];
        var blobs = new List<(int minX, int minY, int maxX, int maxY, List<(int x, int y)> pixels)>();

        for (var y = 0; y < gridH; y++)
            for (var x = 0; x < gridW; x++)
            {
                if (visited[y, x] || grid[y, x] == 0)
                    continue;

                //flood-fill this blob
                var pixels = new List<(int x, int y)>();
                var queue = new Queue<(int x, int y)>();
                queue.Enqueue((x, y));
                visited[y, x] = true;

                var minX = x;
                var minY = y;
                var maxX = x;
                var maxY = y;

                while (queue.Count > 0)
                {
                    var (cx, cy) = queue.Dequeue();
                    pixels.Add((cx, cy));

                    minX = Math.Min(minX, cx);
                    minY = Math.Min(minY, cy);
                    maxX = Math.Max(maxX, cx);
                    maxY = Math.Max(maxY, cy);

                    //4-connected neighbors
                    ReadOnlySpan<(int dx, int dy)> dirs = [(-1, 0), (1, 0), (0, -1), (0, 1)];

                    foreach (var (dx, dy) in dirs)
                    {
                        var nx = cx + dx;
                        var ny = cy + dy;

                        if (nx < 0 || nx >= gridW || ny < 0 || ny >= gridH)
                            continue;

                        if (visited[ny, nx] || grid[ny, nx] == 0)
                            continue;

                        visited[ny, nx] = true;
                        queue.Enqueue((nx, ny));
                    }
                }

                blobs.Add((minX, minY, maxX, maxY, pixels));
            }

        //extract each blob as a prefab, deduplicating by dimensions and content
        //also skip patterns that match existing prefabs (including built-ins)
        var results = new List<LightPrefab>();
        var seen = new HashSet<string>();

        //pre-populate seen set with existing prefabs so we don't create duplicates
        foreach (var existing in Prefabs)
        {
            var existingMax = existing.Data.Length > 0 ? existing.Data.Max() : 0;
            seen.Add($"{existing.Width}x{existing.Height}_{existingMax}");
        }

        foreach (var (minX, minY, maxX, maxY, pixels) in blobs)
        {
            var w = maxX - minX + 1;
            var h = maxY - minY + 1;

            //skip tiny blobs (noise)
            if (w < 10 || h < 10)
                continue;

            var data = new byte[h * w];

            foreach (var (px, py) in pixels)
                data[(py - minY) * w + (px - minX)] = grid[py, px];

            //deduplicate by dimensions + max value
            var maxVal = data.Max();
            var key = $"{w}x{h}_{maxVal}";

            if (!seen.Add(key))
                continue;

            results.Add(
                new LightPrefab
                {
                    Id = GenerateId(w, h),
                    Width = w,
                    Height = h,
                    Data = data
                });
        }

        return results;
    }
}
