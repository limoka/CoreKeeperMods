using System;
using System.Collections.Generic;
using System.Linq;
using CoreLib;
using CoreLib.Data.Configuration;
using CoreLib.Localization;
using CoreLib.Submodules.ModEntity;
using CoreLib.Submodules.ModEntity.Atributes;
using CoreLib.Util.Extensions;
using KeepFarming.Components;
using Mods.KeepFarming.Scripts;
using Mods.KeepFarming.Scripts.Prefab;
using PugMod;
using Unity.Burst;
using Unity.Entities;
using UnityEngine;
using Logger = CoreLib.Util.Logger;
using Object = UnityEngine.Object;

namespace KeepFarming
{
    [EntityModification]
    public class KeepFarmingMod : IMod
    {
        public const string VERSION = "2.0.1";
        public const string NAME = "Keep Farming";
        private LoadedMod modInfo;

        public static ConfigEntry<bool> enableExtraSeedChance;
        public static ConfigEntry<float> extraSeedChanceMultiplier;

        public static ConfigEntry<float> seedExtractionChance;
        public static ConfigEntry<float> juiceOutputChance;

        public static ConfigEntry<bool> migrationMode;

        internal static GameObject juiceItemTemplate;

        internal static Logger Log = new Logger(NAME);
        internal static ConfigFile file;

        public void EarlyInit()
        {
            Log.LogInfo($"Mod version: {VERSION}");
            modInfo = this.GetModInfo();
            if (modInfo == null)
            {
                Log.LogError($"Failed to load {NAME}: mod metadata not found!");
                return;
            }

            CoreLibMod.LoadModules(typeof(LocalizationModule), typeof(EntityModule));

            EntityModule.RegisterDynamicItemHandler<JuiceDynamicItemHandler>();
            EntityModule.RegisterDynamicItemHandler<GoldenSeedDynamicItemHandler>();
            EntityModule.RegisterEntityModifications(modInfo.ModId);

            file = new ConfigFile("KeepFarming/Config.cfg", true, modInfo);

            LoadConfigOptions();

            modInfo.TryLoadBurstAssembly();

            Log.LogInfo("Mod loaded successfully");
        }

        private static void LoadConfigOptions()
        {
            enableExtraSeedChance = file.Bind(
                "ExtraChance",
                "Enabled",
                false,
                "Should extra seed chance mechanic be enabled?\n" +
                "This feature is disabled by default because I consider Seed Extractor mechanic to be superior."
            );

            extraSeedChanceMultiplier = file.Bind(
                "ExtraChance",
                "ExtraSeedChanceMultiplier",
                0.1f,
                "Value to multiply normal seed gain chance, to derive extra seed chance"
            );

            seedExtractionChance = file.Bind(
                "SeedExtractor",
                "SeedExtractionChance",
                0.3f,
                "Chance to gain seed from fruit using Seed Extractor machine"
            );

            juiceOutputChance = file.Bind(
                "SeedExtractor",
                "JuiceOutputChance",
                0.5f,
                "Chance to gain juice from fruit using Seed Extractor machine"
            );

            migrationMode = file.Bind(
                "Misc",
                "EnableMigrationMode",
                false,
                "Should migration mode be enabled?\n" +
                "" +
                "WARNING: Migration mode is intended to be used\n" +
                "when you no longer want to keep playing with\n" +
                "Keep Farming mod, and want to preserve your plants\n" +
                "and seeds.\n" +
                "" +
                "Do NOT enable otherwise!"
            );
        }


        [EntityModification(ObjectID.AutomationTable)]
        private static void EditAutomationTable(Entity entity, GameObject authoring, EntityManager entityManager)
        {
            var canCraftBuffer = entityManager.GetBuffer<CanCraftObjectsBuffer>(entity);
            var lastIndex = canCraftBuffer.Length - 1;
            var item = API.Authoring.GetObjectID("KeepFarming:SeedExtractor");

            if (canCraftBuffer[lastIndex].objectID == ObjectID.None)
            {
                Log.LogInfo($"Adding itemId {item} to AutomationTable");
                canCraftBuffer[lastIndex] = new CanCraftObjectsBuffer
                {
                    objectID = item,
                    amount = 1,
                    entityAmountToConsume = 0
                };
            }
            else
            {
                for (int i = 0; i < canCraftBuffer.Length; i++)
                {
                    if (canCraftBuffer[i].objectID == item) return;
                }

                addBufferEntry(canCraftBuffer, item);
            }
        }

        private static void addBufferEntry(DynamicBuffer<CanCraftObjectsBuffer> canCraftBuffer, ObjectID itemId)
        {
            Log.LogInfo($"Adding itemId {itemId} to AutomationTable");
            canCraftBuffer.Add(new CanCraftObjectsBuffer
            {
                objectID = itemId,
                amount = 1,
                entityAmountToConsume = 0
            });
        }

        public void Init() { }

        public void Shutdown() { }

        public void ModObjectLoaded(Object obj)
        {
            GameObject gameObject = obj as GameObject;
            if (gameObject == null)
            {
                return;
            }

            var seedExtractor = gameObject.GetComponent<SeedExtractor>();

            if (seedExtractor != null)
            {
                EntityModule.AddToAuthoringList(gameObject);
            }

            var juiceTemplate = gameObject.GetComponent<JuiceTemplate>();
            if (juiceTemplate != null)
            {
                juiceItemTemplate = gameObject;
            }
        }

        public void Update() { }
    }
}