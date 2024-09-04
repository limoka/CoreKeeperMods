using HarmonyLib;
using UnityEngine;

namespace PlacementPlus
{
    [HarmonyPatch]
    public static class ShovelSlot_Patch
    {
       /* [HarmonyPatch(typeof(ShovelSlot), "Dig")]
        [HarmonyPrefix]
        public static bool OnDig(ShovelSlot __instance)
        {
            PlacementHandler.SetAllowPlacingAnywhere(false);
            if (BrushExtension.size == 0 ||
                BrushExtension.mode == BrushMode.NONE) return true;

            PlayerController pc = __instance.slotOwner;
            PlacementHandlerDigging handler = __instance.placementHandler;
        
            Vector3Int initialPos = handler.bestPositionToPlaceAt;

            ObjectDataCD item = pc.GetHeldObject();
            if (item.objectID <= 0) return true;

            ObjectInfo info = PugDatabase.GetObjectInfo(item.objectID);
            if (info.objectType != ObjectType.Shovel) return true;
        
            BrushExtension.DigGrid(__instance, initialPos, handler);

            return false;
        }*/
    
    }
}