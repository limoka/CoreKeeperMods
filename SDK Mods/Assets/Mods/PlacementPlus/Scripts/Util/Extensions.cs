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
                   slot == EquipmentSlotType.RoofingToolSlot;
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

        public static bool IsSquare(this BrushMode mode)
        {
            return mode == BrushMode.SQUARE;
        }

        public static int GetShovelDamage(ObjectDataCD item)
        {
            if (item.objectID == ObjectID.None) return 0;
            if (item.amount == 0) return 0;

            ObjectInfo objectInfo = PugDatabase.GetObjectInfo(item.objectID, item.variation);

            if (objectInfo == null ||
                objectInfo.objectType != ObjectType.Shovel) return 0;

            var buffer = PugDatabase.GetBuffer<GivesConditionsWhenEquippedBuffer>(item);
            foreach (GivesConditionsWhenEquippedBuffer condition in buffer)
            {
                if (condition.equipmentCondition.id != ConditionID.DiggingIncrease) continue;

                return condition.equipmentCondition.value;
            }

            return 0;
        }

        public static int GetPickaxeDamage(ObjectDataCD item)
        {
            if (item.objectID == ObjectID.None) return 0;
            if (item.amount == 0) return 0;

            ObjectInfo objectInfo = PugDatabase.GetObjectInfo(item.objectID, item.variation);

            if (objectInfo == null ||
                objectInfo.objectType != ObjectType.MiningPick) return 0;

            var buffer = PugDatabase.GetBuffer<GivesConditionsWhenEquippedBuffer>(item);
            foreach (GivesConditionsWhenEquippedBuffer condition in buffer)
            {
                if (condition.equipmentCondition.id != ConditionID.MiningIncrease) continue;

                return condition.equipmentCondition.value;
            }

            return 0;
        }
    }
}