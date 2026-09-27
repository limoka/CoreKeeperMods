using HarmonyLib;
using PlacementPlus.Components;
using PlayerEquipment;
using Unity.Mathematics;
using UnityEngine;

namespace PlacementPlus
{
    // Command to notify the server of the shovel/hoe brush "shape" on Burst-enabled dedicated servers.
    // The server already knows the size because sizeVariationToPlace handles ghost synchronization, 
    // but shapes (horizontal lines, vertical lines) only exist within our client patch, causing the server 
    // to dig in a true square by default (Observed on 08-27). For true squares or when turned off, 
    // the vanilla server already digs correctly, so we do not send it — we narrow down the blast radius 
    // to line modes only.
    [HarmonyPatch]
    public static class ToolDig_Patch
    {
        private static int3 s_lastSentAnchor;
        private static float s_lastSentReal = -10f;

        [HarmonyPatch(typeof(ShovelSlot), nameof(ShovelSlot.UpdateEquipment))]
        [HarmonyPostfix]
        public static void OnShovelUpdateEquipment(
            in EquipmentUpdateAspect equipmentUpdateAspect,
            in EquipmentUpdateSharedData equipmentUpdateSharedData,
            bool __result)
        {
            SendIfLineShape(equipmentUpdateAspect, equipmentUpdateSharedData, __result);
        }

        [HarmonyPatch(typeof(HoeSlot), nameof(HoeSlot.UpdateEquipment))]
        [HarmonyPostfix]
        public static void OnHoeUpdateEquipment(
            in EquipmentUpdateAspect equipmentUpdateAspect,
            in EquipmentUpdateSharedData equipmentUpdateSharedData,
            bool __result)
        {
            SendIfLineShape(equipmentUpdateAspect, equipmentUpdateSharedData, __result);
        }

        private static void SendIfLineShape(
            in EquipmentUpdateAspect aspect,
            in EquipmentUpdateSharedData sharedData,
            bool dug)
        {
            // __result true = Means a dig actually occurred on this frame (Game's UpdateEquipment).
            if (!dug || sharedData.isServer) return;

            var ppLookups = EquipmentSystem_Patch.GetLookups(false);
            if (!ppLookups.stateLookup.HasComponent(aspect.entity)) return;

            // Tools always run through our path on the server as well, regardless of the brush shape or "Off" state — 
            // ① If the server uses vanilla validation in square mode, the preview flashes/flickers (Observed on 08-27), 
            // and ② when set to "Off", vanilla ignores the size settings and gets stuck at 9x9 (Observed on 08-28).
            
            // 0.3s window: This is shorter than the shovel cooldown (0.4s), allowing legitimate rapid clicking 
            // to pass through while filtering out only prediction re-simulation duplicates (same mechanism as roofing/paint).
            int3 anchor = aspect.placementCD.ValueRO.bestPositionToPlaceAt;
            bool duplicate = math.all(anchor == s_lastSentAnchor) &&
                             Time.realtimeSinceStartup - s_lastSentReal < 0.4f;
            if (duplicate) return;

            s_lastSentAnchor = anchor;
            s_lastSentReal = Time.realtimeSinceStartup;
            PlacementPlusMod.commandSystem?.SendBrushPlace(aspect.entity, anchor, Commands.BrushKind.Tool);
        }
    }
}
