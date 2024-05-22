using ChaosAssetManager.Model;
using DALib.Data;
using DALib.Drawing;
using DALib.Utility;
using Graphics = DALib.Drawing.Graphics;

namespace ChaosAssetManager.Helpers;

public static partial class RenderUtil
{
    public static Animation? RenderNationalEpf(DataArchive archive, DataArchiveEntry entry, string archiveRoot)
    {
        try
        {
            var epfFile = EpfFile.FromEntry(entry);

            if (!TryLoadLegendPalFromRoot(archiveRoot, out var legendPal))
                return null;

            var transformer = epfFile.Select(frame => Graphics.RenderImage(frame, legendPal));
            var frames = new SKImageCollection(transformer);

            return new Animation(frames);
        } catch
        {
            return null;
        }
    }
}