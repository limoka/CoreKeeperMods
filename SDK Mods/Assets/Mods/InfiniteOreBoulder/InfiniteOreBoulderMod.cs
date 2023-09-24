using System.Linq;
using PugMod;
using Unity.Burst;
using UnityEngine;
using UnityEngine.Scripting;

[Preserve]
public class InfiniteOreBoulderMod : IMod
{
    public const string VERSION = "2.2.1";
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

        if (Application.platform == RuntimePlatform.WindowsPlayer)
        {
            string directory = API.ModLoader.GetDirectory(modInfo.ModId);
            BurstRuntime.LoadAdditionalLibrary($"{directory}/InfiniteOreBoulder_burst_generated.dll");
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
