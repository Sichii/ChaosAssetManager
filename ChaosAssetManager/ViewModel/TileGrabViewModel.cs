using Chaos.Extensions.Geometry;
using Chaos.Geometry;
using Chaos.Time.Abstractions;
using Chaos.Wpf.Abstractions;
using Chaos.Wpf.Collections.ObjectModel;
using ChaosAssetManager.Definitions;
using ChaosAssetManager.Helpers;
using SkiaSharp;
using Point = Chaos.Geometry.Point;
using Rectangle = Chaos.Geometry.Rectangle;

namespace ChaosAssetManager.ViewModel;

public sealed class TileGrabViewModel : NotifyPropertyChangedBase, IDeltaUpdatable
{
    public required Rectangle Bounds { get; set; }
    public SKPoint? SelectionStart { get; set; }
    public ObservingCollection<TileViewModel> RawBackgroundTiles { get; } = [];
    public ObservingCollection<TileViewModel> RawLeftForegroundTiles { get; } = [];
    public ObservingCollection<TileViewModel> RawRightForegroundTiles { get; } = [];
    public bool IsEmpty => !HasBackgroundTiles && !HasForegroundTiles;

    public ListSegment2D<TileViewModel> BackgroundTilesView => new(RawBackgroundTiles, Bounds.Width);

    public bool HasBackgroundTiles => RawBackgroundTiles.Count > 0;

    public bool HasForegroundTiles => HasLeftForegroundTiles || HasRightForegroundTiles;

    public bool HasLeftForegroundTiles => RawLeftForegroundTiles.Count > 0;

    public bool HasRightForegroundTiles => RawRightForegroundTiles.Count > 0;

    public ListSegment2D<TileViewModel> LeftForegroundTilesView => new(RawLeftForegroundTiles, Bounds.Width);

    public ListSegment2D<TileViewModel> RightForegroundTilesView => new(RawRightForegroundTiles, Bounds.Width);

    public TileGrabViewModel()
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

    public void Apply(MapViewerViewModel viewer, LayerFlags layerFlags, SKPoint? tileCoordinatesOverride = null, bool overWrite = false)
    {
        var vmbgTiles = viewer.BackgroundTilesView;
        var vmlfgTiles = viewer.LeftForegroundTilesView;
        var vmrfgTiles = viewer.RightForegroundTilesView;
        var tgbgTiles = BackgroundTilesView;
        var tglfgTiles = LeftForegroundTilesView;
        var tgrfgTiles = RightForegroundTilesView;

        var mousePosition = viewer.Control!.Element.GetMousePoint()!;
        var tileCoordinates = viewer.Control.ConvertMouseToTileCoordinates(mousePosition.Value);

        if (tileCoordinatesOverride.HasValue)
            tileCoordinates = tileCoordinatesOverride.Value;

        var bounds = new ValueRectangle(
            (int)tileCoordinates.X,
            (int)tileCoordinates.Y,
            Bounds.Width,
            Bounds.Height);

        if (layerFlags.HasFlag(LayerFlags.Background) && HasBackgroundTiles)
            for (var y = bounds.Top; y <= bounds.Bottom; y++)
                for (var x = bounds.Left; x <= bounds.Right; x++)
                {
                    var point = new Point(x, y);

                    if (!viewer.Bounds.Contains(point))
                        continue;

                    var tileGrabX = x - bounds.Left;
                    var tileGrabY = y - bounds.Top;
                    
                    //if there are no background tiles for this tile, don't overwrite existing ones
                    if (!overWrite && (tgbgTiles[tileGrabX, tileGrabY].TileId == 0))
                        continue;

                    var tile = tgbgTiles[tileGrabX, tileGrabY]
                        .Clone();
                    tile.LayerFlags = LayerFlags.Background;
                    tile.Initialize();

                    vmbgTiles[x, y] = tile;
                }

        if (layerFlags.HasFlag(LayerFlags.LeftForeground) && HasLeftForegroundTiles)
            for (var y = bounds.Top; y <= bounds.Bottom; y++)
                for (var x = bounds.Left; x <= bounds.Right; x++)
                {
                    var point = new Point(x, y);

                    if (!viewer.Bounds.Contains(point))
                        continue;

                    var tileGrabX = x - bounds.Left;
                    var tileGrabY = y - bounds.Top;

                    //if there are no foreground tiles for this tile, don't overwrite existing ones
                    if (!overWrite && (tglfgTiles[tileGrabX, tileGrabY].TileId == 0))
                        continue;

                    var tile = tglfgTiles[tileGrabX, tileGrabY]
                        .Clone();
                    tile.LayerFlags = LayerFlags.LeftForeground;
                    tile.Initialize();

                    vmlfgTiles[x, y] = tile;
                }

        if (layerFlags.HasFlag(LayerFlags.RightForeground) && HasRightForegroundTiles)
            for (var y = bounds.Top; y <= bounds.Bottom; y++)
                for (var x = bounds.Left; x <= bounds.Right; x++)
                {
                    var point = new Point(x, y);

                    if (!viewer.Bounds.Contains(point))
                        continue;

                    var tileGrabX = x - bounds.Left;
                    var tileGrabY = y - bounds.Top;

                    //if there are no foreground tiles for this tile, don't overwrite existing ones
                    if (!overWrite && (tgrfgTiles[tileGrabX, tileGrabY].TileId == 0))
                        continue;
                    
                    var tile = tgrfgTiles[tileGrabX, tileGrabY]
                        .Clone();
                    tile.LayerFlags = LayerFlags.RightForeground;
                    tile.Initialize();

                    vmrfgTiles[x, y] = tile;
                }
    }

