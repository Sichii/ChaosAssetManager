using System.Windows.Controls;
using System.Windows.Data;
using Chaos.Extensions.Common;
using ChaosAssetManager.Model;
using DALib.Data;
using DALib.Drawing;
using DALib.Utility;
using Graphics = DALib.Drawing.Graphics;

namespace ChaosAssetManager.Controls;

public sealed partial class TileViewerControl
{
    private readonly DataArchive Archive;
    private readonly DataArchiveEntry Entry;
    private readonly PaletteLookup PaletteLookup;
    private readonly TileAnimationTable TileAnimationTable;
    private readonly Tileset Tileset;

    public TileViewerControl(DataArchive archive, DataArchiveEntry entry)
    {
        Archive = archive;
        Entry = entry;

        InitializeComponent();

        var palettePrefix = Entry.EntryName.EqualsI("tilea.bmp") ? "mpt" : "mps";

        Tileset = Tileset.FromArchive(Entry.EntryName, Archive);
        PaletteLookup = PaletteLookup.FromArchive(palettePrefix, Archive);
        TileAnimationTable = TileAnimationTable.FromArchive("gndani", Archive);

        var collectionView = new CollectionView(Enumerable.Range(0, Tileset.Count));
        TileListView.ItemsSource = collectionView;
    }

    private void TileListView_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        (TilePreview.Content as IDisposable)?.Dispose();
        TilePreview.Content = null;

        if (TileListView.SelectedItem is null)
            return;

        var tileIndex = TileListView.SelectedIndex;
        List<int> tileIndexes = [tileIndex];

        if (TileAnimationTable.TryGetEntry(tileIndex, out var animationEntry))
            tileIndexes = animationEntry.TileSequence
                                        .Select(i => (int)i)
                                        .ToList();

        var transformer = tileIndexes.Select(
            index =>
            {
                var tile = Tileset[index];
                var palette = PaletteLookup.GetPaletteForId(index + 1);
                var image = Graphics.RenderTile(tile, palette);

                return image;
            });

        var frames = new SKImageCollection(transformer);

        if (frames.IsNullOrEmpty())
            return;
        
        var animation = new Animation(frames, animationEntry?.AnimationIntervalMs);
        var content = new EntryPreviewControl(animation);

        TilePreview.Content = content;
    }
}