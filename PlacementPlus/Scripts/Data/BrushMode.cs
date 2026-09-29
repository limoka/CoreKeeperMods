using System;

namespace PlacementPlus
{
    [Flags]
    public enum BrushMode : byte
    {
        NONE,
        HORIZONTAL = 1,
        VERTICAL = 2,
        SQUARE = HORIZONTAL | VERTICAL,
        MAX
    }
}