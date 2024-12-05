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

        /*
        var rect = new Rectangle(
            0,
            0,
            bitmap.Width,
            bitmap.Height);
        var bmpData = bitmap.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

        using var skBitmap = new SKBitmap(
            bitmap.Width,
            bitmap.Height,
            SKColorType.Bgra8888,
            SKAlphaType.Premul);

        skBitmap.InstallPixels(skBitmap.Info, bmpData.Scan0, bmpData.Stride);

        bitmap.UnlockBits(bmpData);

        return SKImage.FromBitmap(skBitmap);*/
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