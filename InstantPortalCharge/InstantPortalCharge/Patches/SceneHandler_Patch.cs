using HarmonyLib;
using Unity.Entities;

namespace InstantPortalCharge
{

    [HarmonyPatch]
    public static class SceneHandler_Patch
    {
        [HarmonyPatch(typeof(SceneHandler), nameof(SceneHandler.Start))]
        [HarmonyPostfix]
        public static void OnSceneStart()
        {
            World world = Manager.ecs.ServerWorld;
            if (world != null)
            {
                InstantPortalChargePlugin.logger.LogDebug("Loading Portal charge system!");
                PortalChargeSystem.instance.OnServerStarted(world);
            }
        }
        
    }
}