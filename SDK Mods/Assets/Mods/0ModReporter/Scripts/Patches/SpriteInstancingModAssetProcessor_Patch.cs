using HarmonyLib;

namespace ModReporter.Scripts.Patches
{
    [HarmonyPatch]
    public static class SpriteInstancingModAssetProcessor_Patch
    {
        [HarmonyPatch(typeof(SpriteInstancingModAssetProcessor), "Done")]
        [HarmonyPrefix]
        public static void AfterModLoadingIsDone()
        {
            ModReporterMod.currentMod = "";
        }
    }
}