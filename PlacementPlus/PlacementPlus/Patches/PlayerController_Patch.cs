using HarmonyLib;

namespace PlacementPlus
{
    [HarmonyPatch]
    public static class PlayerController_Patch
    {
        [HarmonyPatch(typeof(PlayerController), nameof(PlayerController.EquipSlot))]
        [HarmonyPostfix]
        public static void OnChangeItem()
        {
            BrushExtension.CheckSize();
        }
    }
}