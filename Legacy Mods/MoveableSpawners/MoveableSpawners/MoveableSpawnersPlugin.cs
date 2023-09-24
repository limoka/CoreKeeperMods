using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using BepInEx.Logging;
using CoreLib;
using CoreLib.Submodules.CustomEntity;
using CoreLib.Submodules.CustomEntity.Atributes;
using CoreLib.Submodules.JsonLoader;
using CoreLib.Submodules.Localization;
using CoreLib.Submodules.ModResources;
using HarmonyLib;
using Il2CppSystem.Collections.Generic;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Authoring;
using UnityEngine;

namespace MoveableSpawners
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    [BepInDependency(CoreLibPlugin.GUID)]
    [CoreLibSubmoduleDependency(nameof(CustomEntityModule))]
    [EntityModification]
    public class MoveableSpawnersPlugin : BasePlugin
    {
        public static ManualLogSource logger;

        public static string path;
        public static Sprite iconSmall;
        public static Sprite iconBig;

        public override void Load()
        {
            // Plugin startup logic
            logger = Log;
            Harmony harmony = new Harmony(PluginInfo.PLUGIN_GUID);
            harmony.PatchAll();
            
            path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            
            CustomEntityModule.RegisterModifications(Assembly.GetExecutingAssembly());
            CustomEntityModule.RegisterDynamicItemHandler<SummonAreaDynamicItemHandler>();
            
            LocalizationModule.AddEntityLocalization(ObjectID.SummonArea, "Slime Boss Summon Area", "Allows you to spawn some big baddie.");
            LocalizationModule.AddTerm($"Items/{ObjectID.SummonArea}1", "Hive Boss Summon Area");
            LocalizationModule.AddTerm($"Items/{ObjectID.SummonArea}2", "Larva Boss Summon Area");
            LocalizationModule.AddTerm($"Items/{ObjectID.SummonArea}3", "Poison Slime Boss Summon Area");
            LocalizationModule.AddTerm($"Items/{ObjectID.SummonArea}4", "Shaman Boss Summon Area");
            LocalizationModule.AddTerm($"Items/{ObjectID.SummonArea}5", "Slippery Slime Boss Summon Area");
            LocalizationModule.AddTerm($"Items/{ObjectID.SummonArea}6", "Lava Slime Boss Summon Area");

            
            logger.LogInfo($"{PluginInfo.PLUGIN_NAME} mod is loaded!");
        }

        [EntityModification(ObjectID.SummonArea)]
        public static void EditSpawners(EntityMonoBehaviourData entityData)
        {
            if (iconSmall == null)
            {
                string oldPath = JsonLoaderModule.context;
                JsonLoaderModule.context = path;
                iconSmall = ResourcesModule.LoadAsset<Sprite>("resources/icon-small.png");
                iconBig = ResourcesModule.LoadAsset<Sprite>("resources/icon-big.png");
                JsonLoaderModule.context = oldPath;
            }
            
            entityData.objectInfo.objectType = ObjectType.PlaceablePrefab;
            entityData.objectInfo.isStackable = false;
            entityData.objectInfo.prefabTileSize = new Vector2Int(3, 3);
            entityData.objectInfo.prefabCornerOffset = new Vector2Int(-1, -1);
            entityData.objectInfo.centerIsAtEntityPosition = true;
            entityData.objectInfo.smallIcon = iconSmall;
            entityData.objectInfo.icon = iconBig;

            GameObject go = entityData.gameObject;
            
            Object.Destroy(go.GetComponent<IndestructibleCDAuthoring>());
            Object.Destroy(go.GetComponent<NonHittableCDAuthoring>());

            PlaceableObjectCDAuthoring placeable = go.AddComponent<PlaceableObjectCDAuthoring>();
            placeable.canBePlacedOnPlayer = true;
            placeable.canBePlacedOnAnyWalkableTile = true;
            placeable.canNotBePlacedOnObjects = new List<ObjectID>();
            placeable.canOnlyBePlacedOnObjects = new List<ObjectID>();

            go.AddComponent<MineableCDAuthoring>();
            HealthCDAuthoring health = go.AddComponent<HealthCDAuthoring>();
            health.normalizedOverrideStartHealth = 1;
            health.startHealth = 2;
            health.maxHealth = 2;
            health.maxHealthMultiplier = 1;

            DamageReductionCDAuthoring reduction = go.AddComponent<DamageReductionCDAuthoring>();
            reduction.reductionMultiplier = 1;
            reduction.maxDamagePerHit = 1;

            HealthRegenerationCDAuthoring regen = go.AddComponent<HealthRegenerationCDAuthoring>();
            regen.healthPercentagePerFifthSecond = 100;

            go.AddComponent<AnimationCDAuthoring>();
            go.AddComponent<StateInfoCDAuthoring>();
            go.AddComponent<IdleStateCDAuthoring>();
            go.AddComponent<TookDamageStateCDAuthoring>();
            go.AddComponent<DeathStateCDAuthoring>();

        }
    }
}