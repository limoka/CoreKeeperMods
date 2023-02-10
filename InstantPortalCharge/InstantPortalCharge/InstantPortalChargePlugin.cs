using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;

namespace InstantPortalCharge
{
    [BepInPlugin(MODGUID, MODNAME, VERSION)]
    public class InstantPortalChargePlugin : BasePlugin
    {
        public const string MODNAME = "Instant Portal Charge";

        public const string MODGUID = "org.kremnev8.plugin.InstantPortalChargePlugin";

        public const string VERSION = "1.2.1";

        public static ManualLogSource logger;

        public override void Load()
        {
            logger = Log;
            
            ClassInjector.RegisterTypeInIl2Cpp<PortalChargeSystem>();
            AddComponent<PortalChargeSystem>();
            
            Harmony harmony = new Harmony(MODGUID);
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            logger.LogInfo("Instant Portal Charge plugin is loaded!");
        }
    }
}