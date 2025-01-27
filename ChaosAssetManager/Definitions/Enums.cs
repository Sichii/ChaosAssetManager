namespace ChaosAssetManager.Definitions;

public enum ToolType
{
    Draw,
    Select,
    Sample,
    Erase
}

[Flags]
public enum LayerFlags
{
    Background = 1,
    LeftForeground = 2,
    RightForeground = 4,
    Foreground = LeftForeground | RightForeground,
    All = Background | Foreground
}

public enum ActionType
{
    Draw,
    Erase
}