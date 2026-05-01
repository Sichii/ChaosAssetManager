using System.IO;
using System.Windows;
using System.Windows.Controls;
using Chaos.Extensions.Common;
using ChaosAssetManager.Helpers;
using DALib.Data;
using DALib.Drawing;
using SkiaSharp;
using ListViewItem = System.Windows.Controls.ListViewItem;

namespace ChaosAssetManager.Controls;

public sealed partial class EffectEditorControl
{
    private static PaletteLookup? EfctPaletteLookup;
    private static PaletteLookup? MefcPaletteLookup;
    private string? CurrentEntryName;

    public EffectEditorControl()
    {
        InitializeComponent();

        PathHelper.ArchivesPathChanged += () => EffectEditorControl_OnLoaded(this, new RoutedEventArgs());
    }

    private void Effect_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (EffectListView.SelectedItem is not ListViewItem listViewItem)
            return;

        //first time loading - tag is DataArchiveEntry, convert to file object
        if (listViewItem.Tag is DataArchiveEntry entry)
        {
            var archive = ArchiveCache.Roh;

            if (entry.EntryName.EndsWithI(".efa"))
            {
                var efaFile = EfaFile.FromEntry(entry);
                listViewItem.Tag = efaFile;
            } else if (entry.EntryName.EndsWithI(".epf"))
            {
                var epfFile = EpfFile.FromEntry(entry);
                var paletteInfo = GetPaletteForEffect(entry, archive);

                if (paletteInfo is null)
                {
                    Snackbar.MessageQueue!.Enqueue($"Could not find palette for {entry.EntryName}");

                    return;
                }

                var centerPoints = LoadCenterPoints(entry.EntryName, archive, epfFile.Count);
                listViewItem.Tag = (epfFile, paletteInfo.Value.Palette, paletteInfo.Value.AlphaType, centerPoints);
            }
        }

