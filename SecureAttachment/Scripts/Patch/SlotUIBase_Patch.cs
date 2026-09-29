using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace SecureAttachment.Patch
{
    [HarmonyPatch]
    public class SlotUIBase_Patch
    {
        [HarmonyPatch(typeof(SlotUIBase), nameof(SlotUIBase.GetHoverStats), typeof(ContainedObjectsBuffer),
            typeof(bool), typeof(bool))]
        [HarmonyPostfix]
        public static void OnGetHoverStats(
            SlotUIBase __instance,
            ContainedObjectsBuffer containedObject,
            ref List<TextAndFormatFields> __result
        )
        {
            if (!PugDatabase.HasComponent<WrenchCD>(containedObject.objectData)) return;

            var pc = Manager.main.player;
            if (pc == null) return;

            var entityManager = pc.world.EntityManager;

            if (!entityManager.HasComponent<WrenchStateCD>(pc.entity)) return;
            var state = entityManager.GetComponentData<WrenchStateCD>(pc.entity);

            var modeMessage = state.mode switch
            {
                WrenchMode.Auto => "SecureAttachment/WrenchAutoMode",
                WrenchMode.Mount => "SecureAttachment/WrenchMountMode",
                WrenchMode.Unmount => "SecureAttachment/WrenchUnmountMode",
                _ => ""
            };

            __result ??= new List<TextAndFormatFields>(2);
            __result.Add(new TextAndFormatFields
            {
                text = modeMessage,
                color = Color.yellow
            });

            var wrench = PugDatabase.GetComponent<WrenchCD>(containedObject.objectData);

            var wrenchModeMessage = wrench.mountType switch
            {
                MountType.IncreaseHP => "SecureAttachment/IncreaseHPMounting",
                MountType.Indestructible => "SecureAttachment/IndestructibleMounting",
                _ => ""
            };

            if (wrenchModeMessage.Length > 0)
            {
                __result.Add(new TextAndFormatFields
                {
                    text = wrenchModeMessage
                });
            }
        }
    }
}