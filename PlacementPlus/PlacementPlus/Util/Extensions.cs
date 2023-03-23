using System;
using HarmonyLib;
using Il2CppInterop.Runtime;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics;

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
    
    private static readonly IntPtr CastColliderFixMethodPtr;

    static Extensions()
    {
        var field = typeof(CollisionWorld).GetField(
            "NativeMethodInfoPtr_CastCollider_Public_Virtual_Final_New_Boolean_ColliderCastInput_byref_NativeList_1_ColliderCastHit_0", AccessTools.all);
        if (field == null)
        {
            field = typeof(CollisionWorld).GetField(
                "NativeMethodInfoPtr_CastCollider_Public_Virtual_Final_New_Boolean_ColliderCastInput_NativeList_1_ColliderCastHit_0", AccessTools.all);
        }
        
        CastColliderFixMethodPtr = (IntPtr)field.GetValue(null);
    }

    public static bool HasComponent<T>(EntityManager manager, Entity entity)
    {
        NativeArray<ComponentType> types = manager.GetComponentTypes(entity);
        int typeIndex = TypeManager.GetTypeIndex(Il2CppType.Of<T>());

        foreach (ComponentType type in types)
        {
            if (type.TypeIndex == typeIndex)
            {
                return true;
            }
        }

        types.Dispose();

        return false;
    }

    public static T GetComponentData<T>(this ObjectID objectID) where T : unmanaged
    {
        PugDatabase.InitObjectPrefabEntityLookup();
        ObjectDataCD item = new ObjectDataCD
        {
            objectID = objectID,
            amount = 1,
            variation = 0
        };

        if (PugDatabase.objectPrefabEntityLookup.ContainsKey(item))
        {
            Entity entity = PugDatabase.objectPrefabEntityLookup[item];
            EntityManager entityManager = PugDatabase.world.EntityManager;

            if (entityManager.Exists(entity) &&
                HasComponent<T>(entityManager, entity))
            {
                return entityManager.GetComponentData<T>(entity);
            }
        }

        throw new InvalidOperationException($"Object has not component {nameof(T)}");
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