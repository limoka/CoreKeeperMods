using HarmonyLib;
using UnityEngine;

namespace ModReporter.Scripts.Patches
{
    [HarmonyPatch]
    public static class MenuManager_Patch
    {
        [HarmonyPatch(typeof(MenuManager), nameof(MenuManager.IsAnyMenuActive))]
        [HarmonyPostfix]
        public static void OnIsAnyMenuActive(ref bool __result)
        {
            if (ModReporterMod.reporterWindow == null) return;
            if (ModReporterMod.reporterWindow.isVisible)
            {
                __result = true;
            }
        }
        
        [HarmonyPatch(typeof(UIManager), "LateUpdate")]
        [HarmonyPostfix]
        public static void OnLateUpdate()
        {
            if (ModReporterMod.reporterWindow == null) return;
            if (ModReporterMod.reporterWindow.isVisible)
            {
                Cursor.visible = true;
            }
        }
    }
}