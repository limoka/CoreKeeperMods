using HarmonyLib;
using UnityExplorer.UI;
using UnityExplorer.UI.Panels;

namespace Mods.UnityExplorer.Scripts.Loader.CoreKeeper.Patch
{
    [HarmonyPatch]
    public class UIManager_Patch
    {
        /// Postfix method for checking if any menu is active in the UI.
        /// This method modifies the result of the original property to include the state of the custom user interface module.
        /// <param name="__result">The original value indicating whether any default inventory or crafting UI is active. This will be modified if a custom menu is active via <see cref="UserInterfaceModule"/>.</param>
        [HarmonyPatch(typeof(UIManager), nameof(UIManager.isAnyInventoryShowing), MethodType.Getter)]
        [HarmonyPostfix]
        public static void OnIsAnyMenuActive(ref bool __result)
        {
            if (__result) return;

            var panels = UE_UIManager.UIPanels;

            foreach (var panel in panels.Values)
            {
                if (!panel.Enabled || !panel.BlockClicks) continue;
                
                __result = true;
                return;
            }
        }
    }
}