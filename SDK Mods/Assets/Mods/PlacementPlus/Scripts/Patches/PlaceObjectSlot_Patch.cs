using System;
using HarmonyLib;
using PlayerEquipment;
using Pug.Properties;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace PlacementPlus
{
    [HarmonyPatch]
    public static class PlaceObjectSlot_Patch
    {
        
        [HarmonyPatch(typeof(PlaceObjectSlot), nameof(PlaceObjectSlot.UpdateEquipment))]
        [HarmonyPrefix]
        public static bool OnUpdateEquipment(
            bool interactHeld,
            bool secondInteractHeld,
            in ClientInput clientInput,
            in EquipmentUpdateAspect equipmentUpdateAspect,
            in EquipmentUpdateSharedData equipmentUpdateSharedData,
            in LookupEquipmentUpdateData equipmentUpdateLookupData,
            bool hasItemInMouse,
            ref bool __result
        )
        {
            
            var containedObject = equipmentUpdateAspect.equippedObjectCD.ValueRO.containedObject;
            if (containedObject.auxDataIndex > 0) return true;

            ObjectDataCD objectData = containedObject.objectData;
            ref PugDatabase.EntityObjectInfo entityObjectInfo = ref PugDatabase.GetEntityObjectInfo(objectData.objectID,
                equipmentUpdateSharedData.databaseBank.databaseBankBlob, objectData.variation);

            if (!ObjectPlacementLogic.IsItemValid(ref entityObjectInfo)) return true;
            
            if (clientInput.IsButtonStateSet(CommandInputButtonStateNames.Rotate_Pressed))
            {
                PlaceObjectSlot.Rotate(equipmentUpdateAspect, equipmentUpdateSharedData, equipmentUpdateLookupData);
            }
            
            Entity equipmentPrefab = equipmentUpdateAspect.equippedObjectCD.ValueRO.equipmentPrefab;
            ComponentLookup<ObjectPropertiesCD> objectPropertiesLookup = equipmentUpdateLookupData.objectPropertiesLookup;
            if (objectPropertiesLookup.TryGetComponent(equipmentPrefab, out ObjectPropertiesCD objectPropertiesCD) && 
                objectPropertiesCD.Has(PropertyID.PlaceableObject.alignWithPlayerDirection))
            {
                PlaceObjectSlot.AlignWithPlayer(equipmentUpdateAspect, equipmentUpdateSharedData, equipmentUpdateLookupData);
            }
            
            var ppLookups = EquipmentSystem_Patch.GetLookups(equipmentUpdateSharedData.isServer);
            var state = ppLookups.stateLookup[equipmentUpdateAspect.entity];
            
            var nativeList = new NativeList<PlacementHandler.EntityAndInfoFromPlacement>(Allocator.Temp);
            MyPlacementHandler.UpdatePlaceablePosition(
                equipmentUpdateAspect.equippedObjectCD.ValueRO.equipmentPrefab,
                ref nativeList,
                equipmentUpdateAspect,
                equipmentUpdateSharedData,
                equipmentUpdateLookupData,
                state);
            nativeList.Dispose();

            __result = false;
            if (!secondInteractHeld) return false;
            if (hasItemInMouse) return false;
            
            ObjectPlacementLogic.PlaceItemGrid(
                equipmentUpdateAspect,
                equipmentUpdateSharedData,
                equipmentUpdateLookupData,
                ppLookups,
                state
            );

            __result = true;
            return false;
        }
    }
}