namespace PlacementPlus.Util
{
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