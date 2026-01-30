using System.IO;
using System.Windows;
using System.Windows.Controls;
using ChaosAssetManager.Helpers;
using DALib.Data;
using DALib.Definitions;
using DALib.Drawing;
using ListViewItem = System.Windows.Controls.ListViewItem;

namespace ChaosAssetManager.Controls;

public partial class EquipmentEditorControl
{
    // Equipment type letters mapped to their descriptions
    public static readonly Dictionary<char, string> EquipmentTypes = new()
    {
        ['a'] = "Arms 1",
        ['b'] = "Body 1",
        ['c'] = "Accessories 1",
        ['e'] = "Head 1 (front)",
        ['f'] = "Head 3 (behind body)",
        ['g'] = "Accessories 2 (behind body)",
        ['h'] = "Head 2 (behind armor)",
        ['i'] = "Armor 2 (+1k)",
        ['j'] = "Arms 2",
        ['l'] = "Boots",
        ['m'] = "Body 2",
        ['n'] = "Pants (dyeable 0-15)",
        ['o'] = "Faces",
        ['p'] = "Weapons 2 (casting)",
        ['s'] = "Shields",
        ['u'] = "Armor 1",
        ['w'] = "Weapons 1 (attacks)"
    };

    // Animation suffixes and their descriptions
    public static readonly Dictionary<string, string> AnimationSuffixes = new(StringComparer.OrdinalIgnoreCase)
    {
        [""] = "Base",
        ["01"] = "Walk/Idle",
        ["02"] = "Assail",
        ["03"] = "Emote",
        ["04"] = "Idle Animation",
        ["b"] = "Priest",
        ["c"] = "Warrior",
        ["d"] = "Monk",
        ["e"] = "Rogue",
        ["f"] = "Wizard"
    };

    private int CurrentEntryId;
    private char CurrentTypeLetter;

    private Dictionary<string, EpfFile>? EquipmentFiles;
    private Palette? EquipmentPalette;

    public EquipmentEditorControl()
    {
        InitializeComponent();

        PathHelper.ArchivesPathChanged += () => EquipmentEditorControl_OnLoaded(this, new RoutedEventArgs());
    }

    private void Entry_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (EntryListView.SelectedItem is not ListViewItem { Tag: (int entryId, Dictionary<string, EpfFile> equipmentFiles) })
            return;

