using HarmonyLib;

namespace Mods.CustomizeWaterPriority.Scripts
{
    [HarmonyPatch]
    public class WaterSpreadingSystem_Patch
    {
        [HarmonyPatch(typeof(WaterSpreadingSystem), "OnUpdate")]
        [HarmonyPrefix]
        public static bool OnOnUpdate()
        {
            return false;
        }
    }
}