using System;
using System.Collections.Generic;
using System.Linq;
using ModIO;
using ModIO.Implementation.API.Objects;
using ModReporter.UI;
using PugMod;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ModReporter.Scripts
{
    public class ModReporterMod : IMod
    {
        public const string VERSION = "1.0.0";
        public const string NAME = "Mod Reporter";
        private static LoadedMod thisModInfo;

        internal static AssetBundle AssetBundle => thisModInfo.AssetBundles[0];
        internal static ModReporterWindow reporterWindow;
        internal static HoverTip hoverTip;

        public static Dictionary<string, ModInfo> modInfos = new Dictionary<string, ModInfo>();

        public static Action<string> modStateChanged;

        public static string currentMod = "";

        public void EarlyInit()
        {
            Debug.Log($"[{NAME}]: Mod version: {VERSION}");
            thisModInfo = GetModInfo(this);
            if (thisModInfo == null)
            {
                Debug.Log($"[{NAME}]: Failed to load {NAME}: mod metadata not found!");
                return;
            }

            foreach (LoadedMod loadedMod in API.ModLoader.LoadedMods)
            {
                ModInfo modInfo = new ModInfo()
                {
                    modName = loadedMod.Metadata.name,
                    modId = loadedMod.ModId,
                    // negative id's mean it's local
                    installation = loadedMod.ModId > 0 ? InstallationKind.MOD_IO : InstallationKind.MANUAL,
                    dependencies = loadedMod.Metadata.dependencies
                };

                if (!modInfos.ContainsKey(loadedMod.Metadata.name))
                {
                    modInfos[loadedMod.Metadata.name] = modInfo;
                }
            }
            
            SubscribedMod[] subscribedMods = ModIOUnity.GetSubscribedMods(out var result);
            if (result.Succeeded())
            {
                foreach (SubscribedMod mod in subscribedMods)
                {
                    ModInfo modInfo = GetOrCreateModInfo(mod.modProfile.name, out bool wasCreated);
                    modInfo.installation |= InstallationKind.MOD_IO;

                    if (!mod.enabled && modInfo.installation == InstallationKind.MOD_IO)
                        modInfo.status = ModStatus.DISABLED;
                    else if (mod.status != SubscribedModStatus.Installed)
                        modInfo.status = ModStatus.NOT_DOWNLOADED;

                    if (wasCreated && modInfo.status == ModStatus.OK)
                    {
                        modInfo.status = ModStatus.CORRUPTED;
                    }

                    if (modInfo.dependencies.Count == 0)
                    {
                        DetermineDependencies(mod, modInfo);
                    }
                }
            }

            foreach (ModInfo mod in modInfos.Values)
            {
                CheckDependencies(mod, mod.modName, true);
            }

            Application.logMessageReceived += OnLog;
            Debug.Log($"[{NAME}]: Mod reported started!");
        }

        public static LoadedMod GetModInfo(IMod mod)
        {
            return API.ModLoader.LoadedMods.FirstOrDefault(modInfo => modInfo.Handlers.Contains(mod));
        }

        private static bool CheckDependencies(ModInfo mod, string originMod, bool first)
        {
            if (mod.status != ModStatus.OK) return false;
            if (originMod != null &&
                !first &&
                mod.modName == originMod)
            {
                LogNoNotify($"[Mod Reporter: found mod dependency cycle for mod {originMod}!");
                return false;
            }

            if (!mod.dependencies.All(dependency =>
                {
                    return modInfos.ContainsKey(dependency.modName) &&
                           CheckDependencies(modInfos[dependency.modName], originMod, false);
                }))
            {
                mod.status = ModStatus.MISSING_DEPENDENCY;
                return false;
            }

            return true;
        }

        private ModInfo GetOrCreateModInfo(string modName, out bool wasCreated)
        {
            if (modInfos.ContainsKey(modName))
            {
                wasCreated = false;
                return modInfos[modName];
            }

            ModInfo info = new ModInfo()
            {
                modName = modName
            };
            modInfos[modName] = info;
            wasCreated = true;
            return info;
        }

        private static void DetermineDependencies(SubscribedMod mod, ModInfo modInfo)
        {
            ModIOUnity.GetModDependencies(mod.modProfile.id, depsResult =>
            {
                if (!depsResult.result.Succeeded()) return;

                foreach (ModDependencies dependency in depsResult.value)
                {
                    modInfo.dependencies.Add(new ModMetadata.Dependency()
                    {
                        modName = dependency.modName
                    });
                }
            });
        }

        private static void OnLog(string logString, string stacktrace, LogType type)
        {
            if (logString.Length == 0) return;
            // MARKER Creating modified script files at C:/Users/ILYA/AppData/Local/Temp/Pugstorm/Core Keeper\ModLoader\CoreLib.Localization

            if (logString.Contains("Creating modified script files"))
            {
                var keywordIndex = logString.IndexOf("ModLoader", StringComparison.InvariantCultureIgnoreCase);
                currentMod = logString.Substring(keywordIndex + 10);
                LogNoNotify($"Mod {currentMod} is being loaded!");
                return;
            }

            var modName = currentMod;

            if (logString[0] == '[')
            {
                var modTagEnd = logString.IndexOf("]:", StringComparison.InvariantCultureIgnoreCase);
                if (modTagEnd != -1)
                {
                    modName = logString.Substring(1, modTagEnd - 1);
                }
            }

            modName = modName.Replace(" ", "");

            if (modInfos.ContainsKey(modName))
            {
                ModInfo mod = modInfos[modName];
                mod.logEntries.Add(new LogEntry()
                {
                    logType = type,
                    message = logString,
                    stacktrace = stacktrace
                });

                if (mod.status != ModStatus.OK &&
                    type == LogType.Error)
                {
                    mod.status = ModStatus.ERRORED;
                    modStateChanged?.Invoke(modName);
                }
            }
        }

        public static void LogNoNotify(string text)
        {
            Application.logMessageReceived -= OnLog;
            Debug.Log(text);
            Application.logMessageReceived += OnLog;
        }

        public void Init() { }

        public void Shutdown() { }

        public void ModObjectLoaded(Object obj) { }

        public void Update() { }
    }
}