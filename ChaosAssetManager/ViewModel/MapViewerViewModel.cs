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

#pragma warning disable CS8618, CS9264

namespace ChaosAssetManager.ViewModel;

public sealed class MapViewerViewModel : NotifyPropertyChangedBase, IDeltaUpdatable
{
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
        get;
        set => SetField(ref field, value);
    }

    public bool ForegroundChangePending
    {
        get;
        set => SetField(ref field, value);
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
    public SKMatrix ViwerTransform { get; set; } = SKMatrix.Identity;

    public static MapViewerViewModel Empty { get; } = new()
    {
        BackgroundChangePending = true,
        ForegroundChangePending = true,
        PossibleBounds = [],
        Bounds = new Rectangle(
            0,
            0,
            0,
            0),
        FromPath = ""
    };

    public ObservingCollection<TileViewModel> RawBackgroundTiles { get; } = [];
    public ObservingCollection<TileViewModel> RawLeftForegroundTiles { get; } = [];
    public ObservableCollection<TileViewModel> RawRightForegroundTiles { get; } = [];
    public FixedSizeDeque<ActionContext> RedoableActions { get; } = new(20);

    public FixedSizeDeque<ActionContext> UndoableActions { get; } = new(20);

    public ListSegment2D<TileViewModel> BackgroundTilesView => new(RawBackgroundTiles, Bounds.Width);

    public ListSegment2D<TileViewModel> LeftForegroundTilesView => new(RawLeftForegroundTiles, Bounds.Width);

    public ListSegment2D<TileViewModel> RightForegroundTilesView => new(RawRightForegroundTiles, Bounds.Width);

    public MapViewerViewModel()
    {
        RawBackgroundTiles.CollectionChanged += (_, _) => BackgroundChangePending = true;
        RawLeftForegroundTiles.CollectionChanged += (_, _) => ForegroundChangePending = true;
        RawRightForegroundTiles.CollectionChanged += (_, _) => ForegroundChangePending = true;
    }

    /// <inheritdoc />
    public void Update(TimeSpan delta)
    {
        foreach (var tile in RawBackgroundTiles)
            tile.Update(delta);

        foreach (var tile in RawLeftForegroundTiles)
            tile.Update(delta);

        foreach (var tile in RawRightForegroundTiles)
            tile.Update(delta);
    }

    public void AddAction(
        ActionType actionType,
        TileGrab before,
        TileGrab after,
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

        BackgroundChangePending = false;
        ForegroundChangePending = false;
    }

    public void RedoAction()
    {
        if (RedoableActions.Count == 0)
            return;

        var action = RedoableActions.PopNewest();
        action.Redo(this);
        UndoableActions.AddNewest(action);
    }

    public void UndoAction()
    {
        if (UndoableActions.Count == 0)
            return;

        var action = UndoableActions.PopNewest();
        action.Undo(this);
        RedoableActions.AddNewest(action);
    }
}