        if (listViewItem is { Tag: EfaFile efa, Content: string efaEntryName })
            LoadEffect(efaEntryName, efa);
        else if (listViewItem is { Tag: (EpfFile epf, Palette pal, SKAlphaType alphaType, List<SKPoint> centerPoints), Content: string epfEntryName })
            LoadEffect(epfEntryName, epf, pal, alphaType, centerPoints);
    }

    private void EffectEditorControl_OnLoaded(object sender, RoutedEventArgs e)
    {
        if (!PathHelper.ArchivePathIsValid(PathHelper.Instance.ArchivesPath))
        {
            MainContent.Visibility = Visibility.Collapsed;
            NotConfiguredMessage.Visibility = Visibility.Visible;

            return;
        }

        NotConfiguredMessage.Visibility = Visibility.Collapsed;
        MainContent.Visibility = Visibility.Visible;

        PopulateEffectList();
    }

    private void EffectEditorControl_OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible && PathHelper.ArchivePathIsValid(PathHelper.Instance.ArchivesPath))
            PopulateEffectList();
    }

    private static (Palette Palette, SKAlphaType AlphaType)? GetPaletteForEffect(DataArchiveEntry entry, DataArchive archive)
    {
        if (!entry.TryGetNumericIdentifier(out var id))
            return null;

        if (entry.EntryName.StartsWithI("efct"))
        {
            EfctPaletteLookup ??= PaletteLookup.FromArchive("effpal", "eff", archive)
                                               .Freeze();

            return EfctPaletteLookup.GetPaletteAndAlphaType(id);
        }

        if (entry.EntryName.StartsWithI("mefc"))
        {
            MefcPaletteLookup ??= PaletteLookup.FromArchive("mefcpal", "mefc", archive)
                                               .Freeze();

            return MefcPaletteLookup.GetPaletteAndAlphaType(id);
        }

        // Fallback for other EPF files - try efct palette
        EfctPaletteLookup ??= PaletteLookup.FromArchive("effpal", "eff", archive)
                                           .Freeze();

        return EfctPaletteLookup.GetPaletteAndAlphaType(0);
    }

    private static List<SKPoint> LoadCenterPoints(string entryName, DataArchive archive, int frameCount)
    {
        //fallback used when the tbl is missing or has no entry for a given frame
        var defaultCenter = new SKPoint(28, 70);

        var tblName = Path.ChangeExtension(entryName, ".tbl");

        if (archive.TryGetValue(tblName, out var tblEntry))
        {
            var points = new List<SKPoint>();

            using var stream = tblEntry.ToStreamSegment();
            using var reader = new BinaryReader(stream);

            while (stream.Position < stream.Length)
            {
                var x = reader.ReadInt16();
                var y = reader.ReadInt16();
                points.Add(new SKPoint(x, y));
            }

            //pad missing frames with the default centerpoint
            while (points.Count < frameCount)
                points.Add(defaultCenter);

            return points;
        }

        //no tbl file - all frames use the default centerpoint
        return Enumerable.Repeat(defaultCenter, frameCount)
                         .ToList();
    }

    private void LoadEffect(string entryName, EfaFile efaFile)
    {
        //dispose previous content
        (ContentPanel.Content as IDisposable)?.Dispose();
        ContentPanel.Content = null;
        CurrentEntryName = entryName;

        ContentPanel.Content = new EffectContentEditorControl(efaFile);
    }


    private void LoadEffect(string entryName, EpfFile epfFile, Palette palette, SKAlphaType alphaType, List<SKPoint> centerPoints)
    {
        //dispose previous content
        (ContentPanel.Content as IDisposable)?.Dispose();
        ContentPanel.Content = null;
        CurrentEntryName = entryName;

        ContentPanel.Content = new EffectContentEditorControl(epfFile, palette, alphaType, centerPoints);
    }

    private void PopulateEffectList()
    {
        EffectListView.Items.Clear();

        var archive = ArchiveCache.Roh;

        //find all EPF and EFA entries, sort with mefc at end, then numerically
        var effectEntries = archive.Where(e => e.EntryName.EndsWithI(".epf") || e.EntryName.EndsWithI(".efa"))
                                   .OrderBy(e => e.EntryName.StartsWithI("mefc") ? 1 : 0)
                                   .ThenBy(e => e.TryGetNumericIdentifier(out var num) ? num : int.MaxValue)
                                   .ToList();

        foreach (var entry in effectEntries)
            EffectListView.Items.Add(
                new ListViewItem
                {
                    Content = entry.EntryName,
                    Tag = entry
                });
    }

    private void Save_OnClick(object sender, RoutedEventArgs e)
    {
        if (ContentPanel.Content is not EffectContentEditorControl editor)
        {
            Snackbar.MessageQueue!.Enqueue("No effect loaded to save");

            return;
        }

        if (string.IsNullOrEmpty(CurrentEntryName))
        {
            Snackbar.MessageQueue!.Enqueue("No effect selected");

            return;
        }

        var archive = ArchiveCache.Roh;

        if (editor.IsEfa)

            // Save EFA file
            archive.Patch(CurrentEntryName, editor.EfaFile!);
        else
        {
            // Save EPF file
            archive.Patch(CurrentEntryName, editor.EpfFile!);

            // Save TBL with center points
            var tblName = Path.ChangeExtension(CurrentEntryName, ".tbl");

            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);

            foreach (var pt in editor.CenterPoints!)
            {
                writer.Write((short)pt.X);
                writer.Write((short)pt.Y);
            }

            ms.Seek(0, SeekOrigin.Begin);
            archive.Patch(tblName, new PatchEntry(ms));
        }

        // Save archive to disk
        var archivePath = Path.Combine(PathHelper.Instance.ArchivesPath!, "roh.dat");
        archive.Save(archivePath);

        Snackbar.MessageQueue!.Enqueue($"Saved {CurrentEntryName}");
    }
}