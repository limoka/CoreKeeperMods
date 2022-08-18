using HarmonyLib;
using UnityEngine;

namespace PlacementPlus;

public static class ShovelSlot_Patch
{
    [HarmonyPatch(typeof(ShovelSlot), nameof(ShovelSlot.Dig))]
    [HarmonyPrefix]
    public static bool OnDig(ShovelSlot __instance)
    {
        PlacementHandler.SetAllowPlacingAnywhere(false);
        if (BrushExtension.size == 0) return true;

        PlayerController pc = __instance.slotOwner;
        PlacementHandlerDigging handler = __instance.placementHandler.Cast<PlacementHandlerDigging>();
        
        Vector3Int initialPos = handler.bestPositionToPlaceAt;
        Vector3Int worldPos = new Vector3Int(pc.pugMapPosX, 0, pc.pugMapPosZ);
        

        ObjectDataCD item = __instance.objectReference;
        if (item.objectID <= 0) return true;
        if (!PugDatabase.HasComponent<PaintToolCD>(item)) return true;

        BrushExtension.DigGrid(__instance, initialPos + worldPos, handler);

        return false;
    }
    
}