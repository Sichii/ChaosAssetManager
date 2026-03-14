using ChaosAssetManager.Definitions;

namespace ChaosAssetManager.Model;

/// <summary>
///     Represents a light brush that stamps light values onto a grid.
///     Includes prefab patterns extracted from actual game .hea files
/// </summary>
public sealed class LightBrush
{
    public int Width => Intensities.GetLength(1);
    public int Height => Intensities.GetLength(0);
    public int CenterX => Width / 2;
    public int CenterY => Height / 2;

    /// <summary>
    ///     Precomputed intensity values for each pixel, row-major [height, width]
    /// </summary>
    public byte[,] Intensities { get; init; } = null!;

    /// <summary>
    ///     Creates a circular radial falloff brush
    /// </summary>
    public static LightBrush CreateRadial(int radius, byte centerIntensity)
    {
        var diameter = 2 * radius + 1;
        var intensities = new byte[diameter, diameter];

        for (var dy = -radius; dy <= radius; dy++)
            for (var dx = -radius; dx <= radius; dx++)
            {
                var dist = Math.Sqrt(dx * dx + dy * dy);

                if (dist > radius)
                    continue;

                var falloff = 1.0 - dist / radius;
                var value = (byte)(centerIntensity * falloff);
                intensities[dy + radius, dx + radius] = value;
            }

        return new LightBrush { Intensities = intensities };
    }

    /// <summary>
    ///     Creates a rectangular brush with radial falloff from center
    /// </summary>
    public static LightBrush CreateRectangle(int halfW, int halfH, byte centerIntensity)
    {
        var width = 2 * halfW + 1;
        var height = 2 * halfH + 1;
        var intensities = new byte[height, width];

        for (var dy = -halfH; dy <= halfH; dy++)
            for (var dx = -halfW; dx <= halfW; dx++)
            {
                //normalized distance using chebyshev-like falloff
                var nx = Math.Abs(dx) / (double)halfW;
                var ny = Math.Abs(dy) / (double)halfH;
                var dist = Math.Max(nx, ny);

                if (dist > 1.0)
                    continue;

                var falloff = 1.0 - dist;
                var value = (byte)(centerIntensity * falloff);
                intensities[dy + halfH, dx + halfW] = value;
            }

        return new LightBrush { Intensities = intensities };
    }

    /// <summary>
    ///     Creates a rotated copy of a brush using inverse-mapping
    /// </summary>
    public static LightBrush CreateRotated(LightBrush source, float angleDeg)
    {
        if (angleDeg == 0)
            return source;

        var angleRad = angleDeg * Math.PI / 180.0;
        var cos = Math.Cos(angleRad);
        var sin = Math.Sin(angleRad);

        var srcW = source.Width;
        var srcH = source.Height;
        var srcCx = source.CenterX;
        var srcCy = source.CenterY;

        //compute bounding box of the rotated brush
        var corners = new[]
        {
            (-srcCx, -srcCy),
            (srcW - srcCx, -srcCy),
            (-srcCx, srcH - srcCy),
            (srcW - srcCx, srcH - srcCy)
        };

        var minX = double.MaxValue;
        var maxX = double.MinValue;
        var minY = double.MaxValue;
        var maxY = double.MinValue;

        foreach (var (cx, cy) in corners)
        {
            var rx = cx * cos - cy * sin;
            var ry = cx * sin + cy * cos;
            minX = Math.Min(minX, rx);
            maxX = Math.Max(maxX, rx);
            minY = Math.Min(minY, ry);
            maxY = Math.Max(maxY, ry);
        }

        var newW = (int)Math.Ceiling(maxX - minX);
        var newH = (int)Math.Ceiling(maxY - minY);
        var newCx = newW / 2;
        var newCy = newH / 2;
        var intensities = new byte[newH, newW];

        //inverse-mapping: for each output pixel, find the source pixel
        for (var dy = 0; dy < newH; dy++)
            for (var dx = 0; dx < newW; dx++)
            {
                var ox = dx - newCx;
                var oy = dy - newCy;

                //rotate back to source space
                var sx = ox * cos + oy * sin + srcCx;
                var sy = -ox * sin + oy * cos + srcCy;

                var isx = (int)Math.Round(sx);
                var isy = (int)Math.Round(sy);

                if (isx < 0 || isx >= srcW || isy < 0 || isy >= srcH)
                    continue;

                intensities[dy, dx] = source.Intensities[isy, isx];
            }

        return new LightBrush { Intensities = intensities };
    }

    /// <summary>
    ///     Stamps this brush onto the light grid at the given pixel position, using max blending
    /// </summary>
    public void Stamp(byte[,] lightGrid, int centerX, int centerY)
    {
        var gridH = lightGrid.GetLength(0);
        var gridW = lightGrid.GetLength(1);

        for (var by = 0; by < Height; by++)
            for (var bx = 0; bx < Width; bx++)
            {
                var intensity = Intensities[by, bx];

                if (intensity == 0)
                    continue;

                var gx = centerX - CenterX + bx;
                var gy = centerY - CenterY + by;

                if (gx < 0 || gx >= gridW || gy < 0 || gy >= gridH)
                    continue;

                //max blend: only brighten, never darken
                if (intensity > lightGrid[gy, gx])
                    lightGrid[gy, gx] = intensity;
            }
    }

    /// <summary>
    ///     Erases light within the brush footprint (sets to 0)
    /// </summary>
    public void Erase(byte[,] lightGrid, int centerX, int centerY)
    {
        var gridH = lightGrid.GetLength(0);
        var gridW = lightGrid.GetLength(1);

        for (var by = 0; by < Height; by++)
            for (var bx = 0; bx < Width; bx++)
            {
                if (Intensities[by, bx] == 0)
                    continue;

                var gx = centerX - CenterX + bx;
                var gy = centerY - CenterY + by;

                if (gx < 0 || gx >= gridW || gy < 0 || gy >= gridH)
                    continue;

                lightGrid[gy, gx] = 0;
            }
    }

    /// <summary>
    ///     Creates an unrotated brush from a shape and radius. Rotation is applied separately during stamping
    /// </summary>
    public static LightBrush FromShape(HeaBrushShape shape, int radius, byte intensity) => shape switch
    {
        HeaBrushShape.Circle    => CreateRadial(radius, intensity),
        HeaBrushShape.Rectangle => CreateRectangle(radius, radius / 2, intensity),
        HeaBrushShape.Line      => CreateRectangle(radius, Math.Max(2, radius / 16), intensity),
        _                       => CreateRadial(radius, intensity)
    };

    /// <summary>
    ///     Creates a brush from shape, radius, and rotation. The rotation is baked into the pixel grid
    /// </summary>
    public static LightBrush FromShapeRotated(HeaBrushShape shape, int radius, float rotationDeg, byte intensity)
    {
        var brush = FromShape(shape, radius, intensity);

        //circles don't need rotation
        if (shape == HeaBrushShape.Circle || rotationDeg == 0)
            return brush;

        return CreateRotated(brush, rotationDeg);
    }
}
