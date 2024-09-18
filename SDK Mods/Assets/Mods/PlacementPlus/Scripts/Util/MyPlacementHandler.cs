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
                BrushRect extents = state.GetExtents();

                int width = extents.width + 1;
                int height = extents.height + 1;
                currentSize = new int2(width, height);
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
    }
}