using SkiaSharp;

namespace ChaosAssetManager.Model;

public record DarknessLayer
{
    public required string Name { get; init; }
    public required byte Alpha { get; init; }
    public required byte Red { get; init; }
    public required byte Green { get; init; }
    public required byte Blue { get; init; }

    /// <summary>
    ///     The darkness RGB color (alpha is handled separately via floor-clamp blending)
    /// </summary>
    public SKColor Color => new(Red, Green, Blue);

    /// <inheritdoc />
    public override string ToString() => Name;

    public static DarknessLayer[] Defaults { get; } =
    [
        new() { Name = "Darkest",  Alpha = 18, Red = 6,   Green = 11, Blue = 60  },
        new() { Name = "Darker",   Alpha = 20, Red = 27,  Green = 1,  Blue = 59  },
        new() { Name = "Dark",     Alpha = 23, Red = 100, Green = 10, Blue = 100 },
        new() { Name = "Light",    Alpha = 26, Red = 170, Green = 36, Blue = 50  },
        new() { Name = "Lighter",  Alpha = 32, Red = 0,   Green = 0,  Blue = 255 },
        new() { Name = "Lightest", Alpha = 32, Red = 0,   Green = 0,  Blue = 255 }
    ];
}
