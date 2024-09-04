using CoreLib.Util.Extensions;
using PlacementPlus.Access;
using PlacementPlus.Components;
using PlayerEquipment;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace PlacementPlus
{
    public static class MyPlacementHandler
    {
        public static void UpdatePlaceablePosition(
            Entity placementPrefab,
            ref NativeList<PlacementHandler.EntityAndInfoFromPlacement> diggableEntityAndInfos,
            in EquipmentUpdateAspect equipmentUpdateAspect,
            in EquipmentUpdateSharedData equipmentUpdateSharedData,
            in LookupEquipmentUpdateData equipmentUpdateLookupData,
            in PlacementPlusState state)
        {
            ref PlacementCD local = ref equipmentUpdateAspect.placementCD.ValueRW;
            int2 currentSize = PlacementHandler.GetCurrentSize(placementPrefab, equipmentUpdateAspect.equippedObjectCD.ValueRO.containedObject.objectData,
                in local, equipmentUpdateSharedData.databaseBank, equipmentUpdateLookupData.directionBasedOnVariationLookup,
                equipmentUpdateLookupData.objectPropertiesLookup, equipmentUpdateLookupData.directionLookup);

            if (state.size > 0)
            {
                currentSize = new int2(state.size + 1, state.size + 1);
            }
            
            local.canPlaceObject = true;
            NativeHashMap<int3, bool> tilesChecked = new NativeHashMap<int3, bool>(32, Allocator.Temp);
            if (AccessExtensions.FindPlaceablePositionFromMouseOrJoystick(
                    placementPrefab,
                    currentSize.x,
                    currentSize.y,
                    tilesChecked,
                    ref diggableEntityAndInfos,
                    in equipmentUpdateAspect,
                    in equipmentUpdateSharedData,
                    in equipmentUpdateLookupData))
            {
                local.canPlaceObject = true;
                return;
            }
            
            
            AccessExtensions.FindPlaceablePositionFromOwnerDirection(
                placementPrefab, 
                currentSize.x, 
                currentSize.y, 
                tilesChecked, 
                ref diggableEntityAndInfos,
                in equipmentUpdateAspect, 
                in equipmentUpdateSharedData, 
                in equipmentUpdateLookupData
                );
            local.canPlaceObject = true;
        }

        private static bool FindPlaceablePositionFromMouseOrJoystick(Entity placementPrefab, int width, int height, NativeHashMap<int3, bool> tilesChecked,
            ref NativeList<PlacementHandler.EntityAndInfoFromPlacement> diggableEntityAndInfos, in EquipmentUpdateAspect equipmentUpdateAspect,
            in EquipmentUpdateSharedData equipmentUpdateSharedData, in LookupEquipmentUpdateData equipmentUpdateLookupData)
        {
            ComponentLookup<LocalTransform> localTransformLookup = equipmentUpdateLookupData.localTransformLookup;
            float3 position = localTransformLookup.GetRefRO(equipmentUpdateAspect.entity).ValueRO.Position;
            int num = width * height;
            ref readonly ClientInput valueRO = ref equipmentUpdateAspect.clientInput.ValueRO;
            if (!valueRO.prefersKeyboardAndMouse && !valueRO.wasAiming)
            {
                return false;
            }

            ObjectDataCD objectData = equipmentUpdateAspect.equippedObjectCD.ValueRO.containedObject.objectData;
            float3 @float = valueRO.mouseOrJoystickWorldPoint.ToFloat3();
            float3 float2 = new float3((width - 1) / 2f, 0f, (height - 1) / 2f);
            @float -= float2;
            Bounds bounds = new Bounds(position - float2, new Vector3(width + GetMaxReachDistance(objectData), 0f, height + GetMaxReachDistance(objectData)));
            int3 @int = bounds.ClosestPoint(@float).ToFloat3().RoundToInt3();
            ref PlacementCD valueRW = ref equipmentUpdateAspect.placementCD.ValueRW;
            valueRW.canPlaceObject = PlacementHandler.CanPlaceObjectAtPositionForSlotType(placementPrefab, @int, width, height, tilesChecked,
                ref diggableEntityAndInfos, equipmentUpdateAspect, equipmentUpdateSharedData, equipmentUpdateLookupData) > 0;
            valueRW.bestPositionToPlaceAt = @int;
            return true;
        }

        private static void FindPlaceablePositionFromOwnerDirection(Entity placementPrefab, int width, int height, NativeHashMap<int3, bool> tilesChecked,
            ref NativeList<PlacementHandler.EntityAndInfoFromPlacement> diggableEntityAndInfos, in EquipmentUpdateAspect equipmentUpdateAspect,
            in EquipmentUpdateSharedData equipmentUpdateSharedData, in LookupEquipmentUpdateData equipmentUpdateLookupData)
        {
            Direction facingDirection = equipmentUpdateAspect.animationOrientationCD.ValueRO.facingDirection;
            ComponentLookup<LocalTransform> localTransformLookup = equipmentUpdateLookupData.localTransformLookup;
            float3 position = localTransformLookup.GetRefRO(equipmentUpdateAspect.entity).ValueRO.Position;
            int3 @int = position.RoundToInt3();
            int num = width * height;
            ObjectDataCD objectData = equipmentUpdateAspect.equippedObjectCD.ValueRO.containedObject.objectData;
            int3 int2 = (position + facingDirection.f3 * GetReachDistance(objectData)).RoundToInt3();
            int3 int3 = (position + facingDirection.f3 * GetShortReachDistance(objectData)).RoundToInt3();
            NativeList<int3> nativeList = new NativeList<int3>(2, Allocator.Temp);
            nativeList.Add(int3);
            if (math.all(int3 != int2))
            {
                nativeList.Add(int2);
            }

            ref PlacementCD valueRW = ref equipmentUpdateAspect.placementCD.ValueRW;
            int num2 = 0;
            foreach (int3 int4 in nativeList)
            {
                int num3 = -(width - 1) / 2;
                int num4 = -(height - 1) / 2;
                bool flag = false;
                if (facingDirection.id == Direction.Id.back || facingDirection.id == Direction.Id.zero)
                {
                    flag = true;
                    num4 = -height + 1;
                }
                else if (facingDirection.id == Direction.Id.forward)
                {
                    flag = true;
                    num4 = 0;
                }
                else if (facingDirection.id == Direction.Id.left)
                {
                    num3 = -width + 1;
                }
                else if (facingDirection.id == Direction.Id.right)
                {
                    num3 = 0;
                }

                valueRW.canPlaceObject = false;
                int num5 = (flag ? (width / 2) : 0);
                int num6 = (flag ? 0 : (height / 2));
                bool flag2 = (flag ? (width % 2 == 0) : (height % 2 == 0));
                for (int i = 0; i <= num5; i++)
                {
                    for (int j = 0; j <= num6; j++)
                    {
                        int num7 = num3 + i;
                        int num8 = num4 + j;
                        int3 int5 = new int3(num7, 0, num8);
                        int3 int6 = int4 + int5;
                        int num9 = PlacementHandler.CanPlaceObjectAtPositionForSlotType(placementPrefab, int6, width, height, tilesChecked,
                            ref diggableEntityAndInfos, equipmentUpdateAspect, equipmentUpdateSharedData, equipmentUpdateLookupData);
                        if ((!flag2 || i != num5 || j != num6) && num9 > num2)
                        {
                            num2 = num9;
                            valueRW.bestPositionToPlaceAt = int6;
                            if (num2 == num)
                            {
                                valueRW.canPlaceObject = true;
                                break;
                            }
                        }
                        else if (i > 0 || j > 0)
                        {
                            num7 = num3 - i;
                            num8 = num4 - j;
                            int5 = new int3(num7, 0, num8);
                            int6 = int4 + int5;
                            num9 = PlacementHandler.CanPlaceObjectAtPositionForSlotType(placementPrefab, int6, width, height, tilesChecked,
                                ref diggableEntityAndInfos, equipmentUpdateAspect, equipmentUpdateSharedData, equipmentUpdateLookupData);
                            if (num9 > num2)
                            {
                                num2 = num9;
                                valueRW.bestPositionToPlaceAt = int6;
                                if (num2 == num)
                                {
                                    valueRW.canPlaceObject = true;
                                    break;
                                }
                            }
                        }
                    }

                    if (valueRW.canPlaceObject)
                    {
                        break;
                    }
                }

                if (valueRW.canPlaceObject)
                {
                    break;
                }
            }

            nativeList.Dispose();
            if (!valueRW.canPlaceObject && valueRW.canBePlacedOnPlayer)
            {
                num2 = PlacementHandler.CanPlaceObjectAtPositionForSlotType(placementPrefab, @int, width, height, tilesChecked, ref diggableEntityAndInfos,
                    equipmentUpdateAspect, equipmentUpdateSharedData, equipmentUpdateLookupData);
                if (num2 == num)
                {
                    valueRW.bestPositionToPlaceAt = @int;
                    valueRW.canPlaceObject = true;
                }
            }

            if (num2 == 0)
            {
                int num10 = -(width - 1) / 2;
                int num11 = -(height - 1) / 2;
                if (facingDirection.id == Direction.Id.back || facingDirection.id == Direction.Id.zero)
                {
                    num11 = -height + 1;
                }
                else if (facingDirection.id == Direction.Id.forward)
                {
                    num11 = 0;
                }
                else if (facingDirection.id == Direction.Id.left)
                {
                    num10 = -width + 1;
                }
                else if (facingDirection.id == Direction.Id.right)
                {
                    num10 = 0;
                }

                valueRW.bestPositionToPlaceAt = @int + new int3(num10, 0, num11);
            }
        }

        private static float GetMaxReachDistance(in ObjectDataCD equippedObjectData)
        {
            if (!HasLimitedPlacementRange(equippedObjectData))
            {
                return 1.5f;
            }

            return 1f;
        }

        private static float GetReachDistance(in ObjectDataCD equippedObjectData)
        {
            if (!HasLimitedPlacementRange(equippedObjectData))
            {
                return 1f;
            }

            return 0.75f;
        }

        private static float GetShortReachDistance(in ObjectDataCD equippedObjectData)
        {
            if (!HasLimitedPlacementRange(equippedObjectData))
            {
                return 0.5f;
            }

            return 0.25f;
        }

        private static bool HasLimitedPlacementRange(in ObjectDataCD equippedObjectData)
        {
            ObjectID objectID = equippedObjectData.objectID;
            return objectID == ObjectID.Boat || objectID == ObjectID.SpeederBoat;
        }
    }
}