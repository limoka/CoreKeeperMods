using System.Linq;
using PugMod;
using Unity.Burst;
using UnityEngine;
using UnityEngine.Scripting;

[Preserve]
public class InfiniteOreBoulderMod : IMod
{
    public const string VERSION = "2.2.4";
    public const string NAME = "Inifinte Ore Boulder";
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

        var platform = GetPlatformString();
        if (platform != null)
        {
            string directory = API.ModLoader.GetDirectory(modInfo.ModId);
            string ID = NAME.Replace(" ", "");
            string fileExtension = GetPlatformExtension(platform);
            bool success = BurstRuntime.LoadAdditionalLibrary($"{directory}/{ID}_burst_generated_{platform}.{fileExtension}");
            if (!success)
                Debug.LogWarning($"[{NAME}]: Failed to load burst assembly");
        }
        Debug.Log($"[{NAME}]: Mod loaded successfully");
    }
    
    public static string GetPlatformString()
    {
        switch (Application.platform)
        {
            case RuntimePlatform.WindowsPlayer:
            case RuntimePlatform.WindowsServer:
                return "Windows";
            case RuntimePlatform.LinuxPlayer:
            case RuntimePlatform.LinuxServer:
                return "Linux";
        }

        return null;
    }

    public static string GetPlatformExtension(string platform)
    {
        if (platform == "Windows")
            return "dll";
        if (platform == "Linux")
            return "so";
        return "";
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
