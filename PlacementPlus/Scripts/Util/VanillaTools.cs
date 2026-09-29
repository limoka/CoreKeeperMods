using System;
using HarmonyLib;
using PlayerEquipment;
using PugTilemap;
using Unity.Collections;

namespace PlacementPlus
{
    // Reverse patch to directly invoke the game's shovel/hoe execution engine (private static Dig) 
    // on the server — the same mechanism that MyPlacementHandler uses to access PlacementHandler's 
    // private methods. On Burst-enabled dedicated servers, our Harmony prefix is never called 
    // on the server, causing the brush "shape" to fail to replicate (Observed on 08-27: despite being 
    // in horizontal mode, the server dug a 9x9 square). In that event, ServerBrushExecutor constructs 
    // a list matching the active shape and hands off execution here.
    [HarmonyPatch]
    public static class VanillaTools
    {
        [HarmonyReversePatch]
        [HarmonyPatch(typeof(ShovelSlot), "Dig")]
        public static void ShovelDig(
            ref NativeList<PlacementHandler.EntityAndInfoFromPlacement> diggableEntityAndInfos,
            in EquipmentUpdateAspect equipmentUpdateAspect,
            EquipmentUpdateSharedData equipmentUpdateSharedData,
            LookupEquipmentUpdateData equipmentUpdateLookupData
        ) =>
            throw new NotImplementedException("It's a stub");

        [HarmonyReversePatch]
        [HarmonyPatch(typeof(HoeSlot), "Dig")]
        public static void HoeDig(
            ref NativeList<PlacementHandler.EntityAndInfoFromPlacement> diggableEntityAndInfos,
            in EquipmentUpdateAspect equipmentUpdateAspect,
            EquipmentUpdateSharedData equipmentUpdateSharedData,
            LookupEquipmentUpdateData equipmentUpdateLookupData
        ) =>
            throw new NotImplementedException("It's a stub");
        
        [HarmonyReversePatch]
        [HarmonyPatch(typeof(PaintToolSlot), "PaintIndexToTileset")]
        public static Tileset PaintIndexToTileset(
            int colorIndex, TileCD tileInfo
        ) =>
            throw new NotImplementedException("It's a stub");
        
    }
}
