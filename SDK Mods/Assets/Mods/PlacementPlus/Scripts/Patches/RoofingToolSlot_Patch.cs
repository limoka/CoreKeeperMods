using HarmonyLib;
using UnityEngine;

namespace PlacementPlus
{
    [HarmonyPatch]
    public static class RoofingToolSlot_Patch
    {
        [HarmonyPatch(typeof(RoofingToolSlot), "ToggleRoof")]
        [HarmonyPrefix]
        public static bool OnToggleRoof(RoofingToolSlot __instance)
        {
            PlacementHandler.SetAllowPlacingAnywhere(false);
            if (BrushExtension.size == 0 ||
                BrushExtension.mode == BrushMode.NONE) return true;

            PlayerController pc = __instance.slotOwner;
            PlacementHandlerRoofingTool handler = __instance.placementHandler;
            
            Vector3Int initialPos = handler.bestPositionToPlaceAt;

            ObjectDataCD item = pc.GetHeldObject();
            if (item.objectID <= 0) return true;

            ObjectInfo info = PugDatabase.GetObjectInfo(item.objectID);
            if (info.objectType != ObjectType.RoofingTool) return true;
        
            BrushExtension.RoofGrid(__instance, initialPos);

            return false;
        }
    }
}