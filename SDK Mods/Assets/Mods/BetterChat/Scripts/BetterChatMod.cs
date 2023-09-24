using System.Globalization;
using System.Linq;
using PugMod;
using UnityEngine;

namespace BetterChat.Scripts
{
    public class BetterChatMod : IMod
    {
        public const string VERSION = "1.0.0";
        public const string NAME = "Better Chat";
        private static LoadedMod modInfo;

        internal static AssetBundle AssetBundle => modInfo.AssetBundles[0];

        internal static float chatWindowWidth = 10;

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

        private static void LoadConfigs()
        {
            string ID = NAME.Replace(" ", "");
            if (API.Config.TryGet(ID, "ChatWindow", "Width", out string textValue))
            {
                if (float.TryParse(textValue, out float value))
                {
                    chatWindowWidth = value;
                }
                else
                {
                    Debug.Log($"[{NAME}]: {textValue} is not a valid number!");
                }
            }
            else
            {
                textValue = chatWindowWidth.ToString(CultureInfo.InvariantCulture);
                API.Config.Set(ID, "ChatWindow", "Width", textValue);
            }
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