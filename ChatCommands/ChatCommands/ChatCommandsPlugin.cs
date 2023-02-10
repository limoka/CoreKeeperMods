using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using ChatCommands.Chat;
using CoreLib;
using CoreLib.Submodules.ChatCommands;
using CoreLib.Submodules.CustomEntity;
using CoreLib.Util;
using HarmonyLib;

namespace ChatCommands
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    [BepInDependency(CoreLibPlugin.GUID)]
    [CoreLibSubmoduleDependency(nameof(CommandsModule))]
    [BepInProcess("CoreKeeper.exe")]
    public class ChatCommandsPlugin : BasePlugin
    {
        public static ManualLogSource logger;
        public override void Load()
        {
            // Plugin startup logic
            logger = Log;
            
            CommandsModule.AddCommands(Assembly.GetExecutingAssembly(), PluginInfo.PLUGIN_NAME);
            
            NativeTranspiler.PatchAll(typeof(MapUI_Patch));
            
            Harmony harmony = new Harmony(PluginInfo.PLUGIN_GUID);
            harmony.PatchAll();
            
            logger.LogInfo($"{PluginInfo.PLUGIN_NAME} mod is loaded!");
        }
    }
}
