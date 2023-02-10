using HarmonyLib;
using Unity.Entities;

namespace InfiniteOreBoulder.Patches
{
    [HarmonyPatch]
    public static class SceneHandler_Patch
    {
        [HarmonyPatch(typeof(SceneHandler), nameof(SceneHandler.SetSceneHandlerReady))]
        [HarmonyPostfix]
        public static void OnSceneStart()
        {
            World world = Manager.ecs.ServerWorld;
            if (world != null)
            {
                InfiniteOreBoulderPlugin.logger.LogDebug("Loading infinite boulder system!");
                InfiniteOreBoulderSystem.instance.OnServerStarted(world);
            }
        }
    }
}