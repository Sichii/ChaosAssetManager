using System.Collections.ObjectModel;
using Chaos.Wpf.Abstractions;
using ChaosAssetManager.Definitions;
using ChaosAssetManager.Model;

namespace ChaosAssetManager.ViewModel;

public class HeaEditorViewModel : NotifyPropertyChangedBase
{
    public HeaToolType SelectedTool
    {
        get;
        set => SetField(ref field, value);
    } = HeaToolType.Draw;

    public HeaBrushShape SelectedBrushShape
    {
        get;
        set => SetField(ref field, value);
    } = HeaBrushShape.Circle;

    public byte BrushIntensity
    {
        get;
        set => SetField(ref field, value);
    } = 0x20;

    public int BrushRadius
    {
        get;
        set => SetField(ref field, value);
    } = 31;

    public float BrushRotation
    {
        get;
        set => SetField(ref field, value);
    }

    public LightBrush? SelectedPrefabBrush
    {
        get;
        set => SetField(ref field, value);
    }

    public byte DarknessOpacity
    {
        get;
        set => SetField(ref field, value);
    } = 128;

    public string StatusText
    {
        get;
        set => SetField(ref field, value);
    } = string.Empty;

    public string MousePositionText
    {
        get;
        set => SetField(ref field, value);
    } = string.Empty;

    public bool IsMapLoaded
    {
        get;
        set => SetField(ref field, value);
    }

    public bool IsDimensionsSelectorEnabled
    {
        get;
        set => SetField(ref field, value);
    }

    public int LoadedMapId
    {
        get;
        set => SetField(ref field, value);
    } = -1;

    public int LoadedMapWidth
    {
        get;
        set => SetField(ref field, value);
    }

    public int LoadedMapHeight
    {
        get;
        set => SetField(ref field, value);
    }

    public ObservableCollection<MapBounds> PossibleBounds
    {
        get;
        set => SetField(ref field, value);
    } = [];

    public MapBounds? SelectedBounds
    {
        get;
        set => SetField(ref field, value);
    }
}
