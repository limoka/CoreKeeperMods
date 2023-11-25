using HarmonyLib;

namespace KeepFarming
{
    [HarmonyPatch]
    public class Plant_Patch
    {
        [HarmonyPatch(typeof(Plant), "UpdateSkin")]
        [HarmonyPrefix]
        public static void OnUpdateSkin(ref int currentVariation)
        {
            if (currentVariation == 4)
            {
                currentVariation -= 2;
            }
        }
    }
}