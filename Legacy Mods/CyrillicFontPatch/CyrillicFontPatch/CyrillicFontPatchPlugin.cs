using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using BepInEx.Logging;
using CoreLib;
using CoreLib.Submodules.ModResources;
using HarmonyLib;
using UnityEngine;

namespace RussianFontPatch
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    [BepInDependency(CoreLibPlugin.GUID)]
    [CoreLibSubmoduleDependency(nameof(ResourcesModule))]
    public class CyrillicFontPatchPlugin : BasePlugin
    {
        public static ManualLogSource logger;
        private static string pluginfolder;

        public override void Load()
        {
            // Plugin startup logic
            logger = Log;
            Harmony harmony = new Harmony(PluginInfo.PLUGIN_GUID);
            harmony.PatchAll();
            
            pluginfolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            
            logger.LogInfo($"{PluginInfo.PLUGIN_NAME} mod is loaded!");
        }
        
        internal static Texture2D LoadTexture(string filePath)
        {
            filePath = Path.Join(pluginfolder, filePath);
            // Load a PNG or JPG file from disk to a Texture2D
            // Returns null if load fails

            if (File.Exists(filePath))
            {
                byte[] fileData = File.ReadAllBytes(filePath);
                Texture2D tex2D = new Texture2D(2, 2);
                if (tex2D.LoadImage(fileData))
                {
                    // Load the imagedata into the texture (size is set automatically)
                    /*if (tex2D.format != TextureFormat.RGBA32)
                    {
                        var convertedTex = new Texture2D(tex2D.width, tex2D.height, TextureFormat.RGBA32, false);
                        convertedTex.SetPixels32(tex2D.GetPixels32());
                        return convertedTex;
                    }*/
                    tex2D.filterMode = FilterMode.Point;
                    return tex2D; // If data = readable -> return texture
                }
            }

            logger.LogWarning($"Failed to find asset at {filePath}");
            return null; // Return null if load failed
        }
    }
}