using System.Drawing.Imaging;
using SkiaSharp;

namespace ChaosAssetManager.Extensions;

public static class BitmapExtensions
{
    public static SKImage ToSkImage(this Bitmap bitmap)
    {
        var skImage = SKImage.Create(new SKImageInfo(bitmap.Width, bitmap.Height));
        using var pixmap = skImage.PeekPixels();

        bitmap.ToSkPixmap(pixmap);

        return skImage;
    }

    public static void ToSkPixmap(this Bitmap bitmap, SKPixmap pixmap)
    {
        if (pixmap.ColorType == SKImageInfo.PlatformColorType)
        {
            var info = pixmap.Info;

            using var bitmap1 = new Bitmap(
                info.Width,
                info.Height,
                info.RowBytes,
                PixelFormat.Format32bppPArgb,
                pixmap.GetPixels());

            using var graphics = Graphics.FromImage(bitmap1);

            graphics.Clear(Color.Transparent);
            graphics.DrawImageUnscaled(bitmap, 0, 0);
        } else
        {
            using var skImage = bitmap.ToSkImage();

            skImage.ReadPixels(pixmap, 0, 0);
        }
    }
}