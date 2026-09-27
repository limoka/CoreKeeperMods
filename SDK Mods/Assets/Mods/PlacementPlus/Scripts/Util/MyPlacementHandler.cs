using System;
using CoreLib.Util.Extension;
using HarmonyLib;
using Mods.PlacementPlus.Scripts.Util;
using PlacementPlus.Components;
using PlacementPlus.Util;
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

            if (equipmentUpdateAspect.equipmentSlotCD.ValueRO.slotType.OwnsItsSize())
            {
                // ⚠️ Vanilla GetCurrentSize only uses the size settings (variation values) if the prefab 
                // possesses ResizableTileSizeCD; otherwise, it returns prefabTileSize as-is. 
                // Since we inflate the tool limit to MaxBrushSize, tools lacking that component (such as lower-tier shovels) 
                // default to "always 9x9" — the preview checks the variation value and draws small, but the tool 
                // actually digs a 9x9 area, and the preview cell gets anchored to the bottom-left corner of that 9x9 box 
                // (Observed by SirSephiroth1 on 08-28. Hoes possess the component, so they were asymptomatic). 
                // We unify the tool calculations to match the preview.
                ObjectDataCD toolData = equipmentUpdateAspect.equippedObjectCD.ValueRO.containedObject.objectData;
                ref PugDatabase.EntityObjectInfo toolInfo = ref PugDatabase.GetEntityObjectInfo(
                    toolData.objectID, equipmentUpdateSharedData.databaseBank.databaseBankBlob, toolData.variation);
                if (toolInfo.objectID != ObjectID.None)
                    currentSize = EquipmentSlot.GetTileSizeFromVariation(
                        equipmentUpdateAspect.equipmentSlotCD.ValueRO,
                        equipmentUpdateAspect.placementSizeByEquipmentTypeBuffer,
                        toolInfo.prefabTileSize);


                // state.size belongs to the last placeable that was held — reading
                // it here would freeze the tool at that number and make the tool's
                // own size keys look dead.
                if (state.mode != BrushMode.NONE)
                    currentSize = state.mode.CutToBrush(currentSize);
            }
            else if (state.size > 0)
            {
                BrushRect extents = state.GetExtents();

                int width = extents.width + 1;
                int height = extents.height + 1;
                currentSize = new int2(width, height);
            }
            else if (state.mode != BrushMode.NONE)
            {
                currentSize = new int2(1, 1);
            }

            // Placeables (grids) explicitly force validation to true — because the vanilla rule is 
            // "only when every single cell is valid," a single blocked cell breaks the entire brush. 
            // ⚠️ Do not force this for tools (shovels, hoes, roofing): We must use the exact values calculated 
            // by the game so that the red/blue previews match the server (Observed on 08-27: if forced, it 
            // remained blue even over holes, but because the server evaluated it as red, it flickered).
            bool ownsItsSize = equipmentUpdateAspect.equipmentSlotCD.ValueRO.slotType.OwnsItsSize();
            if (!ownsItsSize) local.canPlaceObject = true;
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
                if (!ownsItsSize) local.canPlaceObject = true;
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
            if (!ownsItsSize) local.canPlaceObject = true;
        }
    }
}