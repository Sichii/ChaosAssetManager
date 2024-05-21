﻿using System.IO;
using System.Windows;
using System.Windows.Controls;
using Chaos.Extensions.Common;
using ChaosAssetManager.Helpers;
using DALib.Data;
using DALib.Drawing;
using Application = System.Windows.Application;
using Brushes = System.Windows.Media.Brushes;
using DataFormats = System.Windows.DataFormats;
using DragDropEffects = System.Windows.DragDropEffects;
using DragEventArgs = System.Windows.DragEventArgs;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using FolderBrowserDialog = System.Windows.Forms.FolderBrowserDialog;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace ChaosAssetManager.Controls;

public sealed partial class ArchivesControl
{
    private AnimatedPreview? AnimatedPreview;
    private string ArchiveName = string.Empty;
    private string ArchiveRoot = string.Empty;
    public DataArchive? Archive { get; set; }

    public ArchivesControl() => InitializeComponent();

    #region Events
    private void ArchivesControl_OnDragEnter(object sender, DragEventArgs e)
        => e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;

    private void ArchivesControl_OnDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop)!;

            if (files.Count(file => file.EndsWithI(".dat")) > 1)
            {
                ShowMessage("Please only drop one archive at a time!");

                return;
            }

            var archivePath = files.FirstOrDefault(file => file.EndsWithI(".dat"));

            if (archivePath is not null)
            {
                LoadArchive(archivePath);
                ShowMessage("Archive loaded successfully!");

                return;
            }

            if (Archive is null)
            {
                ShowMessage("Please load an archive first!");

                return;
            }

            var patched = false;

            ArchivesView.ItemsSource = null;

            foreach (var file in files)
            {
                PatchArchive(file);
                patched = true;
            }

            ArchivesView.ItemsSource = Archive;
            ArchivesView.SelectedItems.Clear();

            foreach (var fileName in files)
            {
                var entryName = Path.GetFileName(fileName);
                var entry = Archive![entryName];
                ArchivesView.SelectedItems.Add(entry);
            }

            if (patched)
                ShowMessage("Archive patched successfully!");
        }
    }

    private void ArchivesView_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        (Preview.Content as IDisposable)?.Dispose();
        AnimatedPreview?.Dispose();
        Preview.Content = null;

        var numSelectedItems = ArchivesView.SelectedItems.Count;

        ExtractSelectionBtn.IsEnabled = numSelectedItems != 0;

        if (numSelectedItems == 1)
        {
            var selectedEntry = (DataArchiveEntry)ArchivesView.SelectedItem!;
            GeneratePreview(selectedEntry);
        }
    }

    private void CompileBtn_OnClick(object sender, RoutedEventArgs e)
    {
        using var folderBrowserDialog = new FolderBrowserDialog();

        if (folderBrowserDialog.ShowDialog() == DialogResult.OK)
        {
            var saveFileDialog = new SaveFileDialog
            {
                Filter = "Data Archive (*.dat)|*.dat"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                DataArchive.Compile(folderBrowserDialog.SelectedPath, saveFileDialog.FileName);

                ShowMessage("Archive compiled successfully!");
            }
        }
    }

    private void CompileToBtn_OnClick(object sender, RoutedEventArgs e)
    {
        var saveFileDialog = new SaveFileDialog
        {
            Filter = "Data Archive (*.dat)|*.dat"
        };

        if (saveFileDialog.ShowDialog() == true)
        {
            Archive?.Save(saveFileDialog.FileName);

            ShowMessage("Archive compiled successfully!");
        }
    }

    private void ExtractBtn_OnClick(object sender, RoutedEventArgs e)
    {
        var saveFileDialog = new SaveFileDialog
        {
            Filter = "Data Archive (*.dat)|*.dat"
        };

        if (saveFileDialog.ShowDialog() == true)
        {
            using var folderBrowserDialog = new FolderBrowserDialog();

            if (folderBrowserDialog.ShowDialog() == DialogResult.OK)
            {
                using var archive = DataArchive.FromFile(saveFileDialog.FileName);
                archive.ExtractTo(folderBrowserDialog.SelectedPath);

                ShowMessage("Archive extracted successfully!");
            }
        }
    }

    private void ExtractSelectionBtn_OnClick(object sender, RoutedEventArgs e)
    {
        using var folderBrowserDialog = new FolderBrowserDialog();

        if (folderBrowserDialog.ShowDialog() == DialogResult.OK)
            foreach (DataArchiveEntry entry in ArchivesView.SelectedItems)
            {
                var outPath = Path.Combine(folderBrowserDialog.SelectedPath, entry.EntryName);
                using var outStream = File.Create(outPath);
                using var entrySegment = entry.ToStreamSegment();

                entrySegment.CopyTo(outStream);

                ShowMessage("Entries extracted successfully!");
            }
    }

    private void ExtractToBtn_OnClick(object sender, RoutedEventArgs e)
    {
        using var folderBrowserDialog = new FolderBrowserDialog();

        if (folderBrowserDialog.ShowDialog() == DialogResult.OK)
        {
            Archive?.ExtractTo(folderBrowserDialog.SelectedPath);

            ShowMessage("Archive extracted successfully!");
        }
    }

    private void Load_OnClick(object sender, RoutedEventArgs e)
    {
        var openFileDialog = new OpenFileDialog
        {
            Filter = "Data Archive (*.dat)|*.dat"
        };

        if (openFileDialog.ShowDialog() == true)
        {
            LoadArchive(openFileDialog.FileName);
            ShowMessage("Archive loaded successfully!");
        }
    }

    private void PatchBtn_OnClick(object sender, RoutedEventArgs e)
    {
        var openFileDialog = new OpenFileDialog
        {
            Filter = "All Files (*.*)|*.*",
            Multiselect = true
        };

        if (openFileDialog.ShowDialog() == true)
        {
            ArchivesView.ItemsSource = null;

            foreach (var file in openFileDialog.FileNames)
                PatchArchive(file);

            ArchivesView.ItemsSource = Archive;

            ArchivesView.SelectedItems.Clear();

            foreach (var fileName in openFileDialog.FileNames)
            {
                var entry = Archive![fileName];
                ArchivesView.SelectedItems.Add(entry);
            }

            ShowMessage("Archive patched successfully!");
        }
    }
    #endregion

    #region Functionality
    private void ShowMessage(string message, TimeSpan? time = null)
        => Snackbar.MessageQueue?.Enqueue(
            message,
            null,
            null,
            null,
            false,
            true,
            time ?? TimeSpan.FromMilliseconds(500));

    private void LoadArchive(string path)
    {
        Archive?.Dispose();

        try
        {
            Archive = DataArchive.FromFile(path, false);
        } catch
        {
            Archive = DataArchive.FromFile(path, false, true);
        }

        ArchiveName = Path.GetFileName(path);
        ArchiveRoot = Path.GetDirectoryName(path) ?? string.Empty;
        ArchivesView.ItemsSource = Archive;
        CompileToBtn.IsEnabled = true;
        ExtractToBtn.IsEnabled = true;
        PatchBtn.IsEnabled = true;
    }

    private void PatchArchive(string path)
    {
        var entryName = Path.GetFileName(path);

        using var entry = new PatchEntry(File.OpenRead(path));

        Archive!.Patch(entryName, entry);
    }

    private void GeneratePreview(DataArchiveEntry selectedEntry)
    {
        var type = Path.GetExtension(selectedEntry.EntryName)
                       .ToLower();

        switch (type)
        {
            case ".tbl":
            case ".txt":
            {
                var text = RenderUtil.RenderText(selectedEntry);

                var textBox = new TextBlock
                {
                    Text = text,
                    TextWrapping = TextWrapping.Wrap,
                    Style = Application.Current.Resources["MaterialDesignTextBlock"] as Style,
                    Foreground = Brushes.White,
                    Padding = new Thickness(10),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    Margin = new Thickness(0)
                };

                Preview.Content = textBox;

                break;
            }
            case ".epf":
            {
                var epfFile = EpfFile.FromEntry(selectedEntry);

                //var transformer = epfFile.Select(frame => Graphics.RenderImage(frame))

                break;
            }
            case ".efa":
            {
                var info = RenderUtil.RenderEfa(selectedEntry);

                AnimatedPreview = new AnimatedPreview(info.Frames, info.FrameIntervalMs);
                Preview.Content = AnimatedPreview.Element;

                break;
            }
            case ".hpf":
            {
                break;
            }
            case ".mpf":
            {
                break;
            }
            case ".spf":
            {
                var frames = RenderUtil.RenderSpf(selectedEntry);

                AnimatedPreview = new AnimatedPreview(frames, 100);
                Preview.Content = AnimatedPreview.Element;

                break;
            }
        }
    }
    #endregion
}