using System;
using CoreLib;
using CoreLib.Data.Configuration;
using CoreLib.Submodule.Entity;
using CoreLib.Submodule.Entity.Attribute;
using CoreLib.Submodule.Localization;
using CoreLib.Util.Extension;
using KeepFarming.Components;
using Mods.KeepFarming.Scripts;
using PugMod;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Logger = CoreLib.Util.Logger;
using Object = UnityEngine.Object;

namespace KeepFarming
{
    public class KeepFarmingMod : IMod
    {
        public const string VERSION = "2.1.8";
        public const string NAME = "Keep Farming";
        private LoadedMod modInfo;

        public static ConfigEntry<bool> enableExtraSeedChance;
        public static ConfigEntry<float> extraSeedChanceMultiplier;

        public static ConfigEntry<bool> migrationMode;

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

            CoreLibMod.LoadSubmodule(typeof(LocalizationModule), typeof(EntityModule));

            EntityModule.RegisterDynamicItemHandler<GoldenSeedDynamicItemHandler>();

            file = new ConfigFile("KeepFarming/Config.cfg", true, modInfo);

            LoadConfigOptions();

            API.Authoring.OnObjectTypeAdded += EditFruits;

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

        private void EditFruits(Entity entity, GameObject authoringdata, EntityManager entitymanager)
        {
            if (migrationMode.Value) return;

            var cookingIngredient = authoringdata.GetComponent<CookingIngredientAuthoring>();
            var flower = authoringdata.GetComponent<FlowerAuthoring>();
            if (cookingIngredient == null || flower == null) return;

            var entityMono = authoringdata.GetComponent<EntityMonoBehaviourData>();
            var objectAuthoring = authoringdata.GetComponent<ObjectAuthoring>();
            if (entityMono == null && objectAuthoring == null) return;
            
            string objectName = "";
            
            if (entityMono != null)
                objectName = entityMono.ObjectInfo.objectID.ToString();

            if (objectAuthoring != null)
                objectName = objectAuthoring.objectName;
            
            if (!objectName.Contains("rare", StringComparison.OrdinalIgnoreCase)) return;
            
            Log.LogInfo($"Checking Fruit {authoringdata.name}");

            var extractable = entitymanager.GetComponentData<ExtractableCD>(entity);
            if (!extractable.extractedObjectOutputArray.IsCreated) return;

            try
            {
                ref var array = ref extractable.extractedObjectOutputArray.Value;
                if (array.Length == 0) return;

                var seedId = array[0].objectID;

                using (var builder = new BlobBuilder(Allocator.Temp))
                {
                    ref var root = ref builder.ConstructRoot<BlobArray<ExtractedObjectOutputElementData>>();

                    var arrayBuilder = builder.Allocate(ref root, 1);
                    Log.LogInfo($"Fruit {authoringdata.name} will now drop {seedId} (2)");
                    arrayBuilder[0] = new ExtractedObjectOutputElementData
                    {
                        objectID = seedId,
                        variation = 2,
                        minMaxRandomAmountOverride = float2.zero
                    };

                    extractable.extractedObjectOutputArray =
                        builder.CreateBlobAssetReference<BlobArray<ExtractedObjectOutputElementData>>(Allocator.Persistent);
                }

                entitymanager.SetComponentData(entity, extractable);
            }
            catch (Exception e)
            {
                Log.LogInfo($"Got an exception while updating fruit: {e}");
            }
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
        }

        public void Update() { }
    }
}