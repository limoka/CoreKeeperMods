using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using ChatCommands.Chat;
using CoreLib;
using CoreLib.Submodules.ChatCommands;
using CoreLib.Submodules.JsonLoader;
using CoreLib.Util;
using HarmonyLib;

#if IL2CPP
using BepInEx.Unity.IL2CPP;
#endif

namespace ChatCommands
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    [BepInDependency(CoreLibPlugin.GUID)]
    [CoreLibSubmoduleDependency(nameof(CommandsModule), nameof(JsonLoaderModule))]
    [BepInProcess("CoreKeeper.exe")]
#if IL2CPP
    public class ChatCommandsPlugin : BasePlugin
#else
    public class ChatCommandsPlugin : BaseUnityPlugin
#endif
    {
        public static ManualLogSource logger;
        
#if IL2CPP
        public override void Load()
        {
            logger = Log;
#else
        public void Awake()
        {
            logger = base.Logger;
#endif            
            CommandsModule.AddCommands(Assembly.GetExecutingAssembly(), PluginInfo.PLUGIN_NAME);
#if IL2CPP
            NativeTranspiler.PatchAll(typeof(MapUI_Patch));
#endif
            
            Harmony harmony = new Harmony(PluginInfo.PLUGIN_GUID);
            harmony.PatchAll();
            
            logger.LogInfo($"{PluginInfo.PLUGIN_NAME} mod is loaded!");
        }
    }
}
