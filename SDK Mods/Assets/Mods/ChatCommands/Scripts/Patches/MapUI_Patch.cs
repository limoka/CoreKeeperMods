using HarmonyLib;

namespace ChatCommands.Chat
{
    [HarmonyPatch]
    public static class MapUI_Patch
    {
        public static float bigRevealRadius = 12;
        
        [HarmonyPatch(typeof(MapUI), "RevealDistance", MethodType.Getter)]
        [HarmonyPostfix]
        public static void OnGetRevealDistance(MapUI __instance, ref float __result)
        {
            if (__instance.revealLargeMap &&
                bigRevealRadius != 12)
            {
                __result = bigRevealRadius;
            }
        }
    }
}