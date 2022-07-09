namespace PlacementPlus.Util;

public static class Extensions
{
    public static bool IsHorizontal(this BrushMode mode)
    {
        return (mode & BrushMode.HORIZONTAL) == BrushMode.HORIZONTAL;
    }
    
    public static bool IsVertical(this BrushMode mode)
    {
        return (mode & BrushMode.VERTICAL) == BrushMode.VERTICAL;
    }

    public static bool IsSquare(this BrushMode mode)
    {
        return mode == BrushMode.SQUARE;
    }
}