using Chaos.Wpf.Abstractions;
using ChaosAssetManager.Helpers;

namespace ChaosAssetManager.ViewModel;

public class OptionsViewModel : NotifyPropertyChangedBase
{
    public string? ArchivesPath
    {
        get;
        set => SetField(ref field, value);
    }

    public OptionsViewModel() => ArchivesPath = PathHelper.Instance.ArchivesPath;

    public void Save()
    {
        PathHelper.Instance.ArchivesPath = ArchivesPath;
        PathHelper.Instance.Save();
    }
}