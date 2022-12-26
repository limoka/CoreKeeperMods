using System;
using UnityEngine;

namespace PlacementPlus;

public readonly struct BrushRect
{
    public readonly int xOffset;
    public readonly int yOffset;

    public readonly int width;
    public readonly int height;

    public Vector3Int offset => new Vector3Int(xOffset, 0, yOffset);
    public int minX => -xOffset;
    public int maxX => width - xOffset;

    public int minY => -yOffset;
    public int maxY => height - yOffset;

    public BrushRect(int xOffset, int yOffset, int width, int height)
    {
        this.xOffset = xOffset;
        this.yOffset = yOffset;
        this.width = width;
        this.height = height;
    }
}

[Flags]
public enum BrushMode
{
    NONE,
    HORIZONTAL = 1,
    VERTICAL = 2,
    SQUARE = HORIZONTAL | VERTICAL,
    MAX
}