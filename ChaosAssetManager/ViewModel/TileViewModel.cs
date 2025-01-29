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

    public Animation? Animation
    {
        get;
        set => SetField(ref field, value);
    }

    public int CurrentFrameIndex
    {
        get;
        set => SetField(ref field, value);
    }

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
        if (Animation is null || FrameTimer is null)
            return;

        FrameTimer.Update(delta);

        if (FrameTimer.IntervalElapsed)
            CurrentFrameIndex = (CurrentFrameIndex + 1) % Animation.Frames.Count;
    }

    public TileViewModel Clone()
        => new()
        {
            LayerFlags = LayerFlags,
            TileId = TileId,
            CurrentFrameIndex = CurrentFrameIndex,
            FrameTimer = DeepClone.CreateRequired(FrameTimer)
        };

    public void Initialize()
    {
        Animation = LayerFlags == LayerFlags.Background
            ? MapEditorRenderUtil.RenderAnimatedBackground(TileId, MapEditorControl.Instance.ViewModel.SnowTileset)
            : MapEditorRenderUtil.RenderAnimatedForeground(TileId, MapEditorControl.Instance.ViewModel.SnowTileset);

        if (Animation is null)
            return;

        if (FrameTimer is null)
        {
            FrameTimer = new IntervalTimer(TimeSpan.FromMilliseconds(Animation.FrameIntervalMs), false);
            FrameTimer.SetOrigin(Origin);
        }

        OnPropertyChanged(nameof(CurrentFrameIndex));
    }

    public void Refresh()
    {
        if (Animation is null)
            return;

        Initialize();
    }
}