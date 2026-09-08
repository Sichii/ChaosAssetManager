using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using Chaos.Extensions.Common;
using ChaosAssetManager.Helpers;
using ChaosAssetManager.Model;
using ChaosAssetManager.ViewModel;
using DALib.Abstractions;
using DALib.Data;
using DALib.Definitions;
using DALib.Drawing;
using DALib.Extensions;
using DALib.Utility;
using SkiaSharp;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace ChaosAssetManager.Controls;

public sealed partial class TileImportControl : IActiveFileProvider
{
    public ObservableCollection<TileImportViewModel> TileViewModels { get; } = [];
    private bool IsForeground => ForegroundRadio.IsChecked == true;
    private bool IsSnow => SnowRadio.IsChecked == true;

    private SplicedTileLayout? SplicedLayout;
    private string? SplicedSourcePath;

    /// <inheritdoc />
    public string? ActiveFileName => PathHelper.ArchivePathIsValid(PathHelper.Instance.ArchivesPath) ? (IsForeground ? "ia.dat" : "seo.dat") : null;

    /// <inheritdoc />
    public event Action? ActiveFileChanged;

    public TileImportControl()
    {
        InitializeComponent();

        PathHelper.ArchivesPathChanged += () => TileImportControl_OnLoaded(this, new RoutedEventArgs());
    }

