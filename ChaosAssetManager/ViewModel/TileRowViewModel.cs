using System.Collections;
using Chaos.Time.Abstractions;
using Chaos.Wpf.Abstractions;

namespace ChaosAssetManager.ViewModel;

public sealed class TileRowViewModel : NotifyPropertyChangedBase, IDeltaUpdatable, IEnumerable<TileViewModel>
{
    public TileViewModel? Tile1
    {
        get;
        set => SetField(ref field, value);
    }

    public TileViewModel? Tile2
    {
        get;
        set => SetField(ref field, value);
    }

    public TileViewModel? Tile3
    {
        get;
        set => SetField(ref field, value);
    }

    public TileViewModel? Tile4
    {
        get;
        set => SetField(ref field, value);
    }

    /// <inheritdoc />
    public IEnumerator<TileViewModel> GetEnumerator()
    {
        if (Tile1 is not null)
            yield return Tile1;

        if (Tile2 is not null)
            yield return Tile2;

        if (Tile3 is not null)
            yield return Tile3;

        if (Tile4 is not null)
            yield return Tile4;
    }

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <inheritdoc />
    public void Update(TimeSpan delta)
    {
        Tile1?.Update(delta);
        Tile2?.Update(delta);
        Tile3?.Update(delta);
        Tile4?.Update(delta);
    }

    public void Refresh()
    {
        Tile1?.Refresh();
        Tile2?.Refresh();
        Tile3?.Refresh();
        Tile4?.Refresh();
    }
}