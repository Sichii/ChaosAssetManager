using ChaosAssetManager.Model;
using DALib.Definitions;
using SkiaSharp;

namespace ChaosAssetManager.Helpers;

/// <summary>
///     Reconstructs spliced tiles into their original isometric positions as a single image: dark background,
///     tile art, an isometric diamond grid, and the 0-based placement order stamped on each tile.
/// </summary>
public static class TileLayoutMapRenderer
{
    private const int TILE_WIDTH = CONSTANTS.TILE_WIDTH;
    private const int TILE_HEIGHT = CONSTANTS.TILE_HEIGHT;
    private const int HALF_TILE_WIDTH = CONSTANTS.HALF_TILE_WIDTH;
    private const float HALF_TILE_HEIGHT = CONSTANTS.HALF_TILE_HEIGHT;

    private static readonly SKColor BackgroundColor = new(0x1A, 0x1A, 0x1A);

    public static SKImage Render(SplicedTileLayout layout)
    {
        //bound the canvas tightly to the non-empty tiles
        var keptTiles = layout.PlacedTiles
                              .Where(tile => !tile.IsEmpty)
                              .ToList();

        if (keptTiles.Count == 0)
        {
            //defensive; callers guard against an empty layout before rendering
            using var placeholder = new SKBitmap(1, 1);

            return SKImage.FromBitmap(placeholder);
        }

        var minLeft = keptTiles.Min(tile => tile.TileLeft);
        var minTop = keptTiles.Min(tile => tile.TileTop);
        var maxRight = keptTiles.Max(tile => tile.TileLeft) + TILE_WIDTH;
        var maxBottom = keptTiles.Max(tile => tile.TileTop) + TILE_HEIGHT;

        var width = maxRight - minLeft;
        var height = maxBottom - minTop;

        var info = new SKImageInfo(
            width,
            height,
            SKColorType.Bgra8888,
            SKAlphaType.Premul);

        using var surface = SKSurface.Create(info);
        var canvas = surface.Canvas;

        canvas.Clear(BackgroundColor);

        //layer 1: tile art at original positions
        foreach (var tile in keptTiles)
            canvas.DrawImage(tile.Image!, tile.TileLeft - minLeft, tile.TileTop - minTop);

        //layer 2: isometric grid on every cell within the bounding box (kept + interior/corner empties)
        DrawGrid(
            canvas,
            layout,
            minLeft,
            minTop,
            width,
            height);

        //layer 3: placement-order numbers
        DrawOrderNumbers(
            canvas,
            keptTiles,
            minLeft,
            minTop);

        return surface.Snapshot();
    }

    private static void DrawGrid(
        SKCanvas canvas,
        SplicedTileLayout layout,
        int minLeft,
        int minTop,
        int width,
        int height)
    {
        using var gridPaint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            Color = SKColors.White.WithAlpha(48),
            StrokeWidth = 1,
            IsAntialias = true
        };

        foreach (var tile in layout.PlacedTiles)
        {
            var x = tile.TileLeft - minLeft;
            var y = tile.TileTop - minTop;

            //trim cells outside the kept bounding box (e.g. empty border cells)
            if ((x < 0) || (y < 0) || ((x + TILE_WIDTH) > width) || ((y + TILE_HEIGHT) > height))
                continue;

            using var path = new SKPath();
            path.MoveTo(x + HALF_TILE_WIDTH, y);               //top
            path.LineTo(x + TILE_WIDTH, y + HALF_TILE_HEIGHT); //right
            path.LineTo(x + HALF_TILE_WIDTH, y + TILE_HEIGHT); //bottom
            path.LineTo(x, y + HALF_TILE_HEIGHT);              //left
            path.Close();

            canvas.DrawPath(path, gridPaint);
        }
    }

    private static void DrawOrderNumbers(
        SKCanvas canvas,
        List<PlacedTile> keptTiles,
        int minLeft,
        int minTop)
    {
        //SkiaSharp 3.116: MeasureText requires a paint arg; text drawn via SKFont overload
        using var measurePaint = new SKPaint();
        using var font = new SKFont(SKTypeface.Default, 11f);

        using var pillPaint = new SKPaint
        {
            Style = SKPaintStyle.Fill,
            Color = new SKColor(0, 0, 0, 180),
            IsAntialias = true
        };

        using var textPaint = new SKPaint
        {
            Color = SKColors.White,
            IsAntialias = true
        };

        var metrics = font.Metrics;

        foreach (var tile in keptTiles)
        {
            var centerX = tile.TileLeft - minLeft + HALF_TILE_WIDTH;
            var centerY = tile.TileTop - minTop + HALF_TILE_HEIGHT;

            var text = tile.Order.ToString();
            var advance = font.MeasureText(text, out var textBounds, measurePaint);

            const float padding = 3f;

            var pill = SKRect.Create(
                centerX - (advance / 2f) - padding,
                centerY - (textBounds.Height / 2f) - padding,
                advance + (padding * 2f),
                textBounds.Height + (padding * 2f));

            canvas.DrawRoundRect(
                pill,
                3f,
                3f,
                pillPaint);

            //baseline that vertically centers the glyphs on centerY (Ascent is negative)
            var baselineY = centerY - ((metrics.Ascent + metrics.Descent) / 2f);

            canvas.DrawText(
                text,
                centerX,
                baselineY,
                SKTextAlign.Center,
                font,
                textPaint);
        }
    }
}
