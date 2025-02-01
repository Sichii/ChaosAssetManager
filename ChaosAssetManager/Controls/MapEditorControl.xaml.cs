using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Chaos.Extensions.Common;
using Chaos.Time;
using ChaosAssetManager.Definitions;
using ChaosAssetManager.Extensions;
using ChaosAssetManager.Helpers;
using ChaosAssetManager.Model;
using ChaosAssetManager.ViewModel;
using DALib.Drawing;
using DALib.Extensions;
using ArgumentOutOfRangeException = System.ArgumentOutOfRangeException;
using Button = System.Windows.Controls.Button;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Rectangle = Chaos.Geometry.Rectangle;

// ReSharper disable ClassCanBeSealed.Global

namespace ChaosAssetManager.Controls;

public partial class MapEditorControl
{
    public static MapEditorControl Instance { get; private set; } = null!;

    public MapEditorViewModel ViewModel { get; set; } = new();

    public MapViewerViewModel? CurrentMapViewer => MapViewerTabControl.SelectedItem as MapViewerViewModel;

    public MapEditorControl()
    {
        InitializeComponent();

        Instance = this;

        if (PathHelper.Instance.ArchivePathIsValid())
            PopulateTileViewModels();

        _ = UpdateLoop();
    }

    private void ArchivePathBtn_OnClick(object sender, RoutedEventArgs e)
    {
        using var openFolderDialog = new FolderBrowserDialog();
        openFolderDialog.InitialDirectory = PathHelper.Instance.MapEditorArchivePath!;

        if (openFolderDialog.ShowDialog() == DialogResult.OK)
        {
            PathHelper.Instance.MapEditorArchivePath = openFolderDialog.SelectedPath;
            PathHelper.Instance.Save();
        }

        if (PathHelper.Instance.ArchivePathIsValid())
        {
            MapEditorRenderUtil.Clear();
            PopulateTileViewModels();
        }
    }

    private void DoRedo()
    {
        var selectedViewer = MapViewerTabControl.SelectedItem as MapViewerViewModel ?? MapViewerViewModel.Empty;

        selectedViewer.RedoAction();
    }

    private void DoUndo()
    {
        var selectedViewer = MapViewerTabControl.SelectedItem as MapViewerViewModel ?? MapViewerViewModel.Empty;

        selectedViewer.UndoAction();
    }

