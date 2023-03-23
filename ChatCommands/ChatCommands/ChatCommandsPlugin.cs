using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using ChatCommands.Chat;
using CoreLib;
using CoreLib.Submodules.ChatCommands;
using CoreLib.Submodules.JsonLoader;
using CoreLib.Util;
using HarmonyLib;
using BepInEx.Unity.IL2CPP;

namespace ChatCommands
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    [BepInDependency(CoreLibPlugin.GUID)]
    [CoreLibSubmoduleDependency(nameof(CommandsModule), nameof(JsonLoaderModule))]
    [BepInProcess("CoreKeeper.exe")]

    public class ChatCommandsPlugin : BasePlugin
    {
        public static ManualLogSource logger;
        

        public override void Load()
        {
            logger = Log;
            CommandsModule.AddCommands(Assembly.GetExecutingAssembly(), PluginInfo.PLUGIN_NAME);
            NativeTranspiler.PatchAll(typeof(MapUI_Patch));

            
            Harmony harmony = new Harmony(PluginInfo.PLUGIN_GUID);
            harmony.PatchAll();
            
            logger.LogInfo($"{PluginInfo.PLUGIN_NAME} mod is loaded!");
        }
    }
}
