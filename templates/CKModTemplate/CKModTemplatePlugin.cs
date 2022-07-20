using BepInEx;
using BepInEx.IL2CPP;
using BepInEx.Logging;
using HarmonyLib;

namespace CKModTemplate
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    [BepInDependency("com.le4fless.corelib")]
    [BepInProcess("CoreKeeper.exe")]
    public class CKModTemplatePlugin : BasePlugin
    {
        public static ManualLogSource logger;
        public override void Load()
        {
            // Plugin startup logic
            logger = Log;
            logger.LogInfo($"{PluginInfo.PLUGIN_NAME} mod is loaded!");
            Harmony harmony = new Harmony(PluginInfo.PLUGIN_GUID);
            harmony.PatchAll();
        }
    }
}
