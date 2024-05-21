using System.IO;
using System.Text;
using DALib.Data;
using DALib.Drawing;
using DALib.Utility;
using Graphics = DALib.Drawing.Graphics;

namespace ChaosAssetManager.Helpers;

public static class RenderUtil
{
    public static string RenderTable(DataArchiveEntry entry)
    {
        var builder = new StringBuilder();
        using var reader = new StreamReader(entry.ToStreamSegment());

        builder.Append(reader.ReadToEnd());

        return builder.ToString();
    }

    public static (SKImageCollection Frames, int FrameIntervalMs) RenderEfa(DataArchiveEntry entry)
    {
        var efaFile = EfaFile.FromEntry(entry);
        var transformer = efaFile.Select(frame => Graphics.RenderImage(frame));
        var collection = new SKImageCollection(transformer);

        return (collection, efaFile.FrameIntervalMs);
    }

    /*public static SKImage RenderEpf(DataArchive archive, DataArchiveEntry entry, string archiveName, string archiveRoot)
    {
        archiveName = archiveName.ToLower();

        switch (archiveName)
        {

        }
    }*/
}