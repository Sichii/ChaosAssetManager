using ChaosAssetManager.Model;
using DALib.Data;
using DALib.Drawing;
using DALib.Utility;
using Graphics = DALib.Drawing.Graphics;

namespace ChaosAssetManager.Helpers;

public static partial class RenderUtil
{
    public static Animation? RenderMiscEpf(DataArchive archive, DataArchiveEntry entry, string archiveRoot)
    {
        if (!TryLoadLegendPalFromRoot(archiveRoot, out var legendPal))
            return null;

        var epfFile = EpfFile.FromEntry(entry);
        var transformer = epfFile.Select(frame => Graphics.RenderImage(frame, legendPal));
        var images = new SKImageCollection(transformer);

        return new Animation(images);
    }
}