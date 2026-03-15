using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Chaos.Extensions.Common;
using ChaosAssetManager.Definitions;
using ChaosAssetManager.Helpers;
using ChaosAssetManager.Model;
using ChaosAssetManager.ViewModel;
using DALib.Data;
using DALib.Drawing;
using SkiaSharp;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

// ReSharper disable ClassCanBeSealed.Global

namespace ChaosAssetManager.Controls;

public partial class HeaEditorControl
{
    public static HeaEditorControl? Instance { get; private set; }

    public HeaEditorViewModel ViewModel { get; set; } = new();

    public HeaEditorControl()
    {
        Instance = this;
        InitializeComponent();
        ViewerControl.ViewModel = ViewModel;
        LoadPrefabsFromRepository();
    }

    private void BrushShape_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || (BrushShapeCmbx.SelectedIndex < 0) || (BrushShapeCmbx.SelectedIndex > 2))
            return;

        ViewModel.SelectedBrushShape = (HeaBrushShape)BrushShapeCmbx.SelectedIndex;

        //switching to a shape clears the prefab selection
        ViewModel.SelectedPrefabBrush = null;
        PrefabsControl.SelectedItem = null;
        ViewerControl.InvalidateBrushPreview();
    }

    private void DimensionsCmbx_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ViewModel.SelectedBounds is null || (ViewModel.LoadedMapId < 0))
            return;

        //don't re-render when selector is locked (loaded from .hea)
        if (!ViewModel.IsDimensionsSelectorEnabled)
            return;

        var mapId = ViewModel.LoadedMapId;
        var tileW = ViewModel.SelectedBounds.Width;
        var tileH = ViewModel.SelectedBounds.Height;

        ViewModel.LoadedMapWidth = tileW;
        ViewModel.LoadedMapHeight = tileH;
        ViewModel.StatusText = $"Map {mapId} ({tileW}x{tileH}) - New";

        //reinitialize the light grid with new dimensions
        if (ViewerControl.LightGrid is null)
        {
            var scanW = 28 * (tileW + tileH) + 1280;
            var scanH = 14 * (tileW + tileH) + 960;

            ViewerControl.Initialize(tileW, tileH, new byte[scanH, scanW]);
        } else
            ViewerControl.Reinitialize(tileW, tileH);

        //render the actual map tiles with the selected dimensions
        ViewerControl.LoadMapBackground(mapId, tileW, tileH);
        ViewerControl.CenterOnMap();
    }

    private void ExtractPrefabsBtn_OnClick(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.IsMapLoaded)
        {
            ShowMessage("No light map loaded");

            return;
        }

        var lightGrid = ViewerControl.LightGrid;

        if (lightGrid is null)
        {
            ShowMessage("No light grid data");

            return;
        }

        try
        {
            var extracted = LightPrefabRepository.Instance.ExtractFromGrid(lightGrid);

            if (extracted.Count == 0)
            {
                ShowMessage("No new light patterns found");

                return;
            }

            foreach (var prefab in extracted)
                LightPrefabRepository.Instance.Add(prefab);

            LoadPrefabsFromRepository();
            ShowMessage($"Extracted {extracted.Count} light prefab(s)");
        } catch (Exception ex)
        {
            ShowMessage($"Error extracting: {ex.Message}");
        }
    }

    private void HeaEditorControl_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        // ReSharper disable once SwitchStatementMissingSomeEnumCasesNoDefault
        switch (key)
        {
            case Key.Z when Keyboard.Modifiers.HasFlag(ModifierKeys.Control):
                ViewerControl.Undo();
                e.Handled = true;

                break;
            case Key.Y when Keyboard.Modifiers.HasFlag(ModifierKeys.Control):
                ViewerControl.Redo();
                e.Handled = true;

                break;
            case Key.S when Keyboard.Modifiers.HasFlag(ModifierKeys.Control):
                SaveBtn_OnClick(sender, e);
                e.Handled = true;

                break;

            //prevent Alt from activating the menu bar (Alt+scroll changes intensity)
            case Key.LeftAlt:
            case Key.RightAlt:
                e.Handled = true;

                break;
        }
    }

    private void HeaEditorControl_OnPreviewKeyUp(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        //suppress Alt key-up to prevent WPF menu bar activation
        if (key is Key.LeftAlt or Key.RightAlt)
            e.Handled = true;
    }

    private void LoadBtn_OnClick(object sender, RoutedEventArgs e)
    {
        //close the dropdown menu
        NewBtn.IsSubmenuOpen = false;

        if (string.IsNullOrEmpty(PathHelper.Instance.ArchivesPath))
        {
            ShowMessage("Fix the archive path first");

            return;
        }

        if (!int.TryParse(MapIdTbx.Text, out var mapId) || (mapId < 0))
        {
            ShowMessage("Invalid map ID");

            return;
        }

        //check if .hea exists - if so, load it with known dimensions
        try
        {
            var seoDat = ArchiveCache.Seo;
            var heaFileName = $"{mapId:D6}.hea";

            if (seoDat.TryGetValue(heaFileName, out _))
            {
                LoadExistingHea(mapId, seoDat);

                return;
            }
        } catch (Exception ex)
        {
            ShowMessage($"Error checking HEA: {ex.Message}");

            return;
        }

        //no .hea - detect dimensions from .map, render map with best guess, let user adjust
        LoadNewFromMap(mapId);
    }

    private void LoadExistingHea(int mapId, DataArchive seoDat)
    {
        var hea = HeaFile.FromArchive(mapId, seoDat);

        //decode the full light grid by stitching all layers
        var lightGrid = new byte[hea.ScanlineCount, hea.ScanlineWidth];

        for (var layer = 0; layer < hea.LayerCount; layer++)
        {
            var layerWidth = hea.GetLayerWidth(layer);
            var xOffset = hea.Thresholds[layer];

            for (var y = 0; y < hea.ScanlineCount; y++)
            {
                var scanline = hea.DecodeScanline(layer, y);

                for (var x = 0; x < layerWidth; x++)
                    lightGrid[y, xOffset + x] = (byte)Math.Min(255, (scanline[x] * 255 + HeaFile.MAX_LIGHT_VALUE / 2) / HeaFile.MAX_LIGHT_VALUE);
            }
        }

        var tileW = hea.TileWidth;
        var tileH = hea.TileHeight;

        //set dimensions from .hea and lock the selector
        ViewModel.PossibleBounds.Clear();

        var bounds = new MapBounds
        {
            Width = tileW,
            Height = tileH
        };

        ViewModel.PossibleBounds.Add(bounds);
        ViewModel.SelectedBounds = bounds;
        ViewModel.IsDimensionsSelectorEnabled = false;

        ViewModel.LoadedMapId = mapId;
        ViewModel.LoadedMapWidth = tileW;
        ViewModel.LoadedMapHeight = tileH;
        ViewModel.IsMapLoaded = true;
        ViewModel.StatusText = $"Map {mapId} ({tileW}x{tileH}) - Loaded from seo.dat";

        ViewerControl.Initialize(tileW, tileH, lightGrid);
        ViewerControl.LoadMapBackground(mapId, tileW, tileH);
        ViewerControl.CenterOnMap();

        ShowMessage($"Loaded HEA for map {mapId}");
    }

    private void LoadNewFromMap(int mapId)
    {
        try
        {
            var mapPath = Path.Combine(PathHelper.Instance.ArchivesPath!, "maps", $"lod{mapId}.map");

            if (!File.Exists(mapPath))
            {
                ShowMessage($"No .hea or .map file found for map {mapId}");

                return;
            }

            var fileSize = new FileInfo(mapPath).Length;
            var totalTiles = (int)(fileSize / 6);

            if (totalTiles <= 0)
            {
                ShowMessage("Invalid map file");

                return;
            }

            //compute possible dimensions, same as map editor
            var possibleBounds = BoundsHelper.GetFactorPairs(totalTiles);

            foreach (var pair in possibleBounds.ToList())
                if ((pair.Item1 > 255) || (pair.Item2 > 255))
                    possibleBounds.Remove(pair);

            possibleBounds = BoundsHelper.OrderByMostSquare(possibleBounds)
                                         .ToList();

            if (possibleBounds.Count == 0)
            {
                ShowMessage("Could not determine valid dimensions for map");

                return;
            }

            ViewModel.PossibleBounds.Clear();

            foreach (var pair in possibleBounds)
                ViewModel.PossibleBounds.Add(
                    new MapBounds
                    {
                        Width = pair.Item1,
                        Height = pair.Item2
                    });

            //pick the most-square as default
            var mostSquare = BoundsHelper.FindMostSquarePair(possibleBounds);

            var defaultBounds = ViewModel.PossibleBounds.First(b => (b.Width == mostSquare.Item1) && (b.Height == mostSquare.Item2));

            ViewModel.IsDimensionsSelectorEnabled = true;
            ViewModel.LoadedMapId = mapId;
            ViewModel.IsMapLoaded = true;

            //this triggers DimensionsCmbx_OnSelectionChanged which initializes the viewer + renders the map
            ViewModel.SelectedBounds = defaultBounds;

            ShowMessage($"No .hea found for map {mapId}. Adjust dimensions if needed, then paint lights and save.");
        } catch (Exception ex)
        {
            ShowMessage($"Error reading map: {ex.Message}");
        }
    }

    public void LoadPrefabsFromRepository()
    {
        PrefabsControl.ItemsSource = null;
        PrefabsControl.ItemsSource = LightPrefabRepository.Instance.Prefabs;
    }

    private void MapIdTbx_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Return)
        {
            e.Handled = true;
            LoadBtn_OnClick(sender, e);
        }
    }

    private void NewBtn_OnSubmenuOpened(object sender, RoutedEventArgs e)
        =>

            //defer focus until the submenu is fully rendered
            Dispatcher.BeginInvoke(
                DispatcherPriority.Input,
                () =>
                {
                    MapIdTbx.Focus();
                    MapIdTbx.SelectAll();
                });

    private void DarknessCmbx_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!ViewModel.IsMapLoaded)
            return;

        ViewerControl.RebuildDarknessOverlay();
        ViewerControl.Redraw();
    }

    private void PrefabsControl_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PrefabsControl.SelectedItem is not LightPrefab prefab)
            return;

        ViewModel.SelectedPrefabBrush = prefab.ToBrush();
        ViewerControl.InvalidateBrushPreview();

        //show "Prefab" in the shape combo without it being a selectable option
        BrushShapeCmbx.SelectedIndex = -1;
        BrushShapeCmbx.Text = "Prefab";
    }

    private void RedoBtn_OnClick(object sender, RoutedEventArgs e) => ViewerControl.Redo();

    private void SaveBtn_OnClick(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.IsMapLoaded)
        {
            ShowMessage("No light map loaded");

            return;
        }

        var lightGrid = ViewerControl.LightGrid;

        if (lightGrid is null)
        {
            ShowMessage("No light grid data");

            return;
        }

        try
        {
            var tileW = ViewModel.LoadedMapWidth;
            var tileH = ViewModel.LoadedMapHeight;
            var mapId = ViewModel.LoadedMapId;

            var scanW = lightGrid.GetLength(1);
            var scanH = lightGrid.GetLength(0);

            //convert the light grid to an SKImage (alpha = light intensity)
            using var bitmap = new SKBitmap(scanW, scanH, SKColorType.Bgra8888, SKAlphaType.Unpremul);
            using var pixMap = bitmap.PeekPixels();
            var pixels = pixMap.GetPixelSpan<SKColor>();

            for (var y = 0; y < scanH; y++)
                for (var x = 0; x < scanW; x++)
                    pixels[y * scanW + x] = new SKColor(255, 255, 255, lightGrid[y, x]);

            using var image = SKImage.FromBitmap(bitmap);
            var hea = HeaFile.FromImage(image, tileW, tileH);

            //patch into seo.dat
            var seoDat = ArchiveCache.Seo;
            var heaFileName = $"{mapId:D6}.hea";

            seoDat.Patch(heaFileName, hea);

            //save the archive
            var archivePath = Path.Combine(PathHelper.Instance.ArchivesPath!, "seo.dat");
            seoDat.Save(archivePath);

            //lock the dimensions selector now that a .hea exists
            ViewModel.IsDimensionsSelectorEnabled = false;
            ViewModel.StatusText = $"Map {mapId} ({tileW}x{tileH}) - Saved";
            ShowMessage("Light map saved to seo.dat");
        } catch (Exception ex)
        {
            ShowMessage($"Error saving: {ex.Message}");
        }
    }

    private void ShowMessage(string message, TimeSpan? time = null)
        => Snackbar.MessageQueue?.Enqueue(
            message,
            null,
            null,
            null,
            false,
            true,
            time ?? TimeSpan.FromMilliseconds(500));

    private void ToolType_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count != 1)
            return;

        var listBoxItem = (ListBoxItem)e.AddedItems[0]!;

        if (listBoxItem.Name.EqualsI(nameof(DrawToolBtn)))
            ViewModel.SelectedTool = HeaToolType.Draw;
        else if (listBoxItem.Name.EqualsI(nameof(EraseToolBtn)))
            ViewModel.SelectedTool = HeaToolType.Erase;
    }

    private void UndoBtn_OnClick(object sender, RoutedEventArgs e) => ViewerControl.Undo();
}