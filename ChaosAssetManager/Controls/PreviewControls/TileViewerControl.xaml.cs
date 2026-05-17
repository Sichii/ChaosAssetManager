using System.Windows.Controls;
using System.Windows.Data;
using ChaosAssetManager.Helpers;
using DALib.Data;
using DALib.Drawing;

namespace ChaosAssetManager.Controls.PreviewControls;

public sealed partial class TileViewerControl : IDisposable
{
    private readonly DataArchive Archive;
    private readonly DataArchiveEntry Entry;
    private readonly Tileset Tileset;

    public TileViewerControl(DataArchive archive, DataArchiveEntry entry)
    {
        Archive = archive;
        Entry = entry;

        InitializeComponent();

        Tileset = Tileset.FromArchive(Entry.EntryName, Archive);

        var collectionView = new CollectionView(Enumerable.Range(0, Tileset.Count));
        TileListView.ItemsSource = collectionView;
    }

    /// <inheritdoc />
    public void Dispose() => (TilePreview?.Content as IDisposable)?.Dispose();

    public int? SelectedTileIndex => TileListView.SelectedIndex >= 0 ? TileListView.SelectedIndex : null;

    private void TileListView_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        (TilePreview.Content as IDisposable)?.Dispose();
        TilePreview.Content = null;

        if (TileListView.SelectedItem is null)
            return;

        var animation = RenderUtil.TryRenderTile(Archive, Entry, TileListView.SelectedIndex);

        if (animation is null)
            return;

        TilePreview.Content = new EntryPreviewControl(animation);
    }
}