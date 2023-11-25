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
        [HarmonyPrefix]
        private static void WateringPatchBefore(WaterCanSlot __instance)
        {
            prevAmount = __instance.objectData.amount;
        }

        [HarmonyPatch(typeof(WaterCanSlot), "PlaceItem")]
        [HarmonyPostfix]
        private static void WateringPatchAfter(WaterCanSlot __instance)
        {
            if (canPlace >= 1)
            {
                PlayerController currentPlayer = Manager.main.player;
                var curAmount = __instance.objectData.amount;
                if (curAmount < prevAmount)
                {
                    currentPlayer.AddSkill(SkillID.Gardening, 1);
                }
            }
        }

        private static int canPlace;
        private static int prevAmount;
    }
}