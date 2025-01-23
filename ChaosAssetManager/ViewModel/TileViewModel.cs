using Chaos.Wpf.Abstractions;

namespace ChaosAssetManager.ViewModel;

public class TileViewModel : NotifyPropertyChangedBase
{
    public int TileId { get;
        set => SetField(ref field, value);
    }
    public TileType TileType { get;
        set => SetField(ref field, value);
    }
}

public enum TileType
{
    Background,
    Foreground
}