using Chaos.Time.Abstractions;
using Chaos.Wpf.Abstractions;
using ChaosAssetManager.Definitions;
using ChaosAssetManager.Helpers;
using ChaosAssetManager.Model;
using SkiaSharp;

namespace ChaosAssetManager.ViewModel;

public sealed class TileViewModel : NotifyPropertyChangedBase, IDeltaUpdatable
{
    private static TimeSpan GlobalElapsed;
    public static bool SnowTileset { get; set; }
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

    /// <summary>
    ///     Optional callback invoked when the animation frame advances. Used by picker/preview controls
    ///     that need to invalidate without going through PropertyChanged (which would trigger
    ///     ObservingCollection.CollectionChanged on map tiles)
    /// </summary>
    public Action? OnFrameAdvanced { get; set; }

    public required LayerFlags LayerFlags
    {
        get;
        set => SetField(ref field, value);
    }

    public required short TileId
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

    /// <summary>
    ///     Advances the shared global animation clock. Call once per frame before updating tiles
    /// </summary>
    public static void AdvanceGlobalClock(TimeSpan delta) => GlobalElapsed += delta;

    public void Update(TimeSpan delta)
    {
        if (Animation is null || Animation.Frames.Count <= 1 || Animation.FrameIntervalMs <= 0)
            return;

        var newIndex = (int)(GlobalElapsed.TotalMilliseconds / Animation.FrameIntervalMs) % Animation.Frames.Count;

        if (newIndex == _currentFrameIndex)
            return;

        _currentFrameIndex = newIndex;
        FrameChanged = true;
        OnFrameAdvanced?.Invoke();
    }

    public TileViewModel Clone()
        => new()
        {
            LayerFlags = LayerFlags,
            TileId = TileId,
            CurrentFrameIndex = CurrentFrameIndex
        };

    public void Initialize()
    {
        Animation = LayerFlags == LayerFlags.Background
            ? MapEditorRenderUtil.RenderAnimatedBackground(TileId, SnowTileset)
            : MapEditorRenderUtil.RenderAnimatedForeground(TileId, SnowTileset);

        if (Animation is null)
            return;

        //derive initial frame index from global clock so new tiles are immediately in sync
        if (Animation.Frames.Count > 1 && Animation.FrameIntervalMs > 0)
            _currentFrameIndex = (int)(GlobalElapsed.TotalMilliseconds / Animation.FrameIntervalMs) % Animation.Frames.Count;
        else
            _currentFrameIndex = 0;

        OnPropertyChanged(nameof(CurrentFrameIndex));
    }

    public void Refresh() => Initialize();
}
