using System.IO;
using System.Windows;
using ChaosAssetManager.Helpers;
using DALib.Abstractions;
using DALib.Drawing;
using SkiaSharp;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace ChaosAssetManager.Controls;

public partial class EditorControl
{
    private List<SKPoint>? CenterPoints;
    private ISavable? CurrentItem;
    private string? CurrentPath;

    public EditorControl() => InitializeComponent();

    private void Load_OnClick(object sender, RoutedEventArgs e)
    {
        (ContentPanel.Content as IDisposable)?.Dispose();
        ContentPanel.Content = null;
        CurrentPath = null;

        var fileDialog = new OpenFileDialog
        {
            Filter = "DA Graphics|*.efa;*.epf",
            InitialDirectory = PathHelper.Instance.EditorImageFromPath
        };

        if (fileDialog.ShowDialog() == false)
            return;

        if (string.IsNullOrEmpty(fileDialog.FileName) || (fileDialog.FileNames.Length > 1))
            return;

        CurrentPath = fileDialog.FileName;

        PathHelper.Instance.EditorImageFromPath = Path.GetDirectoryName(CurrentPath);
        PathHelper.Instance.Save();

        var extension = Path.GetExtension(CurrentPath)
                            .ToLower();

        switch (extension)
        {
            case ".efa":
            {
                var efaFile = EfaFile.FromFile(CurrentPath);
                CurrentItem = efaFile;

                ContentPanel.Content = new EfaEditor(efaFile);

                break;
            }
            case ".epf":
            {
                var epfFile = EpfFile.FromFile(CurrentPath);

                var paletteDialog = new OpenFileDialog
                {
                    Filter = "Palette|*.pal",
                    InitialDirectory = PathHelper.Instance.EditorPalFromPath
                };

                if (paletteDialog.ShowDialog() == false)
                    return;

                if (string.IsNullOrEmpty(paletteDialog.FileName) || (paletteDialog.FileNames.Length > 1))
                    return;

                PathHelper.Instance.EditorPalFromPath = Path.GetDirectoryName(paletteDialog.FileName);
                PathHelper.Instance.Save();

                var palette = Palette.FromFile(paletteDialog.FileName);
                var root = Path.GetDirectoryName(CurrentPath)!;
                var fileName = Path.GetFileNameWithoutExtension(CurrentPath);
                var tblPath = Path.Combine(root, $"{fileName}.tbl");
                List<SKPoint>? tblPoints = null;

                if (File.Exists(tblPath))
                {
                    tblPoints = new List<SKPoint>();

                    using var reader = new BinaryReader(File.OpenRead(tblPath));

                    while (reader.BaseStream.Position < reader.BaseStream.Length)
                    {
                        var x = reader.ReadInt16();
                        var y = reader.ReadInt16();
                        tblPoints.Add(new SKPoint(x, y));
                    }
                }

                CurrentItem = epfFile;
                CenterPoints = tblPoints;
                ContentPanel.Content = new EpfEditor(epfFile, palette, tblPoints);

                break;
            }
        }
    }

    private void Save_OnClick(object sender, RoutedEventArgs e)
    {
        var saveFileDialog = new SaveFileDialog
        {
            Filter = "DA Graphics|*.efa;*.epf",
            InitialDirectory = PathHelper.Instance.EditorImageToPath
        };

        if (saveFileDialog.ShowDialog() == false)
            return;

        PathHelper.Instance.EditorImageToPath = Path.GetDirectoryName(saveFileDialog.FileName);
        PathHelper.Instance.Save();

        CurrentItem?.Save(saveFileDialog.FileName);

        if (CenterPoints is not null)
        {
            var root = Path.GetDirectoryName(saveFileDialog.FileName)!;
            var fileName = Path.GetFileNameWithoutExtension(saveFileDialog.FileName);
            var tblPath = Path.Combine(root, $"{fileName}.tbl");

            using var writer = new BinaryWriter(File.Create(tblPath));

            foreach (var point in CenterPoints)
            {
                writer.Write((short)point.X);
                writer.Write((short)point.Y);
            }
        }
    }
}