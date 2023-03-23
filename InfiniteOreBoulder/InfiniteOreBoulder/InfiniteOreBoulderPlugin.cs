using BepInEx;
using BepInEx.Unity.IL2CPP;
using BepInEx.Logging;
using CoreLib;
using CoreLib.Submodules.ModSystem;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;

namespace InfiniteOreBoulder
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    [CoreLibSubmoduleDependency(nameof(SystemModule))]
    public class InfiniteOreBoulderPlugin : BasePlugin
    {
        public static ManualLogSource logger;

        public override void Load()
        {
            // Plugin startup logic
            logger = Log;
            
            SystemModule.RegisterSystem<InfiniteOreBoulderSystem>();
            
            Harmony harmony = new Harmony(PluginInfo.PLUGIN_GUID);
            harmony.PatchAll();
            logger.LogInfo($"{PluginInfo.PLUGIN_NAME} mod is loaded!");
        }
    }
}