using System.Reflection;
using BepInEx;
using BepInEx.IL2CPP;
using BepInEx.Logging;
using HarmonyLib;
using UnhollowerRuntimeLib;

namespace InstantPortalCharge
{
    [BepInPlugin(MODGUID, MODNAME, VERSION)]
    public class InstantPortalChargePlugin : BasePlugin
    {
        public const string MODNAME = "Instant Portal Charge";

        public const string MODGUID = "org.kremnev8.plugin.InstantPortalChargePlugin";

        public const string VERSION = "1.0.0";

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