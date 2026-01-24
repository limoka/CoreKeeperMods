using System;
using System.Linq;
using HarmonyLib;
using PugMod;
/*
namespace KeepFarming
{
    [HarmonyPatch]
    public class SpriteAssetManager_Patch
    {
        private static bool doneModReload = false;

        [HarmonyPatch(typeof(SpriteAssetManager), "ClearAdditionalAssets")]
        [HarmonyPrefix]
        public static bool OnClearAdditionalAssets()
        {
            return doneModReload;
        }

        [HarmonyPatch(typeof(SpriteAssetManager), "LoadAndCompile")]
        [HarmonyPrefix]
        public static void OnLoad()
        {
            KeepFarmingMod.Log.LogInfo("Before SpriteAssetManager.LoadAndCompile()!");
        }

        internal static void ReloadAssets()
        {
            KeepFarmingMod.Log.LogInfo("Start SpriteAssetManager reload!");
            var method = typeof(SpriteAssetManager)
                .GetMembersChecked()
                .FirstOrDefault(info => info.GetNameChecked().Equals("LoadAndCompile"));
            if (method == null)
                throw new MissingFieldException(typeof(SpriteAssetManager).GetNameChecked(), "LoadAndCompile");

            doneModReload = true;
            SpriteAssetManager.AddAdditionalAssets(null, ECSManager_Patch.gradientMaps.Values.ToList(), null);
            
            API.Reflection.Invoke(method, null);
        }
    }
}*/