using HarmonyLib;
using PlacementPlus.Components;
using PlacementPlus.Util;
using PlayerEquipment;
using Unity.Collections;
using Unity.Entities;

namespace PlacementPlus
{
    // Shovel and hoe both call PlacementHandler.UpdatePlaceablePosition to fill
    // the list of tiles/entities they are about to act on, then loop over that
    // whole list in Dig(). So widening the list is enough — Dig() needs no patch.
    // The roofing gadget calls the same method; it only needs the position, the
    // tiles it changes are handled in RoofingToolSlot_Patch.
    [HarmonyPatch]
    public static class ShovelSlot_Patch
    {
        [HarmonyPatch(typeof(PlacementHandler), nameof(PlacementHandler.UpdatePlaceablePosition))]
        [HarmonyPrefix]
        public static bool OnUpdatePlaceablePosition(
            Entity placementPrefab,
            ref NativeList<PlacementHandler.EntityAndInfoFromPlacement> diggableEntityAndInfos,
            in EquipmentUpdateAspect equipmentUpdateAspect,
            in EquipmentUpdateSharedData equipmentUpdateSharedData,
            in LookupEquipmentUpdateData equipmentUpdateLookupData
        )
        {
            // The roofing gadget goes through here too: it calls the same
            // UpdatePlaceablePosition, and letting the game size the position for
            // a 15x1 area is what keeps the roof strip under the cursor instead
            // of half a tool-width away.
            if (!equipmentUpdateAspect.equipmentSlotCD.ValueRO.slotType.OwnsItsSize()) return true;

            var ppLookups = EquipmentSystem_Patch.GetLookups(equipmentUpdateSharedData.isServer);
            if (!ppLookups.stateLookup.HasComponent(equipmentUpdateAspect.entity)) return true;

            PlacementPlusState state = ppLookups.stateLookup[equipmentUpdateAspect.entity];

            // ⚠️ Even when the brush is turned off, we handle drawing the tools. Vanilla GetCurrentSize 
            // ignores the size settings for tools that lack ResizableTileSizeCD (such as wooden/copper shovels) 
            // and uses prefabTileSize as-is. Since we inflate that value to MaxBrushSize, it gets stuck 
            // at 9x9 when set to "Off", completely breaking size scaling (Reported by SirSephiroth1 → 
            // Observed on 08-28: it only starts working after switching to horizontal mode). 
            // If the shape is set to NONE, MyPlacementHandler does not clip it, so it remains a true square.

            MyPlacementHandler.UpdatePlaceablePosition(
                placementPrefab,
                ref diggableEntityAndInfos,
                equipmentUpdateAspect,
                equipmentUpdateSharedData,
                equipmentUpdateLookupData,
                state);

            // Prevent destruction during prediction re-simulation ticks — The game's native Dig logic 
            // returns early if the target list is empty, so clearing the list is the shallowest way 
            // to express that "this tick has already been dug". If it digs again on every re-simulation, 
            // the client accumulates excessive damage locally, causing multi-hit targets to break prematurely. 
            // When the server rolls this back, fake item drops appear (Measured and observed on 08-28: 
            // a single strike triggered 3 client digs vs. 1 server dig). This has no impact on positions 
            // or previews since they have already been updated by placementCD.

            if (!equipmentUpdateSharedData.isServer &&
                !equipmentUpdateSharedData.isFirstTimeFullyPredictingTick)
            {
                diggableEntityAndInfos.Clear();
                return false;
            }

            // We do not use client prediction on dedicated servers for digging actions that involve 
            // "breakable targets" — destruction and item drop evaluations are server-authoritative, 
            // so if the client breaks them prematurely, ghost drops appear (Observed on 08-28: desyncs 
            // occurred on different strike counts for targets requiring 3 wooden shovel hits vs. 2 copper 
            // shovel hits = variations in damage values per tool tier). This follows the exact same 
            // structural remedy used for object grid placement. Ground digging (tiles only) maintains 
            // prediction to preserve responsive input feel.
            if (!equipmentUpdateSharedData.isServer && !EquipmentSystem_Patch.serverPrefixAlive)
            {
                // We previously removed prediction "only for digs involving entities", but multi-hit 
                // targets are still classified as tiles during the first strike, so they missed that condition 
                // (Observed on 08-28: ghost drops on the copper shovel's first hit). The true rule depends on 
                // "does it not break in a single hit" rather than the target type—and since the client cannot 
                // know that in advance, we unify all tool digging on dedicated servers to rely strictly on server results.
                diggableEntityAndInfos.Clear();
                return false;
            }

            return false;
        }
    }
}
