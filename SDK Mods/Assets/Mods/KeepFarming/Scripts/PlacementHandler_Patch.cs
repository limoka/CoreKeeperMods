using HarmonyLib;

namespace KeepFarming
{
    [HarmonyPatch]
    public static class PlacementHandler_Patch
    {
        [HarmonyPatch(typeof(PlacementHandlerWatering), "CanPlaceObjectAtPosition")]
        [HarmonyPostfix]
        private static void CanPlacePatch(int __result)
        {
            canPlace = __result;
        }
        
        [HarmonyPatch(typeof(WaterCanSlot), "PlaceItem")]
        [HarmonyPostfix]
        private static void WateringPatch()
        {
            if (canPlace >= 1)
            {
                PlayerController currentPlayer = Manager.main.player;
                currentPlayer.AddSkill(SkillID.Gardening, canPlace);
            }
        }

        private static int canPlace;
    }
}