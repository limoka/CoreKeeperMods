using System.Linq;
using PugMod;
using Unity.Burst;
using UnityEngine;

namespace EquipmentKeeper
{
    public class EquipmentKeeperMod : IMod
    {
        public const string VERSION = "1.1.0";
        public const string NAME = "Equipment Keeper";
        private LoadedMod modInfo;

        public void EarlyInit()
        {
            Debug.Log($"[{NAME}]: Mod version: {VERSION}");
            modInfo = GetModInfo(this);
            if (modInfo == null)
            {
                Debug.Log($"[{NAME}]: Failed to load {NAME}: mod metadata not found!");
                return;
            }

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