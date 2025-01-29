using Chaos.Extensions.Geometry;
using Chaos.Geometry;
using Chaos.Time.Abstractions;
using Chaos.Wpf.Abstractions;
using Chaos.Wpf.Collections.ObjectModel;
using ChaosAssetManager.Definitions;
using ChaosAssetManager.Helpers;
using ChaosAssetManager.ViewModel;
using SkiaSharp;
using Point = Chaos.Geometry.Point;
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

    public void Apply(MapViewerViewModel viewModel, LayerFlags layerFlags, SKPoint? tileCoordinatesOverride = null)
    {
        var vmbgTiles = viewModel.BackgroundTilesView;
        var vmlfgTiles = viewModel.LeftForegroundTilesView;
        var vmrfgTiles = viewModel.RightForegroundTilesView;
        var tgbgTiles = BackgroundTilesView;
        var tglfgTiles = LeftForegroundTilesView;
        var tgrfgTiles = RightForegroundTilesView;

        var mousePosition = viewModel.Control!.Element.GetMousePoint()!;
        var tileCoordinates = viewModel.Control.ConvertMouseToTileCoordinates(mousePosition.Value);

        if (tileCoordinatesOverride.HasValue)
            tileCoordinates = tileCoordinatesOverride.Value;

        var bounds = new ValueRectangle(
            (int)tileCoordinates.X,
            (int)tileCoordinates.Y,
            Bounds.Width,
            Bounds.Height);

        if (layerFlags.HasFlag(LayerFlags.Background))
            for (var y = bounds.Top; y <= bounds.Bottom; y++)
                for (var x = bounds.Left; x <= bounds.Right; x++)
                {
                    var point = new Point(x, y);

                    if (!viewModel.Bounds.Contains(point))
                        continue;

                    var tileGrabX = x - bounds.Left;
                    var tileGrabY = y - bounds.Top;

                    var tile = tgbgTiles[tileGrabX, tileGrabY]
                        .Clone();
                    tile.LayerFlags = LayerFlags.Background;
                    tile.Initialize();

                    vmbgTiles[x, y] = tile;
                }

        if (layerFlags.HasFlag(LayerFlags.LeftForeground))
            for (var y = bounds.Top; y <= bounds.Bottom; y++)
                for (var x = bounds.Left; x <= bounds.Right; x++)
                {
                    var point = new Point(x, y);

                    if (!viewModel.Bounds.Contains(point))
                        continue;

                    var tileGrabX = x - bounds.Left;
                    var tileGrabY = y - bounds.Top;

                    var tile = tglfgTiles[tileGrabX, tileGrabY]
                        .Clone();
                    tile.LayerFlags = LayerFlags.LeftForeground;
                    tile.Initialize();

                    vmlfgTiles[x, y] = tile;
                }

        if (layerFlags.HasFlag(LayerFlags.RightForeground))
            for (var y = bounds.Top; y <= bounds.Bottom; y++)
                for (var x = bounds.Left; x <= bounds.Right; x++)
                {
                    var point = new Point(x, y);

                    if (!viewModel.Bounds.Contains(point))
                        continue;

                    var tileGrabX = x - bounds.Left;
                    var tileGrabY = y - bounds.Top;

                    var tile = tgrfgTiles[tileGrabX, tileGrabY]
                        .Clone();
                    tile.LayerFlags = LayerFlags.RightForeground;
                    tile.Initialize();

                    vmrfgTiles[x, y] = tile;
                }
    }

    public static TileGrab Create(
        MapViewerViewModel viewModel,
        SKPoint tileCoordinates,
        int width,
        int height,
        LayerFlags layerFlags)
    {
        var tileX = (int)tileCoordinates.X;
        var tileY = (int)tileCoordinates.Y;
        width = Math.Min(width, viewModel.Bounds.Width - tileX);
        height = Math.Min(height, viewModel.Bounds.Height - tileY);

        var selectionBounds = new Rectangle(
            tileX,
            tileY,
            width,
            height);

        var ret = new TileGrab
        {
            SelectionStart = tileCoordinates,
            Bounds = selectionBounds
        };

        var vmbgTiles = viewModel.BackgroundTilesView;
        var vmlfgTiles = viewModel.LeftForegroundTilesView;
        var vmrfgTiles = viewModel.RightForegroundTilesView;

        if (layerFlags.HasFlag(LayerFlags.Background))
            for (var y = tileY; y <= selectionBounds.Bottom; y++)
                for (var x = tileX; x <= selectionBounds.Right; x++)
                {
                    var point = new Point(x, y);

                    if (!viewModel.Bounds.Contains(point))
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

                    if (!viewModel.Bounds.Contains(point))
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

                    if (!viewModel.Bounds.Contains(point))
                        continue;

                    var tile = vmrfgTiles[x, y]
                        .Clone();
                    tile.Initialize();

                    ret.RawRightForegroundTiles.Add(tile);
                }

        return ret;
    }

    public static TileGrab CreateFrom(
        MapViewerViewModel viewModel,
        TileGrab other,
        LayerFlags layerFlags,
        SKPoint tileCoordinates)
    {
        if (other.SelectionStart is null)
            throw new InvalidOperationException();

        var ret = Create(
            viewModel,
            tileCoordinates,
            other.Bounds.Width,
            other.Bounds.Height,
            layerFlags);

        ret.SelectionStart = other.SelectionStart;

        return ret;
    }

    public void Erase(MapViewerViewModel viewModel, LayerFlags layerFlags)
    {
        var vmbgTiles = viewModel.BackgroundTilesView;
        var vmlfgTiles = viewModel.LeftForegroundTilesView;
        var vmrfgTiles = viewModel.RightForegroundTilesView;

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

    public TileGrab WithTileCoordinates(SKPoint tileCoordinates)
    {
        var tileX = (int)tileCoordinates.X;
        var tileY = (int)tileCoordinates.Y;

        var selectionBounds = new Rectangle(
            tileX,
            tileY,
            Bounds.Width,
            Bounds.Height);

        var ret = new TileGrab
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
}