        CurrentEntryId = entryId;
        LoadEquipmentEntry(CurrentTypeLetter, entryId, equipmentFiles);
    }

    private void EquipmentEditorControl_OnLoaded(object sender, RoutedEventArgs e)
    {
        if (!PathHelper.ArchivePathIsValid(PathHelper.Instance.ArchivesPath))
        {
            MainContent.Visibility = Visibility.Collapsed;
            NotConfiguredMessage.Visibility = Visibility.Visible;

            return;
        }

        NotConfiguredMessage.Visibility = Visibility.Collapsed;
        MainContent.Visibility = Visibility.Visible;

        // Populate equipment type dropdown
        EquipmentTypeCmb.Items.Clear();

        foreach (var kvp in EquipmentTypes.OrderBy(k => k.Value))
            EquipmentTypeCmb.Items.Add(
                new ComboBoxItem
                {
                    Content = kvp.Value,
                    Tag = kvp.Key
                });
    }

    private void EquipmentType_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (EquipmentTypeCmb.SelectedItem is not ComboBoxItem { Tag: char typeLetter })
            return;

        CurrentTypeLetter = typeLetter;
        PopulateEntryList(typeLetter);
    }

    private void Gender_OnChecked(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
            return;

        // Refresh entry list when gender changes
        if (EquipmentTypeCmb.SelectedItem is ComboBoxItem { Tag: char typeLetter })
            PopulateEntryList(typeLetter);
    }

    private static DataArchive? GetArchiveForType(char typeLetter, bool male)
    {
        typeLetter = char.ToLower(typeLetter);

        return typeLetter switch
        {
            >= 'a' and <= 'd' => male ? ArchiveCache.KhanMad : ArchiveCache.KhanWad,
            >= 'e' and <= 'h' => male ? ArchiveCache.KhanMeh : ArchiveCache.KhanWeh,
            >= 'i' and <= 'm' => male ? ArchiveCache.KhanMim : ArchiveCache.KhanWim,
            >= 'n' and <= 's' => male ? ArchiveCache.KhanMns : ArchiveCache.KhanWns,
            >= 't' and <= 'z' => male ? ArchiveCache.KhanMtz : ArchiveCache.KhanWtz,
            _                 => null
        };
    }

    private static string? GetArchivePathForType(char typeLetter, bool male)
    {
        var root = PathHelper.Instance.ArchivesPath;

        if (string.IsNullOrEmpty(root))
            return null;

        typeLetter = char.ToLower(typeLetter);

        var archiveName = typeLetter switch
        {
            >= 'a' and <= 'd' => male ? "khanmad.dat" : "khanwad.dat",
            >= 'e' and <= 'h' => male ? "khanmeh.dat" : "khanweh.dat",
            >= 'i' and <= 'm' => male ? "khanmim.dat" : "khanwim.dat",
            >= 'n' and <= 's' => male ? "khanmns.dat" : "khanwns.dat",
            >= 't' and <= 'z' => male ? "khanmtz.dat" : "khanwtz.dat",
            _                 => null
        };

        return archiveName is null ? null : Path.Combine(root, archiveName);
    }

    private static Palette? GetPaletteForType(char typeLetter, int entryId, bool male)
    {
        typeLetter = char.ToLower(typeLetter);

        // Map equipment type to palette prefix
        var paletteLetter = typeLetter switch
        {
            'a' => 'b',
            'g' => 'c',
            'j' => 'c',
            'o' => 'm',
            's' => 'p',
            _   => typeLetter
        };

        // Special cases
        if (paletteLetter is 'm' or 'n')
        {
            // Body palettes and pants don't use PaletteLookup the same way
            var palettes = Palette.FromArchive($"pal{paletteLetter}", ArchiveCache.KhanPal);

            return palettes.TryGetValue(entryId, out var pal) ? pal : palettes.Values.FirstOrDefault();
        }

        var lookupPrefix = $"pal{paletteLetter}";

        try
        {
            var lookup = PaletteLookup.FromArchive(lookupPrefix, ArchiveCache.KhanPal);
            var overrideType = male ? KhanPalOverrideType.Male : KhanPalOverrideType.Female;

            return lookup.GetPaletteForId(entryId, overrideType);
        } catch
        {
            return null;
        }
    }

    private void LoadEquipmentEntry(char typeLetter, int entryId, Dictionary<string, EpfFile> equipmentFiles)
    {
        //dispose previous content
        (ContentPanel.Content as IDisposable)?.Dispose();
        ContentPanel.Content = null;
        EquipmentPalette = null;

        var male = MaleRadio.IsChecked == true;

        EquipmentFiles = equipmentFiles;

        //load palette
        EquipmentPalette = GetPaletteForType(typeLetter, entryId, male);

        if (EquipmentPalette is null)
        {
            var prefix = male ? $"m{typeLetter}" : $"w{typeLetter}";
            var baseEntryName = $"{prefix}{entryId:D3}";
            Snackbar.MessageQueue!.Enqueue($"Could not find palette for {baseEntryName}");

            return;
        }

        ContentPanel.Content = new EpfEquipmentEditorControl(
            EquipmentFiles,
            EquipmentPalette,
            typeLetter,
            male);
    }

    private void PopulateEntryList(char typeLetter)
    {
        EntryListView.Items.Clear();

        var male = MaleRadio.IsChecked == true;
        var archive = GetArchiveForType(typeLetter, male);

        if (archive is null)
            return;

        //find all unique entry IDs for this equipment type
        var prefix = male ? $"m{typeLetter}" : $"w{typeLetter}";
        var entryIds = new HashSet<int>();

        foreach (var entry in archive)
        {
            if (!entry.EntryName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                continue;

            if (!entry.EntryName.EndsWith(".epf", StringComparison.OrdinalIgnoreCase))
                continue;

            //extract numeric identifier (3 digits after prefix)
            if (entry.TryGetNumericIdentifier(out var id, 3))
                entryIds.Add(id);
        }

        //preload all entry files
        foreach (var id in entryIds.OrderBy(x => x))
        {
            var baseEntryName = $"{prefix}{id:D3}";
            var files = new Dictionary<string, EpfFile>(StringComparer.OrdinalIgnoreCase);

            foreach (var suffix in AnimationSuffixes.Keys)
            {
                var entryName = $"{baseEntryName}{suffix}.epf";

                if (archive.Contains(entryName))
                    files[suffix] = EpfFile.FromEntry(archive[entryName]);
            }

            if (files.Count > 0)
                EntryListView.Items.Add(
                    new ListViewItem
                    {
                        Content = id.ToString("D3"),
                        Tag = (id, files)
                    });
        }
    }

    private void Save_OnClick(object sender, RoutedEventArgs e)
    {
        if (EquipmentFiles is null || (EquipmentFiles.Count == 0))
        {
            Snackbar.MessageQueue!.Enqueue("No equipment loaded to save");

            return;
        }

        var male = MaleRadio.IsChecked == true;
        var archive = GetArchiveForType(CurrentTypeLetter, male);

        if (archive is null)
        {
            Snackbar.MessageQueue!.Enqueue("Could not find archive for equipment type");

            return;
        }

        var prefix = male ? $"m{CurrentTypeLetter}" : $"w{CurrentTypeLetter}";
        var baseEntryName = $"{prefix}{CurrentEntryId:D3}";

        foreach ((var suffix, var epfFile) in EquipmentFiles)
        {
            var entryName = $"{baseEntryName}{suffix}.epf";
            archive.Patch(entryName, epfFile);
        }

        // Save the archive
        var archivePath = GetArchivePathForType(CurrentTypeLetter, male);

        if (!string.IsNullOrEmpty(archivePath))
        {
            archive.Save(archivePath);
            Snackbar.MessageQueue!.Enqueue($"Saved {EquipmentFiles.Count} files to archive");
        }
    }
}