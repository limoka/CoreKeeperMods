using HarmonyLib;

namespace KeepFarming
{
    [HarmonyPatch]
    public class ItemSlotsUIContainer_Patch
    {

        [HarmonyPatch(typeof(ItemSlotsUIContainer), nameof(ItemSlotsUIContainer.OnSlotUpdated))]
        [HarmonyPrefix]
        public static bool OnSlotUpdated(ItemSlotsUIContainer __instance, int visibleSlotIndex)
        {
            if (__instance.visibleColumns == 0 || 
                __instance.visibleRows == 0) return false;
            
            int index = __instance.VisibleSlotIndexToInternalSlotIndex(visibleSlotIndex);
            if (index >= __instance.itemSlots.Count) return false;
            
            return true;
        }
    }
}