    private void BrowseBtn_OnClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = IsForeground ? "Select PNG Tile Images (28 pixels wide)" : "Select PNG Tile Images (56x27 pixels)",
            Filter = "PNG Images|*.png",
            Multiselect = true
        };

        if (dialog.ShowDialog() != true)
            return;

        //clear previous tiles
        foreach (var vm in TileViewModels)
            vm.Dispose();

        TileViewModels.Clear();

        //manually browsed tiles are not a spliced set - no layout map
        SplicedLayout = null;
        SplicedSourcePath = null;

        var validCount = 0;
        var isForeground = IsForeground;

        foreach (var filePath in dialog.FileNames)
            try
            {
                var image = SKImage.FromEncodedData(filePath);

                if (image is null)
                    continue;

                //validate dimensions based on tile type
                if (isForeground)
                {
                    //foreground: must be 28 pixels wide, any height
                    if (image.Width != CONSTANTS.HPF_TILE_WIDTH)
                    {
                        Snackbar.MessageQueue!.Enqueue(
                            $"Skipped {Path.GetFileName(filePath)}: Width must be 28 pixels (got {image.Width})");
                        image.Dispose();

                        continue;
                    }
                } else
                {
                    //background: must be exactly 56x27
                    if ((image.Width != CONSTANTS.TILE_WIDTH) || (image.Height != CONSTANTS.TILE_HEIGHT))
                    {
                        Snackbar.MessageQueue!.Enqueue(
                            $"Skipped {Path.GetFileName(filePath)}: Must be 56x27 pixels (got {image.Width}x{image.Height})");
                        image.Dispose();

                        continue;
                    }
                }

                TileViewModels.Add(
                    new TileImportViewModel
                    {
                        Image = image,
                        FileName = Path.GetFileName(filePath),
                        ShowFlags = isForeground
                    });
                validCount++;
            } catch (Exception ex)
            {
                Snackbar.MessageQueue!.Enqueue($"Error loading {Path.GetFileName(filePath)}: {ex.Message}");
            }

        UpdateStatus(validCount);
    }

    private static string ImportBackgroundTiles(
        string archivePath,
        bool isSnow,
        List<TileImportData> tileData,
        SplicedTileLayout? layout,
        string? sourcePath)
    {
        var seo = ArchiveCache.Seo;
        var tilesetName = isSnow ? "tileas" : "tilea";

        //load existing tileset
        var existingTileset = Tileset.FromArchive(tilesetName, seo);
        var startingIndex = existingTileset.Count;

        //load existing palette lookup
        var paletteLookup = PaletteLookup.FromArchive("mpt", seo);

        //quantize the entire imported set into a single shared palette
        var images = tileData.Select(td => td.Image)
                             .ToList();

        using var palettized = Tileset.FromImages(images);
        (var newTileset, var newPalette) = palettized;

        //add tiles to existing tileset
        foreach (var tile in newTileset)
            existingTileset.Add(tile);

        //all imported tiles reference the one newly created palette
        var paletteId = paletteLookup.GetNextPaletteId();
        var tileIndex = startingIndex;

        for (var i = 0; i < newTileset.Count; i++)
        {
            //palette table uses +2 offset for tile IDs (based on MapEditorRenderUtil)
            paletteLookup.Table.Add(tileIndex + 2, paletteId);
            tileIndex++;
        }

        paletteLookup.Palettes[paletteId] = newPalette;
        seo.Patch($"mpt{paletteId:D4}.pal", newPalette);

        //patch updated tileset
        seo.Patch($"{tilesetName}.bmp", existingTileset);

        //patch updated palette table
        seo.Patch("mptpal.tbl", paletteLookup.Table);

        //save SEO archive
        seo.Save(Path.Combine(archivePath, "seo.dat"));

        var resultMessage =
            $"Imported {tileData.Count} background tiles to {tilesetName} (starting at index {startingIndex}, 1 shared palette)";

        //if these tiles came from the splicer, also write a .map reconstructing the scene
        if (layout is not null)
            resultMessage += WriteLayoutMapFile(layout, sourcePath, startingIndex);

        return resultMessage;
    }

    private static string WriteLayoutMapFile(SplicedTileLayout layout, string? sourcePath, int startingIndex)
    {
        (var map, var size) = TileLayoutMapFileExporter.Build(layout, startingIndex);

        var fileName = string.IsNullOrEmpty(sourcePath)
            ? "tile_layout.map"
            : $"{Path.GetFileNameWithoutExtension(sourcePath)}.map";

        var sourceDir = string.IsNullOrEmpty(sourcePath) ? null : Path.GetDirectoryName(sourcePath);

        //prefer writing next to the source image; fall back to temp if that isn't writable
        string? finalPath = null;

        if (!string.IsNullOrEmpty(sourceDir))
        {
            var candidate = Path.Combine(sourceDir, fileName);

            if (TryWriteMap(map, candidate))
                finalPath = candidate;
        }

        if (finalPath == null)
        {
            var tempCandidate = Path.Combine(Path.GetTempPath(), fileName);

            if (TryWriteMap(map, tempCandidate))
                finalPath = tempCandidate;
        }

        if (finalPath == null)
            return " (layout map write failed)";

        return $" Layout map: {finalPath} ({size}x{size}).";
    }

    private static bool TryWriteMap(MapFile map, string path)
    {
        try
        {
            map.Save(path);

            return true;
        } catch
        {
            //unwritable location (permissions, path too long) - caller falls back to temp
            return false;
        }
    }

    private async void ImportBtn_OnClick(object sender, RoutedEventArgs e)
    {
        var archivePath = PathHelper.Instance.ArchivesPath;

        if (string.IsNullOrEmpty(archivePath) || !PathHelper.ArchivePathIsValid(archivePath))
        {
            Snackbar.MessageQueue!.Enqueue("Please set a valid Archives Directory in Settings (gear icon)");

            return;
        }

        if (TileViewModels.Count == 0)
        {
            Snackbar.MessageQueue!.Enqueue("No tiles to import");

            return;
        }

        var isForeground = IsForeground;
        var isSnow = IsSnow;

        //capture the spliced layout (if any) for .map emission on the background path
        var layout = SplicedLayout;
        var sourcePath = SplicedSourcePath;

        ImportBtn.IsEnabled = false;
        BrowseBtn.IsEnabled = false;

        try
        {
            //capture data before async operation
            var tileData = TileViewModels.Select(vm => new TileImportData(vm.Image, vm.Flags))
                                         .ToList();

            string resultMessage;

            if (isForeground)
                resultMessage = await Task.Run(() => ImportForegroundTiles(archivePath, isSnow, tileData));
            else
                resultMessage = await Task.Run(() => ImportBackgroundTiles(archivePath, isSnow, tileData, layout, sourcePath));

            //clear the view models after successful import
            foreach (var vm in TileViewModels)
                vm.Dispose();

            TileViewModels.Clear();
            UpdateStatus(0);

            //the spliced set has been consumed
            SplicedLayout = null;
            SplicedSourcePath = null;

            //refresh all in-memory archives and render caches so imported tiles are visible without a restart
            CacheManager.RefreshAfterImport();

            Snackbar.MessageQueue!.Enqueue(resultMessage);
        } catch (Exception ex)
        {
            Snackbar.MessageQueue!.Enqueue($"Error: {ex.Message}");
        } finally
        {
            ImportBtn.IsEnabled = TileViewModels.Count > 0;
            BrowseBtn.IsEnabled = true;
        }
    }

    private static string ImportForegroundTiles(string archivePath, bool isSnow, List<TileImportData> tileData)
    {
        var ia = ArchiveCache.Ia;
        var prefix = isSnow ? "sts" : "stc";

        //load existing palette lookup
        var paletteLookup = PaletteLookup.FromArchive(prefix, ia);

        //find next available tile index
        var existingIndices = ia.Where(entry => entry.EntryName.StartsWithI(prefix) && entry.EntryName.EndsWithI(".hpf"))
                                .Select(entry => entry.TryGetNumericIdentifier(out var id) ? id : -1)
                                .Where(id => id >= 0)
                                .ToHashSet();

        var startingIndex = 1;

        while (existingIndices.Contains(startingIndex))
            startingIndex++;

        //quantize the entire imported set into a single shared palette
        var images = tileData.Select(td => td.Image)
                             .ToList();

        var flags = tileData.Select(td => td.Flags)
                            .ToList();

        //preserve non-transparent blacks in place before quantizing (mutates the list entries)
        ImageProcessor.PreserveNonTransparentBlacks(images);

        using var quantized = ImageProcessor.QuantizeMultiple(QuantizerOptions.Default, images.ToArray());
        (var quantizedImages, var palette) = quantized;

        //all imported tiles reference the one newly created palette
        var paletteId = paletteLookup.GetNextPaletteId();
        var tileIndex = startingIndex;
        var createdTileIds = new List<int>();

        //create an HPF file for each quantized image (order matches the input/flags order)
        for (var i = 0; i < quantizedImages.Count; i++)
        {
            var quantizedImage = quantizedImages[i];

            //find next available index
            while (existingIndices.Contains(tileIndex))
                tileIndex++;

            //create HPF with palettized data
            var hpfData = quantizedImage.GetPalettizedPixelData(palette);
            var hpf = new HpfFile(new byte[8], hpfData);

            //patch HPF file
            ia.Patch($"{prefix}{tileIndex:D5}.hpf", hpf);

            //add palette table entry (+1 offset based on MapEditorRenderUtil)
            paletteLookup.Table.Add(tileIndex + 1, paletteId);

            createdTileIds.Add(tileIndex);
            existingIndices.Add(tileIndex);
            tileIndex++;
        }

        //add the shared palette to lookup and patch
        paletteLookup.Palettes[paletteId] = palette;
        ia.Patch($"{prefix}{paletteId:D4}.pal", palette);

        //patch updated palette table
        ia.Patch($"{prefix}pal.tbl", paletteLookup.Table);

        //update SOTP.dat with tile flags
        UpdateSotp(ia, createdTileIds, flags);

        //save IA archive
        ia.Save(Path.Combine(archivePath, "ia.dat"));

        return $"Imported {tileData.Count} foreground tiles as {prefix} (IDs: {startingIndex}-{tileIndex - 1}, 1 shared palette)";
    }

    /// <summary>
    ///     Loads spliced tiles from the Tile Splicer tool
    /// </summary>
    public void LoadSplicedTiles(SplicedTileLayout layout, string? sourcePath)
    {
        //clear previous tiles
        foreach (var vm in TileViewModels)
            vm.Dispose();

        TileViewModels.Clear();

        //remember the spliced layout + source path so Import can also emit a .map next to the source
        SplicedLayout = layout;
        SplicedSourcePath = sourcePath;

        //ensure we're in background mode (spliced tiles are always 56x27 background tiles)
        BackgroundRadio.IsChecked = true;

        var index = 0;

        foreach (var tile in layout.OrderedImages)
        {
            TileViewModels.Add(
                new TileImportViewModel
                {
                    Image = tile,
                    FileName = $"spliced_{index:D4}.png",
                    ShowFlags = false
                });
            index++;
        }

        UpdateStatus(index);
        UpdateInfoMessages();
    }

    private void TileImportControl_OnLoaded(object sender, RoutedEventArgs e)
    {
        var archivePath = PathHelper.Instance.ArchivesPath;

        if (string.IsNullOrEmpty(archivePath) || !PathHelper.ArchivePathIsValid(archivePath))
        {
            NotConfiguredMessage.Visibility = Visibility.Visible;
            MainContent.Visibility = Visibility.Collapsed;
        } else
        {
            NotConfiguredMessage.Visibility = Visibility.Collapsed;
            MainContent.Visibility = Visibility.Visible;
        }

        UpdateInfoMessages();
    }

    private void TileType_Changed(object sender, RoutedEventArgs e)
    {
        //ignore if not fully loaded yet
        if (!IsLoaded)
            return;

        //clear tiles when switching type
        foreach (var vm in TileViewModels)
            vm.Dispose();

        TileViewModels.Clear();
        UpdateStatus(0);
        UpdateInfoMessages();
        ActiveFileChanged?.Invoke();
    }

    private void UpdateInfoMessages()
    {
        if (IsForeground)
        {
            InfoMessage.Text = "Select PNG images (28 pixels wide, any height) to import as foreground tiles (structures). "
                               + "Images will be grouped into shared palettes. Set Wall/Transparent flags for each tile.";
            BottomInfoMessage.Text = "Foreground tiles will be saved as HPF files in IA.dat. Tile flags will be saved to SOTP.dat.";
        } else
        {
            InfoMessage.Text = "Select PNG images (56x27 pixels) to import as background tiles (ground). "
                               + "Images will be grouped into shared palettes.";
            BottomInfoMessage.Text = "Background tiles will be appended to the existing tileset in SEO.dat.";
        }
    }

    private static void UpdateSotp(DataArchive ia, List<int> tileIds, List<TileFlags> flags)
    {
        //load existing SOTP data
        byte[] sotpData;

        if (ia.TryGetValue("sotp.dat", out var sotpEntry))
            sotpData = sotpEntry.ToSpan()
                                .ToArray();
        else
            sotpData = [];

        //calculate required size
        var maxTileId = tileIds.Count > 0 ? tileIds.Max() : 0;
        var requiredSize = maxTileId + 1;

        //expand array if needed
        if (sotpData.Length < requiredSize)
        {
            var newData = new byte[requiredSize];
            Array.Copy(sotpData, newData, sotpData.Length);
            sotpData = newData;
        }

        //set flags for new tiles
        for (var i = 0; (i < tileIds.Count) && (i < flags.Count); i++)
            sotpData[tileIds[i]] = (byte)flags[i];

        //patch and save
        ia.Patch("sotp.dat", new RawDataEntry(sotpData));
    }

    private void UpdateStatus(int validCount) => ImportBtn.IsEnabled = validCount > 0;

    //helper class for patching raw byte data
    private sealed class RawDataEntry(byte[] data) : ISavable
    {
        public void Save(string path)
        {
            using var stream = File.Create(path);
            Save(stream);
        }

        public void Save(Stream stream) => stream.Write(data, 0, data.Length);
    }

    private readonly record struct TileImportData(SKImage Image, TileFlags Flags);
}