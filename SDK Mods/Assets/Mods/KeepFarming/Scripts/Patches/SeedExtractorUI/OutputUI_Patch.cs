using CoreLib.Util.Extensions;
using HarmonyLib;
using UnityEngine;
// ReSharper disable RedundantAssignment

namespace KeepFarming
{
    [HarmonyPatch]
    public static class OutputUI_Patch
    {
        [HarmonyPatch(typeof(OutputUI), nameof(OutputUI.MAX_COLUMNS), MethodType.Getter)]
        [HarmonyPostfix]
        public static void OnGetMaxColumns(ref int __result)
        {
            __result = 2;
        }

        [HarmonyPatch(typeof(OutputUI), nameof(OutputUI.Init))]
        [HarmonyPrefix]
        public static void OnInit(OutputUI __instance)
        {
            if (!__instance.GetValue<bool>("initDone"))
            {
                var itemSlots = __instance.itemSlots;
                if (itemSlots.Count != 1) return;

                var firstSlot = itemSlots[0];
                var secondSlotGo = Object.Instantiate(firstSlot.gameObject, firstSlot.transform.parent);
                secondSlotGo.transform.localPosition += new Vector3(1.375f, 0, 0);
                var secondSlot = secondSlotGo.GetComponent<SlotUIBase>();
                itemSlots.Add(secondSlot);
            }
        }


        [HarmonyPatch(typeof(OutputUI), nameof(OutputUI.ShowContainerUI))]
        [HarmonyPrefix]
        public static void OnShowUI(OutputUI __instance)
        {
            UpdateContainerSize(__instance);
        }

        private static void UpdateContainerSize(OutputUI outputUI)
        {
            PlayerController player = Manager.main.player;
            if (player == null) return;

            var inventoryHandler = outputUI.GetInventoryHandler();
            if (inventoryHandler == null) return;

            outputUI.SetValue("visibleColumns", inventoryHandler.columns);

            if (inventoryHandler.columns == 1)
            {
                outputUI.itemSlots[1].gameObject.SetActive(false);
            }
            else
            {
                outputUI.itemSlots[1].visibleSlotIndex = 1;
                outputUI.itemSlots[1].gameObject.SetActive(true);
                outputUI.itemSlots[1].UpdateSlot();
            }
        }
    }
}