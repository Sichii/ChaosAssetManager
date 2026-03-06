using Chaos.Common.Utilities;
using Chaos.Time;
using Chaos.Time.Abstractions;
using Chaos.Wpf.Abstractions;
using ChaosAssetManager.Controls;
using ChaosAssetManager.Definitions;
using ChaosAssetManager.Helpers;
using ChaosAssetManager.Model;
using SkiaSharp;

namespace ChaosAssetManager.ViewModel;

public sealed class TileViewModel : NotifyPropertyChangedBase, IDeltaUpdatable
{
    private static readonly DateTime Origin = DateTime.FromOADate(50);
    private readonly Lock Sync = new();
    private int _currentFrameIndex;

    public Animation? Animation
    {
        get;
        set => SetField(ref field, value);
    }

    public int CurrentFrameIndex
    {
        get => _currentFrameIndex;
        set => SetField(ref _currentFrameIndex, value);
    }

    //set during Update() when animation frame advances, cleared by ChunkManager dirty marking
    //does not raise PropertyChanged to avoid triggering ObservingCollection.CollectionChanged
    public bool FrameChanged { get; set; }

    public IIntervalTimer? FrameTimer { get; set; }

    public required LayerFlags LayerFlags
    {
        get;
        set => SetField(ref field, value);
    }

    public required int TileId
    {
        get;
        set => SetField(ref field, value);
    }

    public static TileViewModel EmptyBackground
    {
        get
        {
            var emptyTile = new TileViewModel
            {
                TileId = 0,
                LayerFlags = LayerFlags.Background
            };

            emptyTile.Initialize();

            return emptyTile;
        }
    }

    public static TileViewModel EmptyLeftForeground
    {
        get
        {
            var emptyTile = new TileViewModel
            {
                TileId = 0,
                LayerFlags = LayerFlags.LeftForeground
            };

            emptyTile.Initialize();

            return emptyTile;
        }
    }

    public static TileViewModel EmptyRightForeground
    {
        get
        {
            var emptyTile = new TileViewModel
            {
                TileId = 0,
                LayerFlags = LayerFlags.RightForeground
            };

            emptyTile.Initialize();

            return emptyTile;
        }
    }

    public SKImage? CurrentFrame => Animation?.Frames[CurrentFrameIndex];

    public void Update(TimeSpan delta)
    {
        using var @lock = Sync.EnterScope();

        if (Animation is null || FrameTimer is null)
            return;

        FrameTimer.Update(delta);

        if (FrameTimer.IntervalElapsed)
        {
            //directly set the backing field to avoid PropertyChanged / ObservingCollection churn
            _currentFrameIndex = (_currentFrameIndex + 1) % Animation.Frames.Count;
            FrameChanged = true;
        }
    }

    public TileViewModel Clone()
    {
        using var @lock = Sync.EnterScope();

        var vm = new TileViewModel
        {
            LayerFlags = LayerFlags,
            TileId = TileId,
            FrameTimer = FrameTimer is not null ? DeepClone.CreateRequired(FrameTimer) : null,
            CurrentFrameIndex = CurrentFrameIndex
        };

        return vm;
    }

    public void Initialize()
    {
        Animation = LayerFlags == LayerFlags.Background
            ? MapEditorRenderUtil.RenderAnimatedBackground(TileId, MapEditorControl.Instance.ViewModel.SnowTileset)
            : MapEditorRenderUtil.RenderAnimatedForeground(TileId, MapEditorControl.Instance.ViewModel.SnowTileset);

        if (Animation is null)
            return;

        //only create a timer for multi-frame animations
        //single-frame tiles don't need to tick and would just waste CPU marking dirty
        if (FrameTimer is null && Animation.Frames.Count > 1)
        {
            FrameTimer = new IntervalTimer(TimeSpan.FromMilliseconds(Animation.FrameIntervalMs), false);
            FrameTimer.SetOrigin(Origin);
        }

        OnPropertyChanged(nameof(CurrentFrameIndex));
    }

    public void Refresh() => Initialize();
}