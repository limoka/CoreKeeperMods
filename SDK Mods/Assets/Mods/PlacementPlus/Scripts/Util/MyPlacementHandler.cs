using System;
using CoreLib.Util.Extension;
using HarmonyLib;
using Mods.PlacementPlus.Scripts.Util;
using PlacementPlus.Components;
using PlayerEquipment;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace PlacementPlus
{
    [HarmonyPatch]
    public static class MyPlacementHandler
    {
        [HarmonyReversePatch]
        [HarmonyPatch(typeof(PlacementHandler), "CanPlaceObjectAtPosition")]
        public static int CanPlaceObjectAtPosition(
            Entity placementPrefab, 
            int3 posToPlaceAt, 
            int width, 
            int height, 
            NativeHashMap<int3, bool> tilesChecked, 
            in EquipmentUpdateAspect equipmentUpdateAspect, 
            in EquipmentUpdateSharedData equipmentUpdateSharedData, 
            in LookupEquipmentUpdateData equipmentUpdateLookupData
        ) =>
            throw new NotImplementedException("It's a stub");
        
        [HarmonyReversePatch]
        [HarmonyPatch(typeof(PlacementHandler), "FindPlaceablePositionFromMouseOrJoystick")]
        public static bool FindPlaceablePositionFromMouseOrJoystick(
            Entity placementPrefab, 
            int width,
            int height, 
            NativeHashMap<int3, bool> tilesChecked,
            ref NativeList<PlacementHandler.EntityAndInfoFromPlacement> diggableEntityAndInfos,
            in EquipmentUpdateAspect equipmentUpdateAspect, 
            in EquipmentUpdateSharedData equipmentUpdateSharedData,
            in LookupEquipmentUpdateData equipmentUpdateLookupData
        ) =>
            throw new NotImplementedException("It's a stub");
        
        [HarmonyReversePatch]
        [HarmonyPatch(typeof(PlacementHandler), "FindPlaceablePositionFromOwnerDirection")]
        public static void FindPlaceablePositionFromOwnerDirection(
            Entity placementPrefab, 
            int width, 
            int height, 
            NativeHashMap<int3, bool> tilesChecked, 
            ref NativeList<PlacementHandler.EntityAndInfoFromPlacement> diggableEntityAndInfos,
            in EquipmentUpdateAspect equipmentUpdateAspect, 
            in EquipmentUpdateSharedData equipmentUpdateSharedData,
            in LookupEquipmentUpdateData equipmentUpdateLookupData
        ) =>
            throw new NotImplementedException("It's a stub");
        
        public static void UpdatePlaceablePosition(
            Entity placementPrefab,
            ref NativeList<PlacementHandler.EntityAndInfoFromPlacement> diggableEntityAndInfos,
            in EquipmentUpdateAspect equipmentUpdateAspect,
            in EquipmentUpdateSharedData equipmentUpdateSharedData,
            in LookupEquipmentUpdateData equipmentUpdateLookupData,
            in PlacementPlusState state)
        {
            ref PlacementCD local = ref equipmentUpdateAspect.placementCD.ValueRW;
            int2 currentSize = PlacementHandler.GetCurrentSize(
                placementPrefab,
                equipmentUpdateAspect.equippedObjectCD.ValueRO.containedObject.objectData,
                in local,
                equipmentUpdateAspect.placementSizeByEquipmentTypeBuffer,
                equipmentUpdateAspect.equipmentSlotCD.ValueRO,
                equipmentUpdateSharedData.databaseBank, 
                equipmentUpdateLookupData.directionBasedOnVariationLookup,
                equipmentUpdateLookupData.objectPropertiesLookup,
                equipmentUpdateLookupData.directionLookup,
                equipmentUpdateLookupData.sizeVariationLookup);

            if (state.size > 0)
            {
                BrushRect extents = state.GetExtents();

                int width = extents.width + 1;
                int height = extents.height + 1;
                currentSize = new int2(width, height);
            }
            
            local.canPlaceObject = true;
            NativeHashMap<int3, bool> tilesChecked = new NativeHashMap<int3, bool>(32, Allocator.Temp);
            if (FindPlaceablePositionFromMouseOrJoystick(
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
            
            
            FindPlaceablePositionFromOwnerDirection(
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