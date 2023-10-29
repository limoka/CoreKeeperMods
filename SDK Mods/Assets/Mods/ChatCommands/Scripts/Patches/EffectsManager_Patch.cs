using ChatCommands.Chat.Commands;
using HarmonyLib;

namespace ChatCommands.Chat
{
    [HarmonyPatch]
    public static class EffectsManager_Patch
    {
        [HarmonyPatch(typeof(PlayerController), nameof(PlayerController.AE_FootStep))]
        [HarmonyPrefix]
        public static bool AE_FootStep()
        {
            return HideUICommand.GetState("player");
        }
    }
}