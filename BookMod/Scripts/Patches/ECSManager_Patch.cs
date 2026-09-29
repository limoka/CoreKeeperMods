using HarmonyLib;

namespace BookMod.Patches
{
    [HarmonyPatch]
    public class ECSManager_Patch
    {

        [HarmonyPatch(typeof(ECSManager), nameof(ECSManager.Init))]
        [HarmonyPrefix]
        public static void OnInit(ECSManager __instance)
        {
            BookMod.Log.LogInfo("On ECSManager Init");
            BookMod.AddBooks();
        }
    }
}