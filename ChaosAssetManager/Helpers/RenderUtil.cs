using System.IO;
using System.Text;
using DALib.Data;
using DALib.Definitions;
using DALib.Drawing;
using DALib.Utility;
using Graphics = DALib.Drawing.Graphics;

namespace ChaosAssetManager.Helpers;

public static class RenderUtil
{
    public static (SKImageCollection Frames, int FrameIntervalMs) RenderEfa(DataArchiveEntry entry)
    {
        var efaFile = EfaFile.FromEntry(entry);
        var transformer = efaFile.Select(frame => Graphics.RenderImage(frame));
        var collection = new SKImageCollection(transformer);

        return (collection, efaFile.FrameIntervalMs);
    }

    public static SKImageCollection RenderSpf(DataArchiveEntry entry)
    {
        var spfFile = SpfFile.FromEntry(entry);

        var transformer = spfFile.Select(
            frame => spfFile.Format == SpfFormatType.Colorized
                ? Graphics.RenderImage(frame)
                : Graphics.RenderImage(frame, spfFile.PrimaryColors!));
        var collection = new SKImageCollection(transformer);

        return collection;
    }

    public static string RenderText(DataArchiveEntry entry)
    {
        var builder = new StringBuilder();
        using var reader = new StreamReader(entry.ToStreamSegment());

        builder.Append(reader.ReadToEnd());

        return builder.ToString();
    }

    /*public static SKImage RenderEpf(DataArchive archive, DataArchiveEntry entry, string archiveName, string archiveRoot)
    {
        archiveName = archiveName.ToLower();

        switch (archiveName)
        {

        }
    }*/
}