using BookMod.UI;
using CoreLib.UserInterface;
using CoreLib.UserInterface.Patches;
using HarmonyLib;

namespace BookMod.Patches
{
    [HarmonyPatch]
    public class MenuManager_Patch
    {
        [HarmonyPatch(typeof(UIManager), nameof(UIManager.isAnyInventoryShowing), MethodType.Getter)]
        [HarmonyPostfix]
        public static void OnIsAnyMenuActive(ref bool __result)
        {
            if (__result) return;

            __result |= UserInterfaceModule.GetCurrentInterface<BookUI>() != null;
        }
    }
}