using Chaos.Time.Abstractions;
using Chaos.Wpf.Abstractions;
using Chaos.Wpf.Collections.ObjectModel;
using ChaosAssetManager.Definitions;
using ChaosAssetManager.Helpers;
using Rectangle = Chaos.Geometry.Rectangle;

namespace ChaosAssetManager.ViewModel;

public class StructureViewModel : NotifyPropertyChangedBase, IDeltaUpdatable
{
    public required Rectangle Bounds { get; set; }
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

    public StructureViewModel()
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

    public static StructureViewModel Create(int[,]? bgData = null, int[,]? lfgData = null, int[,]? rfgData = null)
    {
        var width = bgData?.GetLength(0) ?? lfgData?.GetLength(0) ?? rfgData?.GetLength(0) ?? 0;
        var height = bgData?.GetLength(1) ?? lfgData?.GetLength(1) ?? rfgData?.GetLength(1) ?? 0;

        var ret = new StructureViewModel
        {
            Bounds = new Rectangle(
                0,
                0,
                width,
                height)
        };

        if (bgData is not null)
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var tile = new TileViewModel
                    {
                        TileId = bgData[x, y],
                        LayerFlags = LayerFlags.Background
                    };

                    ret.RawBackgroundTiles.Add(tile);
                }
            }

        if (lfgData is not null)
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var tile = new TileViewModel
                    {
                        TileId = lfgData[x, y],
                        LayerFlags = LayerFlags.LeftForeground
                    };

                    ret.RawLeftForegroundTiles.Add(tile);
                }
            }

        if (rfgData is not null)
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var tile = new TileViewModel
                    {
                        TileId = rfgData[x, y],
                        LayerFlags = LayerFlags.RightForeground
                    };

                    ret.RawRightForegroundTiles.Add(tile);
                }
            }

        return ret;
    }

    public void Initialize()
    {
        foreach (var tile in RawBackgroundTiles)
            tile.Initialize();

        foreach (var tile in RawLeftForegroundTiles)
            tile.Initialize();

        foreach (var tile in RawRightForegroundTiles)
            tile.Initialize();
    }

    public TileGrabViewModel ToTileGrab()
    {
        var ret = new TileGrabViewModel
        {
            Bounds = Bounds
        };

        foreach (var tile in RawBackgroundTiles)
        {
            var cloned = tile.Clone();
            cloned.Initialize();

            ret.RawBackgroundTiles.Add(cloned);
        }

        foreach (var tile in RawLeftForegroundTiles)
        {
            var cloned = tile.Clone();
            cloned.Initialize();

            ret.RawLeftForegroundTiles.Add(cloned);
        }

        foreach (var tile in RawRightForegroundTiles)
        {
            var cloned = tile.Clone();
            cloned.Initialize();

            ret.RawRightForegroundTiles.Add(cloned);
        }

        return ret;
    }
}