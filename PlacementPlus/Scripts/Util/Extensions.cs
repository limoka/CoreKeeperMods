using PlayerEquipment;
using Unity.Mathematics;

namespace PlacementPlus.Util
{
    public static class Extensions
    {
        // Shovels, hoes and the roofing gadget keep their area in the game's size
        // variation: CHANGE_SIZE is routed to sizeVariationToPlace for anything
        // with a ResizableTileSizeCD, so state.size never moves for them. For
        // these the brush picks the shape only, and the size keys stay the
        // game's own.
        public static bool OwnsItsSize(this EquipmentSlotType slot)
        {
            return slot == EquipmentSlotType.ShovelSlot ||
                   slot == EquipmentSlotType.HoeSlot ||
                   slot == EquipmentSlotType.RoofingToolSlot ||
                   slot == EquipmentSlotType.PaintToolSlot; 
        }

        public static int2 CutToBrush(this BrushMode mode, int2 size)
        {
            return new int2(
                mode.IsHorizontal() ? size.x : 1,
                mode.IsVertical() ? size.y : 1);
        }

        public static bool IsHorizontal(this BrushMode mode)
        {
            return (mode & BrushMode.HORIZONTAL) == BrushMode.HORIZONTAL;
        }

        public static bool IsVertical(this BrushMode mode)
        {
            return (mode & BrushMode.VERTICAL) == BrushMode.VERTICAL;
        }
    }
}