    private void EditingToolType_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count != 1)
            return;

        var listBoxItem = (ListBoxItem)e.AddedItems[0]!;

        if (listBoxItem.Name.EqualsI(nameof(DrawToolBtn)))
            ViewModel.SelectedTool = ToolType.Draw;
        else if (listBoxItem.Name.EqualsI(nameof(SelectToolBtn)))
            ViewModel.SelectedTool = ToolType.Select;
        else if (listBoxItem.Name.EqualsI(nameof(SampleToolBtn)))
            ViewModel.SelectedTool = ToolType.Sample;
        else if (listBoxItem.Name.EqualsI(nameof(EraseToolBtn)))
            ViewModel.SelectedTool = ToolType.Erase;
    }

    private void EditTileType_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count != 1)
            return;

        var listBoxItem = (ListBoxItem)e.AddedItems[0]!;

        if (listBoxItem.Name.EqualsI(nameof(EditBackgroundBtn)))
            ViewModel.EditingLayerFlags = LayerFlags.Background;
        else if (listBoxItem.Name.EqualsI(nameof(EditLeftForegroundBtn)))
        {
            if (ViewModel.EditingLayerFlags == LayerFlags.RightForeground)
                HandleLeftOrRightSwap();

            ViewModel.EditingLayerFlags = LayerFlags.LeftForeground;
        } else if (listBoxItem.Name.EqualsI(nameof(EditRightForegroundBtn)))
        {
            if (ViewModel.EditingLayerFlags == LayerFlags.LeftForeground)
                HandleLeftOrRightSwap();

            ViewModel.EditingLayerFlags = LayerFlags.RightForeground;
        } else if (listBoxItem.Name.EqualsI(nameof(EditForegroundBtn)))
            ViewModel.EditingLayerFlags = LayerFlags.Foreground;
        else if (listBoxItem.Name.EqualsI(nameof(EditAllBtn)))
            ViewModel.EditingLayerFlags = LayerFlags.All;
    }

    /// <summary>
    ///     If the left or right foreground tilegrab only has 1 tile, swap it with the other side
    /// </summary>
    private void HandleLeftOrRightSwap()
    {
        if (ViewModel.TileGrab?.Bounds is { Width: 1, Height: 1 }
            && ((ViewModel.TileGrab.RawLeftForegroundTiles.Count == 1) ^ (ViewModel.TileGrab.RawRightForegroundTiles.Count == 1)))
        {
            var hasTile = ViewModel.TileGrab.RawLeftForegroundTiles.Count == 1
                ? ViewModel.TileGrab.RawLeftForegroundTiles
                : ViewModel.TileGrab.RawRightForegroundTiles;

            var doesNotHaveTile = ViewModel.TileGrab.RawLeftForegroundTiles == hasTile
                ? ViewModel.TileGrab.RawRightForegroundTiles
                : ViewModel.TileGrab.RawLeftForegroundTiles;

            doesNotHaveTile.Add(hasTile[0]);
            hasTile.Clear();
        }
    }

    private void LoadBtn_OnClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(PathHelper.Instance.MapEditorArchivePath))
        {
            ShowMessage("Fix the archive path first");

            return;
        }

        using var openFileDialog = new OpenFileDialog();
        openFileDialog.Filter = "Map files (*.map)|*.map";

        if (openFileDialog.ShowDialog() == DialogResult.OK)
        {
            var path = openFileDialog.FileName;

            using var stream = File.Open(
                path.WithExtension(".map"),
                new FileStreamOptions
                {
                    Access = FileAccess.Read,
                    Mode = FileMode.Open,
                    Options = FileOptions.SequentialScan,
                    Share = FileShare.ReadWrite
                });

            using var reader = new BinaryReader(stream, Encoding.Default, true);

            var backgroundTiles = new List<TileViewModel>();
            var leftForegroundTiles = new List<TileViewModel>();
            var rightForegroundTiles = new List<TileViewModel>();

            while (reader.BaseStream.Position < reader.BaseStream.Length)
            {
                var background = reader.ReadInt16();
                var leftForeground = reader.ReadInt16();
                var rightForeground = reader.ReadInt16();

                if (background > 0)
                    --background;

                backgroundTiles.Add(
                    new TileViewModel
                    {
                        TileId = background,
                        LayerFlags = LayerFlags.Background
                    });

                leftForegroundTiles.Add(
                    new TileViewModel
                    {
                        TileId = leftForeground,
                        LayerFlags = LayerFlags.LeftForeground
                    });

                rightForegroundTiles.Add(
                    new TileViewModel
                    {
                        TileId = rightForeground,
                        LayerFlags = LayerFlags.RightForeground
                    });
            }

            var possibleBounds = BoundsHelper.GetFactorPairs(backgroundTiles.Count);

            foreach (var pair in possibleBounds.ToList())
                if ((pair.Item1 > 255) || (pair.Item2 > 255))
                    possibleBounds.Remove(pair);

            possibleBounds = BoundsHelper.OrderByMostSquare(possibleBounds)
                                         .ToList();

            var mostSquareBounds = BoundsHelper.FindMostSquarePair(possibleBounds);

            var bounds = new Rectangle(
                0,
                0,
                mostSquareBounds.Item1,
                mostSquareBounds.Item2);

            var viewer = new MapViewerViewModel
            {
                PossibleBounds = possibleBounds.Select(
                                                   pair => new MapBounds
                                                   {
                                                       Width = pair.Item1,
                                                       Height = pair.Item2
                                                   })
                                               .ToList(),
                Bounds = bounds,
                FromPath = path
            };

            viewer.RawBackgroundTiles.AddRange(backgroundTiles);
            viewer.RawLeftForegroundTiles.AddRange(leftForegroundTiles);
            viewer.RawRightForegroundTiles.AddRange(rightForegroundTiles);

            viewer.Initialize();
            ViewModel.Maps.Add(viewer);

            MapViewerTabControl.SelectedItem = viewer;
        }
    }

    private void MapCloseBtn_OnClick(object sender, RoutedEventArgs e)
    {
        var button = (Button)sender;
        var tabItemParent = button.FindVisualParent<TabItem>();

        if (tabItemParent!.DataContext is not MapViewerViewModel viewer)
            return;

        viewer.Control?.Dispose();

        ViewModel.Maps.Remove(viewer);
    }

    private void MapEditorControl_OnKeyDown(object sender, KeyEventArgs e)
    {
        // ReSharper disable once SwitchStatementMissingSomeEnumCasesNoDefault
        switch (e.Key)
        {
            case Key.Z when Keyboard.Modifiers.HasFlag(ModifierKeys.Control):
                DoUndo();

                break;
            case Key.Y when Keyboard.Modifiers.HasFlag(ModifierKeys.Control):
                DoRedo();

                break;
        }
    }

    private void MapViewerTabControl_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var selectedTab = MapViewerTabControl.SelectedItem as MapViewerViewModel ?? MapViewerViewModel.Empty;

        ViewModel.PossibleBounds = new ObservableCollection<MapBounds>(selectedTab.PossibleBounds);
        ViewModel.CurrentMapViewer = selectedTab;
    }

    private void NewMapCreateBtn_OnClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(PathHelper.Instance.MapEditorArchivePath))
        {
            ShowMessage("Fix the archive path first");

            return;
        }

        if (!byte.TryParse(NewMapWidthTbx.Text, out var width))
        {
            ShowMessage("Invalid width");

            return;
        }

        if (!byte.TryParse(NewMapHeightTbx.Text, out var height))
        {
            ShowMessage("Invalid height");

            return;
        }

        var possibleBounds = BoundsHelper.GetFactorPairs(width * height)
                                         .Where(pair => pair is (<= 255, <= 255));

        possibleBounds = BoundsHelper.OrderByMostSquare(possibleBounds);

        var viewer = new MapViewerViewModel
        {
            Bounds = new Rectangle(
                0,
                0,
                width,
                height),
            FromPath = CONSTANTS.NEW_MAP_NAME,
            PossibleBounds = possibleBounds.Select(
                                               pair => new MapBounds
                                               {
                                                   Width = pair.Item1,
                                                   Height = pair.Item2
                                               })
                                           .ToList()
        };

        viewer.RawBackgroundTiles.AddRange(
            Enumerable.Range(0, width * height)
                      .Select(_ => TileViewModel.EmptyBackground));

        viewer.RawLeftForegroundTiles.AddRange(
            Enumerable.Range(0, width * height)
                      .Select(_ => TileViewModel.EmptyLeftForeground));

        viewer.RawRightForegroundTiles.AddRange(
            Enumerable.Range(0, width * height)
                      .Select(_ => TileViewModel.EmptyRightForeground));

        viewer.Initialize();

        ViewModel.Maps.Add(viewer);
        MapViewerTabControl.SelectedItem = viewer;
    }

    private void PopulateTileViewModels()
    {
        var iaDat = ArchiveCache.GetArchive(PathHelper.Instance.MapEditorArchivePath!, "ia.dat");
        var seoDat = ArchiveCache.GetArchive(PathHelper.Instance.MapEditorArchivePath!, "seo.dat");

        var foregroundCount = iaDat.Count(entry => entry.EntryName.StartsWithI("stc"));
        var tileset = Tileset.FromArchive("tilea.bmp", seoDat);
        var backgroundCount = tileset.Count;

        var foregroundTiles = iaDat.Where(entry => entry.EntryName.StartsWithI("stc"))
                                   .Select(entry =>
                                   {
                                       entry.TryGetNumericIdentifier(out var identifier);

                                       return identifier;
                                   })
                                        .Select(
                                            i => new TileViewModel
                                            {
                                                TileId = i,
                                                LayerFlags = LayerFlags.Foreground
                                            })
                                        .Chunk(4)
                                        .Select(
                                            chunk => new TileRowViewModel
                                            {
                                                Tile1 = chunk.ElementAtOrDefault(0),
                                                Tile2 = chunk.ElementAtOrDefault(1),
                                                Tile3 = chunk.ElementAtOrDefault(2),
                                                Tile4 = chunk.ElementAtOrDefault(3)
                                            });

        var backgroundtiles = Enumerable.Range(0, backgroundCount)
                                        .Select(
                                            i => new TileViewModel
                                            {
                                                TileId = i,
                                                LayerFlags = LayerFlags.Background
                                            })
                                        .Chunk(4)
                                        .Select(
                                            chunk => new TileRowViewModel
                                            {
                                                Tile1 = chunk.ElementAtOrDefault(0),
                                                Tile2 = chunk.ElementAtOrDefault(1),
                                                Tile3 = chunk.ElementAtOrDefault(2),
                                                Tile4 = chunk.ElementAtOrDefault(3)
                                            });

        ViewModel.ForegroundTiles.AddRange(foregroundTiles);
        ViewModel.BackgroundTiles.AddRange(backgroundtiles);
        ViewModel.ForegroundStructures.AddRange(CONSTANTS.FOREGROUND_STRUCTURES);
    }

    private void RedoBtn_OnClick(object sender, RoutedEventArgs e) => DoRedo();

    private void SaveAsBtn_OnClick(object sender, RoutedEventArgs e)
    {
        if (MapViewerTabControl.SelectedItem is null)
        {
            ShowMessage("No map selected");

            return;
        }

        var viewer = (MapViewerViewModel)MapViewerTabControl.SelectedItem;

        using var saveFileDialog = new SaveFileDialog();
        saveFileDialog.Filter = "Map files (*.map)|*.map";

        if (saveFileDialog.ShowDialog() == DialogResult.OK)
        {
            if (string.IsNullOrEmpty(saveFileDialog.FileName))
            {
                ShowMessage("Invalid file name");

                return;
            }

            viewer.FromPath = saveFileDialog.FileName.WithExtension(".map");

            SaveBtn_OnClick(sender, e);
        }
    }

    private void SaveBtn_OnClick(object sender, RoutedEventArgs e)
    {
        if (MapViewerTabControl.SelectedItem is null)
        {
            ShowMessage("No map selected");

            return;
        }

        var viewer = (MapViewerViewModel)MapViewerTabControl.SelectedItem;

        if (viewer.FromPath.EqualsI(CONSTANTS.NEW_MAP_NAME))
        {
            SaveAsBtn_OnClick(sender, e);

            return;
        }

        var path = viewer.FromPath;

        using (var stream = File.Open(
                   path.WithExtension(".map"),
                   new FileStreamOptions
                   {
                       Access = FileAccess.Write,
                       Mode = FileMode.Create,
                       Options = FileOptions.SequentialScan,
                       Share = FileShare.ReadWrite
                   }))
        {
            //clear existing data
            stream.SetLength(0);

            using var writer = new BinaryWriter(stream, Encoding.Default, true);

            var backgroundTiles = viewer.BackgroundTilesView;
            var leftForegroundTiles = viewer.LeftForegroundTilesView;
            var rightForegroundTiles = viewer.RightForegroundTilesView;

            for (var y = 0; y < viewer.Bounds.Height; y++)
                for (var x = 0; x < viewer.Bounds.Width; x++)
                {
                    var bgTileId = backgroundTiles[x, y].TileId;

                    if (bgTileId != 0)
                        ++bgTileId;

                    writer.Write((short)bgTileId);
                    writer.Write((short)leftForegroundTiles[x, y].TileId);
                    writer.Write((short)rightForegroundTiles[x, y].TileId);
                }
        }

        ShowMessage("Map saved successfully");
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

    private void SnowTileSetBtn_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        var listBoxItem = (ListBoxItem)sender;

        ViewModel.SnowTileset = listBoxItem.IsSelected;

        foreach (var row in ViewModel.BackgroundTiles)
            row.Refresh();

        foreach (var row in ViewModel.ForegroundTiles)
            row.Refresh();
    }

    private void StructuresControl_OnSelectedCellsChanged(object sender, SelectedCellsChangedEventArgs e)
    {
        var added = e.AddedCells;

        if (added.Count == 0)
            return;

        if (added[0].Item is not StructureViewModel row)
            return;

        if (ViewModel.EditingLayerFlags is not (LayerFlags.Foreground or LayerFlags.Background))
            return;

        ViewModel.TileGrab = row.ToTileGrab();
    }

    private void TextBoxBase_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        //nothing yet
    }

    private void TilesControl_OnSelectedCellsChanged(object sender, SelectedCellsChangedEventArgs e)
    {
        var added = e.AddedCells;

        if (added.Count == 0)
            return;

        if (added[0].Item is not TileRowViewModel row)
            return;

        if (ViewModel.EditingLayerFlags is LayerFlags.All or LayerFlags.Foreground)
            return;

        var column = added[0].Column;
        var columnIndex = column.DisplayIndex;

        var rowItem = columnIndex switch
        {
            0 => row.Tile1,
            1 => row.Tile2,
            2 => row.Tile3,
            3 => row.Tile4,
            _ => null
        };

        if (rowItem is null)
            return;

        ViewModel.SelectedTileIndex = rowItem.TileId;

        var tileGrab = new TileGrabViewModel
        {
            Bounds = new Rectangle(
                0,
                0,
                1,
                1)
        };

        var tile = rowItem.Clone();

        tile.Initialize();

        switch (ViewModel.EditingLayerFlags)
        {
            case LayerFlags.Background:
            {
                tileGrab.RawBackgroundTiles.Add(tile);

                break;
            }
            case LayerFlags.LeftForeground:
            {
                tileGrab.RawLeftForegroundTiles.Add(tile);

                break;
            }
            case LayerFlags.RightForeground:
            {
                tileGrab.RawRightForegroundTiles.Add(tile);

                break;
            }
            default:
                throw new ArgumentOutOfRangeException();
        }

        ViewModel.TileGrab = tileGrab;
    }

    private void UndoBtn_OnClick(object sender, RoutedEventArgs e) => DoUndo();

    private async Task UpdateLoop()
    {
        var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(1000d / 30d));
        var deltaTime = new DeltaTime();

        while (true)
            try
            {
                await timer.WaitForNextTickAsync();

                var delta = deltaTime.GetDelta;

                foreach (var row in ViewModel.BackgroundTiles)
                    row.Update(delta);

                foreach (var row in ViewModel.ForegroundTiles)
                    row.Update(delta);

                foreach (var structure in ViewModel.ForegroundStructures)
                    structure.Update(delta);

                ViewModel.CurrentMapViewer.Update(delta);
                ViewModel.TileGrab?.Update(delta);
            } catch
            {
                //ignored
            }
    }
}