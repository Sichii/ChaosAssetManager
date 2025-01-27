using Chaos.Time.Abstractions;
using Chaos.Wpf.Abstractions;
using Chaos.Wpf.Collections.ObjectModel;
using ChaosAssetManager.Helpers;
using ChaosAssetManager.ViewModel;
using SkiaSharp;
using Rectangle = Chaos.Geometry.Rectangle;

namespace ChaosAssetManager.Model;

public sealed class TileGrab : NotifyPropertyChangedBase, IDeltaUpdatable
{
    public required Rectangle Bounds { get; set; }
    public SKPoint? SelectionStart { get; set; }
    public ObservingCollection<TileViewModel> RawBackgroundTiles { get; } = [];
    public ObservingCollection<TileViewModel> RawLeftForegroundTiles { get; } = [];
    public ObservingCollection<TileViewModel> RawRightForegroundTiles { get; } = [];

    public ListSegment2D<TileViewModel> BackgroundTilesView => new(RawBackgroundTiles, Bounds.Width);

    public bool HasBackgroundTiles => RawBackgroundTiles.Count > 0;

    public bool HasForegroundTiles => HasLeftForegroundTiles || HasRightForegroundTiles;

    public bool HasLeftForegroundTiles => RawLeftForegroundTiles.Count > 0;

    public bool HasRightForegroundTiles => RawRightForegroundTiles.Count > 0;

    public ListSegment2D<TileViewModel> LeftForegroundTilesView => new(RawLeftForegroundTiles, Bounds.Width);

    public ListSegment2D<TileViewModel> RightForegroundTilesView => new(RawRightForegroundTiles, Bounds.Width);

    public TileGrab()
    {
        RawBackgroundTiles.CollectionChanged += (_, _) => OnPropertyChanged(nameof(RawBackgroundTiles));
        RawLeftForegroundTiles.CollectionChanged += (_, _) => OnPropertyChanged(nameof(RawLeftForegroundTiles));
        RawRightForegroundTiles.CollectionChanged += (_, _) => OnPropertyChanged(nameof(RawRightForegroundTiles));
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
}