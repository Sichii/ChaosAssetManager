using Chaos.Wpf.Abstractions;
using Chaos.Wpf.Collections.ObjectModel;
using DALib.Data;

namespace ChaosAssetManager.ViewModel;

public sealed class MetaFileViewModel : NotifyPropertyChangedBase
{
    public ObservingCollection<MetaFileEntryViewModel> Entries { get; }

    public MetaFileViewModel(MetaFile file)
    {
        Entries = new ObservingCollection<MetaFileEntryViewModel>();

        Entries.CollectionChanged += (_, _) => OnPropertyChanged(nameof(Entries));

        foreach (var entry in file)
            Entries.Add(new MetaFileEntryViewModel(entry));
    }

    public void AddEntry() => Entries.Add(new MetaFileEntryViewModel(new MetaFileEntry("")));

    public void RemoveEntry(MetaFileEntryViewModel entry) => Entries.Remove(entry);
}