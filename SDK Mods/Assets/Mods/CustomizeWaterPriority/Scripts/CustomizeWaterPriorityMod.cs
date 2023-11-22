using System.Linq;
using CoreLib.Data.Configuration;
using PugMod;
using PugTilemap;
using UnityEngine;

namespace Mods.CustomizeWaterPriority.Scripts
{
    public class CustomizeWaterPriorityMod : IMod
    {
        public const string VERSION = "1.0.0";
        public const string NAME = "Customize Water Priority";
        private static LoadedMod modInfo;
        
        internal static ConfigEntry<Tileset> highestPriorityTileset;
        internal static ConfigFile configFile;

        public void EarlyInit()
        {
            Debug.Log($"[{NAME}]: Mod version: {VERSION}");
            modInfo = GetModInfo(this);
            if (modInfo == null)
            {
                Debug.Log($"[{NAME}]: Failed to load {NAME}: mod metadata not found!");
                return;
            }

            configFile = new ConfigFile("CustomizeWaterPriority/Config.cfg", true, modInfo);
            highestPriorityTileset = configFile.Bind("General", "HighestPriorityTileset", Tileset.Dirt, "Choose which tileset will have highest priority");
            
            Debug.Log($"[{NAME}]: Current highest priority: {highestPriorityTileset.Value}");

            Debug.Log($"[{NAME}]: Mod loaded successfully");
        }
        
        public static LoadedMod GetModInfo(IMod mod)
        {
            return API.ModLoader.LoadedMods.FirstOrDefault(modInfo => modInfo.Handlers.Contains(mod));
        }

        public void Init()
        {
        }

        public void Shutdown()
        {
        }

        public void ModObjectLoaded(Object obj)
        {
        }

        public void Update()
        {
        }
    }
}