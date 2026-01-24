using System.Linq;
using CoreLib;
using CoreLib.Submodule.Command;
using CoreLib.Submodule.Command.Data;
using CoreLib.Submodule.Command.Interface;
using PugMod;
using UnityEngine;

namespace ChatCommands
{
    public class ChatCommandsMod : IMod
    {
        public const string VERSION = "2.2.0";
        public const string NAME = "Chat Commands";
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

            CoreLibMod.LoadSubmodule(typeof(CommandModule));
            CommandModule.AddCommands(modInfo.ModId, NAME);

            Debug.Log($"[{NAME}]: Mod loaded successfully");
        }

        public void Init() { }

        public void Shutdown() { }

        public void ModObjectLoaded(Object obj) { }

        public void Update() { }

        public static LoadedMod GetModInfo(IMod mod)
        {
            return API.ModLoader.LoadedMods.FirstOrDefault(modInfo => modInfo.Handlers.Contains(mod));
        }
    }
}