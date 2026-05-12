using System.Collections.Generic;
using HarmonyLib;
using Mods.MovableSpawners.Scripts.Data;
using Pug.Conversion;
using UnityEngine;

namespace MovableSpawners.Patches
{
    [HarmonyPatch]
    public static class ConversionManager_Patch
    {

        public static void RemoveComponent<T>(this GameObject gameObject) where T : Component
        {
            var component = gameObject.GetComponent<T>();
            if (component == null) return;
            
            Object.DestroyImmediate(component);
        }
        
        [HarmonyPatch(typeof(ConversionManager), "RunConverters")]
        [HarmonyPrefix]
        public static void BeforeConvert(ConversionManager __instance, GameObject gameObject)
        {
            var authoring = gameObject;
            
            var entityData = authoring.GetComponent<EntityMonoBehaviourData>();
            if (entityData == null ||
                (entityData.objectInfo.objectID != ObjectID.SummonArea &&
                entityData.objectInfo.objectID != ObjectID.CrystalMeteor)) return;

            var edited = authoring.GetComponent<SpawnerEdited>();
            if (edited != null) return;
            
            MovableSpawnersMod.Log.LogInfo($"Editing {entityData.objectInfo.objectID}, variation: {entityData.objectInfo.variation}");


            if (entityData.objectInfo.objectID != ObjectID.CrystalMeteor)
            {
                entityData.objectInfo.objectType = ObjectType.PlaceablePrefab;
                entityData.objectInfo.rarity = Rarity.Legendary;
                entityData.objectInfo.isStackable = false;
            }

            var timeSize = new Vector2Int(3, 3);
            var tileOffset = new Vector2Int(-1, -1);

            if (entityData.objectInfo.objectID == ObjectID.CrystalMeteor)
            {
                timeSize = new Vector2Int(12, 9);
                tileOffset = new Vector2Int(-6, -2);
            }
            
            entityData.objectInfo.prefabTileSize = timeSize;
            entityData.objectInfo.prefabCornerOffset = tileOffset;
            entityData.objectInfo.centerIsAtEntityPosition = true;

            if (entityData.objectInfo.objectID != ObjectID.CrystalMeteor)
            {
                var variation = entityData.objectInfo.variation;

                if (variation >= 0 && variation < MovableSpawnersMod.icons.Length)
                {
                    var icon = MovableSpawnersMod.icons[variation];
                    entityData.objectInfo.icon = icon;
                    entityData.objectInfo.smallIcon = icon;
                }
                else
                {
                    entityData.objectInfo.icon = MovableSpawnersMod.errorIcon;
                    entityData.objectInfo.smallIcon = MovableSpawnersMod.errorIcon;
                }
            }
            
            if (entityData.objectInfo.objectID == ObjectID.CrystalMeteor)
            {
                var dropLoot = authoring.AddComponent<ModDropLootAuthoring>();
                dropLoot.objectName = "MovableSpawners:CoreBossSummonArea";
            }
            else
            {
                authoring.RemoveComponent<DontDropSelfAuthoring>();
            }
            
            authoring.RemoveComponent<NonHittableAuthoring>();
            authoring.RemoveComponent<AlwaysDropVariationZeroAuthoring>();

            var health = authoring.GetComponent<HealthAuthoring>();
            
            if (health == null)
                health = authoring.AddComponent<HealthAuthoring>();
            
            health.startHealth = 2;
            health.maxHealth = 2;

            var damageReduction = authoring.AddComponent<DamageReductionAuthoring>();
            damageReduction.maxDamagePerHit = 1;

            authoring.AddComponent<IgnoreVertexOffsetsAuthoring>();
            authoring.AddComponent<MineableAuthoring>();
            authoring.AddComponent<StateAuthoring>();
            authoring.AddComponent<IdleStateAuthoring>();
            authoring.AddComponent<TookDamageStateAuthoring>();
            authoring.AddComponent<DeathStateAuthoring>();

            var placeable = authoring.AddComponent<PlaceableObjectAuthoring>();
            placeable.prefabTileSize = timeSize;
            placeable.prefabCornerOffset = tileOffset;
            placeable.centerIsAtEntityPosition = true;
            placeable.variationToPlace = entityData.objectInfo.variation;
            placeable.canBePlacedOnAnyWalkableTile = true;
            placeable.canBePlacedOnPlayer = true;
            placeable.canBePlacedOnObjects = new List<ObjectID>();
            placeable.canNotBePlacedOnObjects = new List<ObjectID>();
            

            authoring.AddComponent<SpawnerEdited>();
        }
    }
}