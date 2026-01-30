using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using Chaos.Extensions.Common;
using ChaosAssetManager.Helpers;
using ChaosAssetManager.ViewModel;
using DALib.Abstractions;
using DALib.Data;
using DALib.Definitions;
using DALib.Drawing;
using DALib.Extensions;
using DALib.Utility;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SkiaSharp;

namespace ChaosAssetManager.Controls;

public sealed partial class TileImportControl
{
    private bool IsForeground => ForegroundRadio.IsChecked == true;
    private bool IsSnow => SnowRadio.IsChecked == true;

    public ObservableCollection<TileImportViewModel> TileViewModels { get; } = [];

    private readonly record struct TileImportData(SKImage Image, TileFlags Flags);

    public TileImportControl()
    {
        InitializeComponent();

        PathHelper.ArchivesPathChanged += () => TileImportControl_OnLoaded(this, new RoutedEventArgs());
    }

    /// <summary>
    ///     Loads spliced tiles from the Tile Splicer tool
    /// </summary>
    public void LoadSplicedTiles(IEnumerable<SKImage> tiles)
    {
        //clear previous tiles
        foreach (var vm in TileViewModels)
            vm.Dispose();

        TileViewModels.Clear();

        //ensure we're in background mode (spliced tiles are always 56x27 background tiles)
        BackgroundRadio.IsChecked = true;

        var index = 0;

        foreach (var tile in tiles)
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

        UpdateStatus(index, 0);
        UpdateInfoMessages();
    }

    private void TileImportControl_OnLoaded(object sender, RoutedEventArgs e)
    {
        var archivePath = PathHelper.Instance.ArchivesPath;

        if (string.IsNullOrEmpty(archivePath) || !PathHelper.ArchivePathIsValid(archivePath))
        {
            NotConfiguredMessage.Visibility = Visibility.Visible;
            MainContent.Visibility = Visibility.Collapsed;
        }
        else
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
        UpdateStatus(0, 0);
        UpdateInfoMessages();
    }

    private void UpdateInfoMessages()
    {
        if (IsForeground)
        {
            InfoMessage.Text = "Select PNG images (28 pixels wide, any height) to import as foreground tiles (structures). " +
                               "Images will be grouped into shared palettes. Set Wall/Transparent flags for each tile.";
            BottomInfoMessage.Text = "Foreground tiles will be saved as HPF files in IA.dat. Tile flags will be saved to SOTP.dat.";
        }
        else
        {
            InfoMessage.Text = "Select PNG images (56x27 pixels) to import as background tiles (ground). " +
                               "Images will be grouped into shared palettes.";
            BottomInfoMessage.Text = "Background tiles will be appended to the existing tileset in SEO.dat.";
        }
    }

