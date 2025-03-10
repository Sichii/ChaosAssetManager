using System.Collections.ObjectModel;
using DALib.Data;

namespace ChaosAssetManager.Model;

public sealed class EntryGrouping : IEquatable<EntryGrouping>
{
    public ObservableCollection<DataArchiveEntry> Entries { get; }
    public string Extension { get; }
    public bool IsExpanded { get; set; }

    public EntryGrouping(string extension, IEnumerable<DataArchiveEntry> entries)
    {
        Extension = extension;
        Entries = new ObservableCollection<DataArchiveEntry>(entries);
    }

    /// <inheritdoc />
    public bool Equals(EntryGrouping? other)
    {
        if (other is null)
            return false;

        if (ReferenceEquals(this, other))
            return true;

        return string.Equals(Extension, other.Extension, StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => ReferenceEquals(this, obj) || (obj is EntryGrouping other && Equals(other));

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Extension);

    public static bool operator ==(EntryGrouping? left, EntryGrouping? right) => Equals(left, right);
    public static bool operator !=(EntryGrouping? left, EntryGrouping? right) => !Equals(left, right);
}