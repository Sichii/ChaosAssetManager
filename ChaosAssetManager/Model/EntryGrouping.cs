using System.Collections.ObjectModel;
using DALib.Data;

namespace ChaosAssetManager.Model;

public sealed class EntryGrouping
{
    public ObservableCollection<DataArchiveEntry> Entries { get; }
    public string Extension { get; }

    public EntryGrouping(string extension, IEnumerable<DataArchiveEntry> entries)
    {
        Extension = extension;
        Entries = new ObservableCollection<DataArchiveEntry>(entries);
    }
}