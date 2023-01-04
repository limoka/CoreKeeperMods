﻿using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using CoreLib;
using CoreLib.Submodules.CustomEntity;
using CoreLib.Submodules.CustomEntity.Atributes;
using CoreLib.Submodules.Equipment;
using CoreLib.Submodules.JsonLoader;
using CoreLib.Submodules.Localization;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;
using QFSW.QC.Actions;

namespace BucketMod
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    [BepInDependency("com.le4fless.corelib")]
    [BepInProcess("CoreKeeper.exe")]
    [CoreLibSubmoduleDependency(new []{nameof(CustomEntityModule), nameof(JsonLoaderModule), nameof(EquipmentSlotModule)})]
    public class BucketModPlugin : BasePlugin
    {
        public static ManualLogSource logger;

        public override void Load()
        {
            // Plugin startup logic
            logger = Log;
            Harmony harmony = new Harmony(PluginInfo.PLUGIN_GUID);
            
            string pluginfolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            
            JsonLoaderModule.LoadFolder("BucketMod", pluginfolder);
            BucketSlot.Init();

            LocalizationModule.AddTerm($"Items/{BucketSlot.bucketObjectID}Dirt", "Water Bucket");
            LocalizationModule.AddTerm($"Items/{BucketSlot.bucketObjectID}LarvaHive", "Poison Water Bucket");
            LocalizationModule.AddTerm($"Items/{BucketSlot.bucketObjectID}Mold", "Mouldy Water Bucket");
            LocalizationModule.AddTerm($"Items/{BucketSlot.bucketObjectID}Sea", "Sea Water Bucket");
            LocalizationModule.AddTerm($"Items/{BucketSlot.bucketObjectID}Lava", "Lava Bucket");
            
            LocalizationModule.AddTerm($"Items/{BucketSlot.canObjectID}Dirt", "Pressurized Water Can");
            LocalizationModule.AddTerm($"Items/{BucketSlot.canObjectID}LarvaHive", "Pressurized Poison Water Can");
            LocalizationModule.AddTerm($"Items/{BucketSlot.canObjectID}Mold", "Pressurized Mouldy Water Can");
            LocalizationModule.AddTerm($"Items/{BucketSlot.canObjectID}Sea", "Pressurized Sea Water Can");
            LocalizationModule.AddTerm($"Items/{BucketSlot.canObjectID}Lava", "Pressurized Lava Can");
            
            CustomEntityModule.RegisterDynamicItemHandler<BucketDynamicItemHandler>();
            EquipmentSlotModule.RegisterEquipmentSlot<BucketSlot>(EquipmentSlotModule.PLACEMENT_PREFAB);
            
            harmony.PatchAll();
            logger.LogInfo($"{PluginInfo.PLUGIN_NAME} mod is loaded!");
        }
    }
}