    public TileGrabViewModel Clone()
    {
        var ret = new TileGrabViewModel
        {
            SelectionStart = SelectionStart,
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

    public static TileGrabViewModel Create(
        MapViewerViewModel viewer,
        SKPoint tileCoordinates,
        int width,
        int height,
        LayerFlags layerFlags)
    {
        var tileX = (int)tileCoordinates.X;
        var tileY = (int)tileCoordinates.Y;
        width = Math.Min(width, viewer.Bounds.Width - tileX);
        height = Math.Min(height, viewer.Bounds.Height - tileY);

        var selectionBounds = new Rectangle(
            tileX,
            tileY,
            width,
            height);

        var ret = new TileGrabViewModel
        {
            SelectionStart = tileCoordinates,
            Bounds = selectionBounds
        };

        var vmbgTiles = viewer.BackgroundTilesView;
        var vmlfgTiles = viewer.LeftForegroundTilesView;
        var vmrfgTiles = viewer.RightForegroundTilesView;

        if (layerFlags.HasFlag(LayerFlags.Background))
            for (var y = tileY; y <= selectionBounds.Bottom; y++)
                for (var x = tileX; x <= selectionBounds.Right; x++)
                {
                    var point = new Point(x, y);

                    if (!viewer.Bounds.Contains(point))
                        continue;

                    var tile = vmbgTiles[x, y]
                        .Clone();
                    tile.Initialize();

                    ret.RawBackgroundTiles.Add(tile);
                }

        if (layerFlags.HasFlag(LayerFlags.LeftForeground))
            for (var y = tileY; y <= selectionBounds.Bottom; y++)
                for (var x = tileX; x <= selectionBounds.Right; x++)
                {
                    var point = new Point(x, y);

                    if (!viewer.Bounds.Contains(point))
                        continue;

                    var tile = vmlfgTiles[x, y]
                        .Clone();
                    tile.Initialize();

                    ret.RawLeftForegroundTiles.Add(tile);
                }

        if (layerFlags.HasFlag(LayerFlags.RightForeground))
            for (var y = tileY; y <= selectionBounds.Bottom; y++)
                for (var x = tileX; x <= selectionBounds.Right; x++)
                {
                    var point = new Point(x, y);

                    if (!viewer.Bounds.Contains(point))
                        continue;

                    var tile = vmrfgTiles[x, y]
                        .Clone();
                    tile.Initialize();

                    ret.RawRightForegroundTiles.Add(tile);
                }

        return ret;
    }

    public static TileGrabViewModel CreateFrom(
        MapViewerViewModel viewer,
        TileGrabViewModel other,
        LayerFlags layerFlags,
        SKPoint tileCoordinates)
    {
        if (other.SelectionStart is null)
            throw new InvalidOperationException();

        var ret = Create(
            viewer,
            tileCoordinates,
            other.Bounds.Width,
            other.Bounds.Height,
            layerFlags);

        ret.SelectionStart = other.SelectionStart;

        return ret;
    }

    public void Erase(MapViewerViewModel viewer, LayerFlags layerFlags)
    {
        var vmbgTiles = viewer.BackgroundTilesView;
        var vmlfgTiles = viewer.LeftForegroundTilesView;
        var vmrfgTiles = viewer.RightForegroundTilesView;

        for (var y = Bounds.Top; y <= Bounds.Bottom; y++)
        {
            for (var x = Bounds.Left; x <= Bounds.Right; x++)
            {
                if (HasBackgroundTiles && layerFlags.HasFlag(LayerFlags.Background))
                    vmbgTiles[x, y] = TileViewModel.EmptyBackground;

                if (HasLeftForegroundTiles && layerFlags.HasFlag(LayerFlags.LeftForeground))
                    vmlfgTiles[x, y] = TileViewModel.EmptyLeftForeground;

                if (HasRightForegroundTiles && layerFlags.HasFlag(LayerFlags.RightForeground))
                    vmrfgTiles[x, y] = TileViewModel.EmptyRightForeground;
            }
        }
    }

    public TileGrabViewModel WithTileCoordinates(SKPoint tileCoordinates)
    {
        var tileX = (int)tileCoordinates.X;
        var tileY = (int)tileCoordinates.Y;

        var selectionBounds = new Rectangle(
            tileX,
            tileY,
            Bounds.Width,
            Bounds.Height);

        var ret = new TileGrabViewModel
        {
            SelectionStart = tileCoordinates,
            Bounds = selectionBounds
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

    public StructureViewModel ToStructureViewModel()
    {
        var bgTiles = BackgroundTilesView;
        var lfgTiles = LeftForegroundTilesView;
        var rfgTiles = RightForegroundTilesView;

        //find bounding box of non-empty tiles
        var minX = Bounds.Width;
        var minY = Bounds.Height;
        var maxX = -1;
        var maxY = -1;

        for (var y = 0; y < Bounds.Height; y++)
        {
            for (var x = 0; x < Bounds.Width; x++)
            {
                var hasBg = HasBackgroundTiles && bgTiles[x, y].TileId != 0;
                var hasLfg = HasLeftForegroundTiles && lfgTiles[x, y].TileId != 0;
                var hasRfg = HasRightForegroundTiles && rfgTiles[x, y].TileId != 0;

                if (hasBg || hasLfg || hasRfg)
                {
                    minX = Math.Min(minX, x);
                    minY = Math.Min(minY, y);
                    maxX = Math.Max(maxX, x);
                    maxY = Math.Max(maxY, y);
                }
            }
        }

        //if no non-empty tiles found, return empty 1x1 structure
        if (maxX < 0)
        {
            return new StructureViewModel
            {
                Bounds = new Rectangle(0, 0, 1, 1)
            };
        }

        var trimmedWidth = maxX - minX + 1;
        var trimmedHeight = maxY - minY + 1;

        var ret = new StructureViewModel
        {
            Bounds = new Rectangle(0, 0, trimmedWidth, trimmedHeight)
        };

        //copy only the trimmed region
        for (var y = minY; y <= maxY; y++)
        {
            for (var x = minX; x <= maxX; x++)
            {
                if (HasBackgroundTiles)
                {
                    var cloned = bgTiles[x, y].Clone();
                    cloned.Initialize();
                    ret.RawBackgroundTiles.Add(cloned);
                }

                if (HasLeftForegroundTiles)
                {
                    var cloned = lfgTiles[x, y].Clone();
                    cloned.Initialize();
                    ret.RawLeftForegroundTiles.Add(cloned);
                }

                if (HasRightForegroundTiles)
                {
                    var cloned = rfgTiles[x, y].Clone();
                    cloned.Initialize();
                    ret.RawRightForegroundTiles.Add(cloned);
                }
            }
        }

        return ret;
    }

    public void Refresh()
    {
        foreach (var tile in RawBackgroundTiles)
            tile.Refresh();

        foreach (var tile in RawLeftForegroundTiles)
            tile.Refresh();

        foreach (var tile in RawRightForegroundTiles)
            tile.Refresh();
    }
}