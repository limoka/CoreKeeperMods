using HarmonyLib;
using ModReporter.UI;
using UnityEngine;

namespace ModReporter.Scripts.Patches
{
    [HarmonyPatch]
    public static class TitleScreenAnimator_Patch
    {
        [HarmonyPatch(typeof(TitleScreenAnimator), "OpenMenu")]
        [HarmonyPostfix]
        public static void OnOpenMenu()
        {
            var prefab = ModReporterMod.AssetBundle.LoadAsset<GameObject>("Assets/Mods/0ModReporter/Prefab/ModReporterUI.prefab");
            var windowGO = Object.Instantiate(prefab);
            Object.DontDestroyOnLoad(windowGO);
            
            ModReporterMod.reporterWindow = windowGO.GetComponentInChildren<ModReporterWindow>();
            ModReporterMod.hoverTip = windowGO.GetComponentInChildren<HoverTip>();
            
            ModReporterMod.hoverTip.HideTip();
            ModReporterMod.reporterWindow.FillUp();
            ModReporterMod.reporterWindow.Show();
        }
    }
}