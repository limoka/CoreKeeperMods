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
        // M2 (plan-deharmony): The latest grid anchor value calculated by the client — 
        // Used by PlacementHandler_Patch to lock the icon position in place. Because placementCD 
        // handles prediction synchronization, it bounces back and forth with the server (vanilla) value, 
        // causing the screen to shake; therefore, the rendering path trusts this cache. 
        // It uses frameCount to determine data freshness.
        internal static int3 lastClientAnchor;
        internal static int lastClientAnchorFrame = -1;

        // Duplicate transmission guard: When prediction re-simulation reruns the same placement tick, 
        // 2 to 4 RPCs get fired for the exact same anchor (Measured and observed on 08-25). 
        // The server then executes the grid that many times → causing prediction desyncs → leading to 
        // longer re-simulations → triggering more retransmissions... This positive feedback loop 
        // creates a multi-second freeze. Since re-simulations cannot fake the wall-clock time, 
        // we throttle transmissions based on realtime.
        private static int3 s_lastSentAnchor;
        private static float s_lastSentReal = -10f;

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

            ComponentLookup<ObjectPropertiesCD> objectPropertiesLookup = equipmentUpdateLookupData.objectPropertiesLookup;
            
            Entity equipmentPrefab = equipmentUpdateAspect.equippedObjectCD.ValueRO.equipmentPrefab;
            if (!objectPropertiesLookup.TryGetComponent(equipmentPrefab, out ObjectPropertiesCD properties))
                return false;
            
            if (!ObjectPlacementLogic.IsItemValid(ref entityObjectInfo, ref properties)) return true;

            // Placing against a wall side: hand it back to the game. The grid path
            // bails out on canPlaceOnSideOfWall, and since this prefix skips the
            // original, taking over here would drop the placement entirely.
            if (equipmentUpdateAspect.placementCD.ValueRO.canPlaceOnSideOfWall) return true;
            
            if (clientInput.IsButtonStateSet(CommandInputButtonStateNames.Rotate_Pressed))
            {
                PlaceObjectSlot.Rotate(equipmentUpdateAspect, equipmentUpdateSharedData, equipmentUpdateLookupData);
            }
            
            if (properties.Has(PropertyID.PlaceableObject.alignWithPlayerDirection))
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

            if (!equipmentUpdateSharedData.isServer)
            {
                lastClientAnchor = equipmentUpdateAspect.placementCD.ValueRO.bestPositionToPlaceAt;
                lastClientAnchorFrame = Time.frameCount;
            }

            __result = false;
            if (!secondInteractHeld) return false;
            if (hasItemInMouse) return false;
            
            bool placed = ObjectPlacementLogic.PlaceItemGrid(
                equipmentUpdateAspect,
                equipmentUpdateSharedData,
                equipmentUpdateLookupData,
                ppLookups,
                state
            );

            // M2 (plan-deharmony): On Burst-enabled servers, the server-side part of this prefix 
            // completely breaks, failing to place the grid. Therefore, we command the server to 
            // execute the placement on the exact frame the client places it — using the client-side 
            // anchor value just calculated by MyPlacementHandler (which differs from the server's vanilla 
            // anchor, Round 1 observation). In environments where the server patch is fully functional, 
            // the executor remains dormant (serverPrefixAlive), making this harmless. We restrict transmissions 
            // to 'placed' (cooldown passed) frames only to prevent spam.

            if (placed && !equipmentUpdateSharedData.isServer)
            {
                int3 sendAnchor = equipmentUpdateAspect.placementCD.ValueRO.bestPositionToPlaceAt;
                // Duplication logic uses a 0.5-second wall-clock window — because prediction re-simulation 
                // generates the same anchor multiple times in the exact same instant, this window catches it.
                // ⚠️ We previously tried changing this to a tick-based window to let "rapid clicking" pass 
                // through (08-27), but because the engine's design fires placement commands across multiple ticks 
                // for a single click, floors and walls were placed simultaneously — Reverted (User decision).
                bool duplicate = math.all(sendAnchor == s_lastSentAnchor) &&
                                 Time.realtimeSinceStartup - s_lastSentReal < 0.5f;
                if (!duplicate)
                {
                    s_lastSentAnchor = sendAnchor;
                    s_lastSentReal = Time.realtimeSinceStartup;
                    PlacementPlusMod.commandSystem?.SendBrushPlace(
                        equipmentUpdateAspect.entity, sendAnchor);
                }
            }

            __result = true;
            return false;
        }
    }
}