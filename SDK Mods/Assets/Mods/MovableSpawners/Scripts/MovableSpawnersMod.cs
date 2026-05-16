using CoreLib.Util.Extension;
using PugMod;
using UnityEngine;
using Logger = CoreLib.Util.Logger;

namespace MovableSpawners
{
    public class MovableSpawnersMod : IMod
    {
        internal static Logger Log = new Logger(NAME);
        internal const string Textures = "Assets/Mods/MovableSpawners/Textures/";
        
        public const string VERSION = "2.0.1";
        public const string NAME = "Movable Spawners";
        private static LoadedMod modInfo;

        internal static AssetBundle AssetBundle => modInfo.AssetBundles[0];
        
        internal static Sprite[] icons;
        internal static Sprite errorIcon;

        public void EarlyInit()
        {
            Log.LogInfo($"Mod version: {VERSION}");
            modInfo = this.GetModInfo();
            if (modInfo == null)
            {
                Log.LogError($"Failed to load {NAME}: mod metadata not found!");
                return;
            }

            if (modInfo.AssetBundles.Count == 0)
            {
                Log.LogError($"Failed to load {NAME}: Asset bundle missing!");
                return;
            }

            icons = AssetBundle.LoadAssetWithSubAssets<Sprite>(Textures + "boss-rune-icons.png");
            errorIcon = AssetBundle.LoadAsset<Sprite>(Textures + "icon-small.png");

            System.Array.Sort(
                icons,
                (a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal)
            );
            
            Log.LogInfo($"Movable Spawners Mod loaded successfully");
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