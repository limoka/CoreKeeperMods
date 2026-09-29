namespace PlacementPlus
{
    public enum BlockMode : byte
    {
        TOGGLE,
        GROUND,
        WALL,
        // Creative-only void brush: clears every layer of the cell. Kept last so
        // the cycle can simply stop at WALL in non-creative worlds.
        REMOVE,
        MAX
    }
}