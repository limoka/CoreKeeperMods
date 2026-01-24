using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using CoreLib.Util.Extension;
using HarmonyLib;
using KeepFarming.Components;
using KeepFarming.Util;
using Pug.Conversion;
using PugMod;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;
using Object = UnityEngine.Object;

namespace KeepFarming
{
    [HarmonyPatch]
    public class ECSManager_Patch
    {
        private static Regex camelCaseSplitPattern = new Regex("([A-Z])", RegexOptions.Compiled);
        
        internal static Dictionary<CookingIngredientCD, Texture2D> gradientMaps = new Dictionary<CookingIngredientCD, Texture2D>(new CookingIngredientComparer());

        
        [HarmonyPatch(typeof(ECSManager), nameof(ECSManager.Init))]
        [HarmonyPrefix]
        public static void OnInit(ECSManager __instance)
        {
            foreach (MonoBehaviour monoBehaviour in __instance.pugDatabase.prefabList)
            {
                var plant = monoBehaviour.GetComponent<PlantAuthoring>();
                if (plant != null)
                {
                    AddPersistentGoldenPlant(monoBehaviour.gameObject);
                    continue;
                }
                
                var seedAuthoring = monoBehaviour.GetComponent<SeedAuthoring>();
                if (seedAuthoring != null)
                {
                    AddPersistentGoldenSeed(monoBehaviour.gameObject);
                }
            }
            //SpriteAssetManager_Patch.ReloadAssets();
        }

        [HarmonyPatch(typeof(ConversionManager), "CreateAndEnqueue")]
        [HarmonyPostfix]
        public static void OnCreateAndEnqueue(ConversionManager __instance, GameObject gameObject, Entity __result)
        {
            if (gameObject == null) return;
            
            var copiedPrefab = gameObject.GetComponent<CopiedPrefabAuthoring>();
            
            if (gameObject.scene.IsValid() &&
                copiedPrefab != null &&
                !__instance.EntityManager.HasComponent<Prefab>(__result))
            {
                __instance.EntityManager.AddComponent<Prefab>(__result);
                __instance.EntityManager.AddBuffer<LinkedEntityGroup>(__result).Add(__result);
            }
        }

        private static void AddPersistentGoldenSeed(GameObject prefabGo)
        {
            GameObject newPrefab = Object.Instantiate(prefabGo, null);
            newPrefab.hideFlags = HideFlags.HideAndDontSave;
            
            newPrefab.AddComponent<CopiedPrefabAuthoring>();

            var entityMono = newPrefab.GetComponent<EntityMonoBehaviourData>();
            var objectAuthoring = newPrefab.GetComponent<ObjectAuthoring>();
            if (entityMono == null && objectAuthoring == null) return;
            
            string objectName = "";
            
            if (entityMono != null)
            {
                if (entityMono.ObjectInfo.variation == 0)
                {
                    Object.Destroy(newPrefab);
                    return;
                }

                entityMono.ObjectInfo.variation++;
                objectName = entityMono.ObjectInfo.objectID.ToString();
                entityMono.ObjectInfo.prefabInfos[0].ecsPrefab = newPrefab;
            }

            if (objectAuthoring != null)
            {
                if (objectAuthoring.variation == 0)
                {
                    Object.Destroy(newPrefab);
                    return;
                }

                objectAuthoring.variation++;
                objectName = objectAuthoring.objectName;
            }
            
            var ghost = newPrefab.GetComponent<GhostAuthoringComponent>();
            newPrefab.name += "P";
            ghost.SetValue("prefabId", GetGuid(newPrefab.name));
            ghost.ForcePrefabConversion = true;

            var seedAuthoring = newPrefab.GetComponent<SeedAuthoring>();
            seedAuthoring.rarePlantVariation += 2;
            seedAuthoring.rareSeedVariation++;

            newPrefab.AddComponent<GoldenSeedAuthoring>();

            var alwaysDropZero = newPrefab.GetComponent<AlwaysDropVariationZeroAuthoring>();
            if (alwaysDropZero != null)
                Object.Destroy(alwaysDropZero);
            
            KeepFarmingMod.Log.LogInfo($"Adding golden persistent seed for object {objectName}!");
            API.Authoring.RegisterAuthoringGameObject(newPrefab);
        }


        private static void AddPersistentGoldenPlant(GameObject prefabGo)
        {
            var growingAuthoring = prefabGo.GetComponent<PlantAuthoring>();
            if (growingAuthoring == null) return;
            
            // Is it a complete plant?
            if (growingAuthoring.growingSettings.currentStage == growingAuthoring.growingSettings.highestStage) return;

            GameObject newPrefab = Object.Instantiate(prefabGo, null);
            newPrefab.hideFlags = HideFlags.HideAndDontSave;

            newPrefab.AddComponent<CopiedPrefabAuthoring>();
            
            var entityMono = newPrefab.GetComponent<EntityMonoBehaviourData>();
            var objectAuthoring = newPrefab.GetComponent<ObjectAuthoring>();
            if (entityMono == null && objectAuthoring == null) return;

            string objectName = "";
            
            if (entityMono != null)
            {
                if (entityMono.ObjectInfo.variation == 0)
                {
                    Object.Destroy(newPrefab);
                    return;
                }

                entityMono.ObjectInfo.variation += 2;
                objectName = entityMono.ObjectInfo.objectID.ToString();
                entityMono.ObjectInfo.prefabInfos[0].ecsPrefab = newPrefab;
            }

            if (objectAuthoring != null)
            {
                if (objectAuthoring.variation == 0)
                {
                    Object.Destroy(newPrefab);
                    return;
                }

                objectAuthoring.variation += 2;
                objectName = objectAuthoring.objectName;
            }

            var dropLoot = newPrefab.GetComponent<DropLootAuthoring>();

            if (dropLoot.customLoot.Values.Count > 0)
            {
                var dropGoldenSeed = newPrefab.AddComponent<DropsGoldenSeedAuthoring>();

                dropGoldenSeed.chance = dropLoot.customLoot.chance;
                dropGoldenSeed.seedId = dropLoot.customLoot.Values[0].lootDropID;
                dropGoldenSeed.amount = dropLoot.customLoot.Values[0].amount;
            }
            Object.Destroy(dropLoot);

            var ghost = newPrefab.GetComponent<GhostAuthoringComponent>();
            newPrefab.name += "P";
            ghost.SetValue("prefabId", GetGuid(newPrefab.name));
            ghost.ForcePrefabConversion = true;

            KeepFarmingMod.Log.LogInfo($"Adding golden persistent plant for object {objectName}!");
            API.Authoring.RegisterAuthoringGameObject(newPrefab);
        }
        
        public static string GetGuid(string objectId)
        {
            using MD5 md5 = MD5.Create();
            byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes(objectId));
            Guid result = new Guid(hash);
            return result.ToString("N");
        }
        
        public static string SplitCamelCase(string input)
        {
            return camelCaseSplitPattern.Replace(input, " $1");
        }
    }
}