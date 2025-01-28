using System.Collections.ObjectModel;
using Chaos.Wpf.Abstractions;
using Chaos.Wpf.Collections.ObjectModel;
using ChaosAssetManager.Controls;
using ChaosAssetManager.Definitions;
using ChaosAssetManager.Model;
using SkiaSharp;

namespace ChaosAssetManager.ViewModel;

public class MapEditorViewModel : NotifyPropertyChangedBase
{
    public ObservingCollection<TileRowViewModel> BackgroundTiles
    {
        get;
        set => SetField(ref field, value);
    } = [];

    public MapViewerViewModel CurrentMapViewer
    {
        get;
        set => SetField(ref field, value);
    } = MapViewerViewModel.Empty;

    public LayerFlags EditingLayerFlags
    {
        get;
        set => SetField(ref field, value);
    } = LayerFlags.Background;

    public ObservingCollection<TileRowViewModel> ForegroundTiles
    {
        get;
        set => SetField(ref field, value);
    } = [];

    public SKPoint? MouseHoverTileCoordinates
    {
        get;
        set => SetField(ref field, value);
    }

    public ObservableCollection<MapBounds> PossibleBounds
    {
        get;
        set => SetField(ref field, value);
    } = [];

    /// <summary>
    ///     Used to display the selected tile index on the footer
    /// </summary>
    public int SelectedTileIndex
    {
        get;
        set => SetField(ref field, value);
    }

    public ToolType SelectedTool
    {
        get;
        set => SetField(ref field, value);
    }

    public bool ShowBackground
    {
        get;

        set
        {
            SetField(ref field, value);
            MapEditorControl.Instance.ViewModel.CurrentMapViewer.BackgroundChangePending = true;
        }
    } = true;

    public bool ShowLeftForeground
    {
        get;

        set
        {
            SetField(ref field, value);
            MapEditorControl.Instance.ViewModel.CurrentMapViewer.ForegroundChangePending = true;
        }
    } = true;

    public bool ShowRightForeground
    {
        get;

        set
        {
            SetField(ref field, value);
            MapEditorControl.Instance.ViewModel.CurrentMapViewer.ForegroundChangePending = true;
        }
    } = true;

    public bool ShowTabMap
    {
        get;

        set
        {
            SetField(ref field, value);
            MapEditorControl.Instance.ViewModel.CurrentMapViewer.TabMapChangePending = true;
        }
    } = false;

    public bool SnowTileset
    {
        get;
        set => SetField(ref field, value);
    }

    public TileGrab? TileGrab
    {
        get;
        set => SetField(ref field, value);
    } = null;

    public ObservableCollection<MapViewerViewModel> Maps { get; } = [];

    public MapEditorViewModel()
    {
        ForegroundTiles.CollectionChanged += (_, _) => OnPropertyChanged(nameof(ForegroundTiles));
        BackgroundTiles.CollectionChanged += (_, _) => OnPropertyChanged(nameof(BackgroundTiles));
        PossibleBounds.CollectionChanged += (_, _) => OnPropertyChanged(nameof(PossibleBounds));
    }
}