    private void BrowseBtn_OnClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = IsForeground
                ? "Select PNG Tile Images (28 pixels wide)"
                : "Select PNG Tile Images (56x27 pixels)",
            Filter = "PNG Images|*.png",
            Multiselect = true
        };

        if (dialog.ShowDialog() != true)
            return;

        //clear previous tiles
        foreach (var vm in TileViewModels)
            vm.Dispose();

        TileViewModels.Clear();

        var validCount = 0;
        var invalidCount = 0;
        var isForeground = IsForeground;

        foreach (var filePath in dialog.FileNames)
        {
            try
            {
                var image = SKImage.FromEncodedData(filePath);

                if (image is null)
                {
                    invalidCount++;

                    continue;
                }

                //validate dimensions based on tile type
                if (isForeground)
                {
                    //foreground: must be 28 pixels wide, any height
                    if (image.Width != CONSTANTS.HPF_TILE_WIDTH)
                    {
                        invalidCount++;
                        Snackbar.MessageQueue!.Enqueue(
                            $"Skipped {Path.GetFileName(filePath)}: Width must be 28 pixels (got {image.Width})");
                        image.Dispose();

                        continue;
                    }
                }
                else
                {
                    //background: must be exactly 56x27
                    if ((image.Width != CONSTANTS.TILE_WIDTH) || (image.Height != CONSTANTS.TILE_HEIGHT))
                    {
                        invalidCount++;
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
            }
            catch (Exception ex)
            {
                invalidCount++;
                Snackbar.MessageQueue!.Enqueue($"Error loading {Path.GetFileName(filePath)}: {ex.Message}");
            }
        }

        UpdateStatus(validCount, invalidCount);
    }

    private void UpdateStatus(int validCount, int invalidCount)
        => ImportBtn.IsEnabled = validCount > 0;

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
                resultMessage = await Task.Run(() => ImportBackgroundTiles(archivePath, isSnow, tileData));

            //clear the view models after successful import
            foreach (var vm in TileViewModels)
                vm.Dispose();

            TileViewModels.Clear();
            UpdateStatus(0, 0);

            //clear render caches so new tiles are visible
            MapEditorRenderUtil.Clear();

            Snackbar.MessageQueue!.Enqueue(resultMessage);
        }
        catch (Exception ex)
        {
            Snackbar.MessageQueue!.Enqueue($"Error: {ex.Message}");
        }
        finally
        {
            ImportBtn.IsEnabled = TileViewModels.Count > 0;
            BrowseBtn.IsEnabled = true;
        }
    }

    private static string ImportBackgroundTiles(
        string archivePath,
        bool isSnow,
        List<TileImportData> tileData)
    {
        var seo = ArchiveCache.Seo;
        var tilesetName = isSnow ? "tileas" : "tilea";

        //load existing tileset
        var existingTileset = Tileset.FromArchive(tilesetName, seo);
        var startingIndex = existingTileset.Count;

        //load existing palette lookup
        var paletteLookup = PaletteLookup.FromArchive("mpt", seo);

        //group images by color count to respect palette limits
        var images = tileData.Select(td => td.Image).ToList();
        var imageGroups = GroupImagesByColorCount(images);

        var tileIndex = startingIndex;
        var palettesCreated = 0;

        foreach (var group in imageGroups)
        {
            //create palettized tileset from this group
            using var palettized = Tileset.FromImages(group);
            (var newTileset, var newPalette) = palettized;

            //add tiles to existing tileset
            foreach (var tile in newTileset)
                existingTileset.Add(tile);

            //get next palette ID and add entries
            var paletteId = paletteLookup.GetNextPaletteId();

            for (var i = 0; i < newTileset.Count; i++)
            {
                //palette table uses +2 offset for tile IDs (based on MapEditorRenderUtil)
                paletteLookup.Table.Add(tileIndex + 2, paletteId);
                tileIndex++;
            }

            //add palette to lookup
            paletteLookup.Palettes[paletteId] = newPalette;

            //patch new palette
            seo.Patch($"mpt{paletteId:D3}.pal", newPalette);
            palettesCreated++;
        }

        //patch updated tileset
        seo.Patch($"{tilesetName}.bmp", existingTileset);

        //patch updated palette table
        seo.Patch("mptpal.tbl", paletteLookup.Table);

        //save SEO archive
        seo.Save(Path.Combine(archivePath, "seo.dat"));

        return $"Imported {tileData.Count} background tiles to {tilesetName} (starting at index {startingIndex}, {palettesCreated} palettes)";
    }

    private static string ImportForegroundTiles(
        string archivePath,
        bool isSnow,
        List<TileImportData> tileData)
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

        //group images by color count
        var images = tileData.Select(td => td.Image).ToList();
        var flags = tileData.Select(td => td.Flags).ToList();
        var imageGroups = GroupImagesByColorCount(images);

        //track which images belong to which group for flag association
        var imageToGroupIndex = new Dictionary<SKImage, int>();
        var currentImageIndex = 0;

        for (var groupIndex = 0; groupIndex < imageGroups.Count; groupIndex++)
        {
            foreach (var img in imageGroups[groupIndex])
            {
                imageToGroupIndex[img] = groupIndex;
                currentImageIndex++;
            }
        }

        var tileIndex = startingIndex;
        var palettesCreated = 0;
        var createdTileIds = new List<int>();

        foreach (var group in imageGroups)
        {
            //quantize all images in group together for shared palette
            ImageProcessor.PreserveNonTransparentBlacks(group.ToArray());

            using var quantized = ImageProcessor.QuantizeMultiple(QuantizerOptions.Default, group.ToArray());
            (var quantizedImages, var palette) = quantized;

            //get next palette ID
            var paletteId = paletteLookup.GetNextPaletteId();

            //create HPF files for each image in group
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

            //add palette to lookup and patch
            paletteLookup.Palettes[paletteId] = palette;
            ia.Patch($"{prefix}{paletteId:D3}.pal", palette);
            palettesCreated++;
        }

        //patch updated palette table
        ia.Patch($"{prefix}pal.tbl", paletteLookup.Table);

        //update SOTP.dat with tile flags
        UpdateSotp(ia, createdTileIds, flags);

        //save IA archive
        ia.Save(Path.Combine(archivePath, "ia.dat"));

        return $"Imported {tileData.Count} foreground tiles as {prefix} (IDs: {startingIndex}-{tileIndex - 1}, {palettesCreated} palettes)";
    }

    private static void UpdateSotp(DataArchive ia, List<int> tileIds, List<TileFlags> flags)
    {
        //load existing SOTP data
        byte[] sotpData;

        if (ia.TryGetValue("sotp.dat", out var sotpEntry))
            sotpData = sotpEntry.ToSpan().ToArray();
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
        for (var i = 0; i < tileIds.Count && i < flags.Count; i++)
            sotpData[tileIds[i]] = (byte)flags[i];

        //patch and save
        ia.Patch("sotp.dat", new RawDataEntry(sotpData));
    }

    private static List<List<SKImage>> GroupImagesByColorCount(List<SKImage> images)
    {
        var groups = new List<List<SKImage>>();
        var currentGroup = new List<SKImage>();
        var currentColors = new HashSet<SKColor>();

        foreach (var image in images)
        {
            var imageColors = GetUniqueColors(image);

            var combinedColors = new HashSet<SKColor>(currentColors);

            foreach (var color in imageColors)
                combinedColors.Add(color);

            if ((combinedColors.Count > CONSTANTS.COLORS_PER_PALETTE) && (currentGroup.Count > 0))
            {
                groups.Add(currentGroup);
                currentGroup = [];
                currentColors.Clear();

                foreach (var color in imageColors)
                    currentColors.Add(color);
            }
            else
                currentColors = combinedColors;

            currentGroup.Add(image);
        }

        if (currentGroup.Count > 0)
            groups.Add(currentGroup);

        return groups;
    }

    private static HashSet<SKColor> GetUniqueColors(SKImage image)
    {
        using var bitmap = SKBitmap.FromImage(image);
        var colors = new HashSet<SKColor>();

        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                var pixel = bitmap.GetPixel(x, y);

                if (pixel.Alpha > 0)
                    colors.Add(pixel.WithAlpha(255));
            }
        }

        return colors;
    }

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
}
