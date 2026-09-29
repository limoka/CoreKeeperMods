using HarmonyLib;

namespace KeepFarming
{
    [HarmonyPatch]
    public class Plant_Patch
    {
        [HarmonyPatch(typeof(SpriteSkinFromEntityAndSeason), "UpdateSkin")]
        [HarmonyPrefix]
        public static void OnUpdateSkin(SpriteSkinFromEntityAndSeason __instance, ref int variation)
        {
            var plant = __instance.gameObject.GetComponent<Plant>();
            if (plant == null) return;
            
            if (variation == 4)
            {
                variation -= 2;
            }
        }
    }
}