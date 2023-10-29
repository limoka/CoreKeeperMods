using HarmonyLib;

namespace PlacementPlus
{
    [HarmonyPatch]
    public static class PlayerController_Patch
    {
        [HarmonyPatch(typeof(PlayerController), nameof(PlayerController.EquipSlot))]
        [HarmonyPostfix]
        public static void OnChangeItem(PlayerController __instance)
        {
            BrushExtension.CheckSize();
            var slot = __instance.GetEquippedSlot();
            if (slot is PlaceObjectSlot placeSlot)
            {
                BrushExtension.TryRotate(placeSlot);
            }
        }
    }
}