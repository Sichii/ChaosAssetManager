using Chaos.Wpf.Abstractions;
using Chaos.Wpf.Collections.ObjectModel;
using Chaos.Wpf.Observables;
using DALib.Data;

namespace ChaosAssetManager.ViewModel;

public sealed class MetaFileEntryViewModel : NotifyPropertyChangedBase
{
    private bool _isExpanded;
    private BindableString _key;

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetField(ref _isExpanded, value);
    }

    public BindableString Key
    {
        get => _key;
        set => SetField(ref _key, value);
    }

    public ObservingCollection<BindableString> Properties { get; }

    public MetaFileEntryViewModel(MetaFileEntry entry)
    {
        _key = entry.Key;
        Properties = new ObservingCollection<BindableString>();
        Properties.CollectionChanged += (_, _) => OnPropertyChanged(nameof(Properties));

        foreach (var property in entry.Properties)
            Properties.Add(property);
    }
}