using HarmonyLib;
using UnityEngine;

namespace KeepFarming
{
    [HarmonyPatch]
    public static class ProcessResourcesCraftingUI_Patch
    {
        private static int lastOutputCount = -1;
        

        [HarmonyPatch(typeof(ProcessResourcesCraftingUI), nameof(ProcessResourcesCraftingUI.UpdateAllCraftingUI))]
        [HarmonyPostfix]
        public static void OnUpdateAllCraftingUI(ProcessResourcesCraftingUI __instance)
        {
            int visibleSlots = __instance.outputUI.GetInventoryHandler().columns;
            for (int i = 0; i < visibleSlots; i++)
            {
                __instance.outputUI.OnSlotUpdated(i);
            }

            if (lastOutputCount == visibleSlots) return;
            
            var backgroundTrans = __instance.root.transform.Find("Background");
            if (backgroundTrans == null) return;

            var backgroundSR = backgroundTrans.GetComponent<SpriteRenderer>();
            var backgroundCollider = backgroundTrans.GetComponent<BoxCollider>();
            
            var srSize = backgroundSR.size;
            var colliderSize = backgroundCollider.size;
            
            if (visibleSlots == 1)
            {
                srSize.x = 4.6875f;
                colliderSize.x = 4.6875f;
                backgroundTrans.localPosition = new Vector3(0.03125f, 0, 0);
            }
            else
            {
                srSize.x = 5.5f;
                colliderSize.x = 5.5f;
                backgroundTrans.localPosition = new Vector3(0.6875f, 0, 0);
            }

            backgroundSR.size = srSize;
            backgroundCollider.size = colliderSize;
            lastOutputCount = visibleSlots;
        }
    }
}