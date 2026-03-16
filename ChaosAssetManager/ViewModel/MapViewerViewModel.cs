using System.Collections.ObjectModel;
using System.IO;
using Chaos.Time.Abstractions;
using Chaos.Wpf.Abstractions;
using Chaos.Wpf.Collections.ObjectModel;
using ChaosAssetManager.Controls.MapEditorControls;
using ChaosAssetManager.Definitions;
using ChaosAssetManager.Helpers;
using ChaosAssetManager.Model;
using SkiaSharp;
using Rectangle = Chaos.Geometry.Rectangle;

// ReSharper disable ConvertToAutoProperty

#pragma warning disable CS8618, CS9264

namespace ChaosAssetManager.ViewModel;

public sealed class MapViewerViewModel : NotifyPropertyChangedBase, IDeltaUpdatable
{
    //structure editing properties
    public bool IsStructure { get; set; }
    public string? StructureId { get; set; }
    public string? OriginalStructureId { get; set; } //tracks original id for updates

    public bool BackgroundChangePending
    {
        get;
        set => SetField(ref field, value);
    }

    public required Rectangle Bounds
    {
        get;

        set
        {
            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            if (value is null)
                return;

            SetField(ref field, value);
            BackgroundChangePending = true;
            ForegroundChangePending = true;
        }
    }

    public MapViewerControl? Control { get; set; }

    public string FileName
    {
        get => IsStructure ? (StructureId ?? "New Structure") : field;
        set => SetField(ref field, value);
    }

    public bool ForegroundChangePending
    {
        get;

        set
        {
            SetField(ref field, value);

            if (value)
                TabMapChangePending = true;
        }
    }

    public required string FromPath
    {
        get;

        set
        {
            FileName = Path.GetFileName(value);
            SetField(ref field, value);
        }
    }

    public required List<MapBounds> PossibleBounds { get; set; } = [];

    public bool TabMapChangePending
    {
        get;
        set => SetField(ref field, value);
    }

    public ChunkManager? ChunkMgr { get; set; }
    public SKMatrix? ViewerTransform { get; set; }

    public static MapViewerViewModel Empty { get; } = new()
    {
        PossibleBounds = [],
        Bounds = new Rectangle(
            0,
            0,
            0,
            0),
        FromPath = "",
        BackgroundChangePending = false,
        ForegroundChangePending = false,
        TabMapChangePending = false
    };

    public ObservingCollection<TileViewModel> RawBackgroundTiles { get; } = [];
    public ObservingCollection<TileViewModel> RawLeftForegroundTiles { get; } = [];
    public ObservableCollection<TileViewModel> RawRightForegroundTiles { get; } = [];
    private List<(TileViewModel Tile, int Index, LayerFlags Layer)> AnimatedTiles { get; set; } = [];

    public FixedSizeDeque<ActionContext> RedoableActions { get; } = new(100);

    public FixedSizeDeque<ActionContext> UndoableActions { get; } = new(100);

    public ListSegment2D<TileViewModel> BackgroundTilesView => new(RawBackgroundTiles, Bounds.Width);

    public ListSegment2D<TileViewModel> LeftForegroundTilesView => new(RawLeftForegroundTiles, Bounds.Width);

    public ListSegment2D<TileViewModel> RightForegroundTilesView => new(RawRightForegroundTiles, Bounds.Width);

    public MapViewerViewModel()
    {
        RawBackgroundTiles.CollectionChanged += (_, _) =>
        {
            ChunkMgr?.MarkAllDirty(LayerFlags.Background);
            BackgroundChangePending = true;
        };

        RawLeftForegroundTiles.CollectionChanged += (_, _) =>
        {
            ChunkMgr?.MarkAllDirty(LayerFlags.LeftForeground);
            ForegroundChangePending = true;
        };

        RawRightForegroundTiles.CollectionChanged += (_, _) =>
        {
            ChunkMgr?.MarkAllDirty(LayerFlags.RightForeground);
            ForegroundChangePending = true;
        };
    }

    /// <inheritdoc />
    public void Update(TimeSpan delta)
    {
        var bgDirtied = false;
        var fgDirtied = false;

        foreach (var (tile, index, layer) in AnimatedTiles)
        {
            tile.Update(delta);

            if (!tile.FrameChanged)
                continue;

            tile.FrameChanged = false;

            if (ChunkMgr is not null)
            {
                var x = index % Bounds.Width;
                var y = index / Bounds.Width;
                ChunkMgr.MarkDirty(x, y, layer);
            }

            if (layer == LayerFlags.Background)
                bgDirtied = true;
            else
                fgDirtied = true;
        }

        if (bgDirtied)
            BackgroundChangePending = true;

        if (fgDirtied)
            ForegroundChangePending = true;
    }

    public void RebuildAnimatedTileList()
    {
        var list = new List<(TileViewModel, int, LayerFlags)>();

        CollectAnimatedTiles(list, RawBackgroundTiles, LayerFlags.Background);
        CollectAnimatedTiles(list, RawLeftForegroundTiles, LayerFlags.LeftForeground);
        CollectAnimatedTiles(list, RawRightForegroundTiles, LayerFlags.RightForeground);

        AnimatedTiles = list;
    }

    private static void CollectAnimatedTiles(
        List<(TileViewModel, int, LayerFlags)> list,
        IReadOnlyList<TileViewModel> tiles,
        LayerFlags layer)
    {
        for (var i = 0; i < tiles.Count; i++)
            if (tiles[i].Animation is { Frames.Count: > 1 })
                list.Add((tiles[i], i, layer));
    }

    public void AddAction(
        ActionType actionType,
        TileGrabViewModel before,
        TileGrabViewModel after,
        LayerFlags layerFlags,
        SKPoint tileCoordinates)
    {
        var actionContext = new ActionContext
        {
            ActionType = actionType,
            Before = before,
            After = after,
            LayerFlags = layerFlags,
            TileCoordinates = tileCoordinates
        };

        UndoableActions.AddNewest(actionContext);
        RedoableActions.Clear();
    }

    public void Initialize()
    {
        foreach (var tile in RawBackgroundTiles)
            tile.Initialize();

        foreach (var tile in RawLeftForegroundTiles)
            tile.Initialize();

        foreach (var tile in RawRightForegroundTiles)
            tile.Initialize();

        RebuildAnimatedTileList();

        BackgroundChangePending = false;
        ForegroundChangePending = false;
    }

    public void RedoAction(MapEditorViewModel editorViewModel)
    {
        if (RedoableActions.Count == 0)
            return;

        var action = RedoableActions.PopNewest();
        action.Redo(this, editorViewModel);
        UndoableActions.AddNewest(action);
    }

    public void Refresh()
    {
        foreach (var tile in RawBackgroundTiles)
            tile.Refresh();

        foreach (var tile in RawLeftForegroundTiles)
            tile.Refresh();

        foreach (var tile in RawRightForegroundTiles)
            tile.Refresh();

        RebuildAnimatedTileList();
    }

    public void UndoAction(MapEditorViewModel editorViewModel)
    {
        if (UndoableActions.Count == 0)
            return;

        var action = UndoableActions.PopNewest();
        action.Undo(this, editorViewModel);
        RedoableActions.AddNewest(action);
    }
}