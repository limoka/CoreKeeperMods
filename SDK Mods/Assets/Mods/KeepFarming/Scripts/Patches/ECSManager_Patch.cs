using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using CoreLib.Localization;
using CoreLib.Util.Extensions;
using HarmonyLib;
using KeepFarming.Components;
using PugConversion;
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
                    continue;
                }
                
                var cookingIngredient = monoBehaviour.GetComponent<CookingIngredientAuthoring>();
                var givesConditions = monoBehaviour.GetComponent<GivesConditionsWhenConsumedAuthoring>();
                var flower = monoBehaviour.GetComponent<FlowerAuthoring>();
                if (cookingIngredient == null ||
                    flower == null) continue;
                
                if (monoBehaviour is EntityMonoBehaviourData data)
                {
                    CreateJuice(
                        data.objectInfo.objectID.ToString(), 
                        givesConditions, 
                        cookingIngredient, 
                        flower);
                }

                if (monoBehaviour is ObjectAuthoring objectAuthoring)
                {
                    CreateJuice(
                        objectAuthoring.objectName,
                        givesConditions, 
                        cookingIngredient, 
                        flower);
                }
            }
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

        private static void CreateJuice(
            string fruitName, 
            GivesConditionsWhenConsumedAuthoring givesConditions,
            CookingIngredientAuthoring cookingIngredient, 
            FlowerAuthoring flower)
        {
            if (flower.plantVariation > 0) return;
            
            var plantName = flower.plantID.ToString();
            if (SeedExtractorSystem.juiceData.Any(data => data.plantName.Equals(plantName))) return;

            var item = Object.Instantiate(KeepFarmingMod.juiceItemTemplate);
            item.hideFlags = HideFlags.HideAndDontSave;

            item.AddComponent<CopiedPrefabAuthoring>();

            var newItemId = $"KeepFarming:{fruitName}Juice";
            item.name = $"{newItemId}_Prefab";

            var juiceTemplate = item.GetComponent<JuiceTemplate>();
            var objectAuthoring = item.AddComponent<ObjectAuthoring>();
            objectAuthoring.objectType = juiceTemplate.objectType;
            objectAuthoring.tags = juiceTemplate.tags;
            objectAuthoring.rarity = juiceTemplate.rarity;
            objectAuthoring.objectName = newItemId;
            
            Object.Destroy(juiceTemplate);

            var juiceAuthoring = item.AddComponent<JuiceAuthoring>();
            juiceAuthoring.brightestColor = cookingIngredient.brightestColor;
            juiceAuthoring.brightColor = cookingIngredient.brightColor;
            juiceAuthoring.darkColor = cookingIngredient.darkColor;
            juiceAuthoring.darkestColor = cookingIngredient.darkestColor;

            if (givesConditions != null)
            {
                var itemGivesConditions = item.AddComponent<GivesConditionsWhenConsumedAuthoring>();
                itemGivesConditions.Values = new List<ConditionDataContainer>();

                foreach (ConditionDataContainer value in givesConditions.Values)
                {
                    if (value.conditionData.conditionID != ConditionID.None)
                    {
                        itemGivesConditions.Values.Add(new ConditionDataContainer()
                        {
                            conditionData = value.conditionData
                        });
                    }
                }
            }
            else
            {
                KeepFarmingMod.Log.LogWarning($"{fruitName} does not have conditions!");
            }

            var localizedTerm = API.Localization.GetLocalizedTerm($"Items/{fruitName}");

            if (localizedTerm == null)
            {
                localizedTerm = SplitCamelCase(fruitName);
            }
            
            LocalizationModule.AddTerm($"Items/{newItemId}", $"{localizedTerm} Juice");
            LocalizationModule.AddTerm($"Items/{newItemId}Desc", $"Delicious juice made from fresh fruit!");
            
            SeedExtractorSystem.juiceData.Add(new SeedExtractorSystem.JuiceData()
            {
                plantName = plantName,
                juiceName = newItemId
            });
            
            KeepFarmingMod.Log.LogDebug($"Adding juice for object {fruitName}!");
            API.Authoring.RegisterAuthoringGameObject(item);
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
            ghost.SetValue("prefabId", GetGUID(newPrefab.name));
            ghost.ForcePrefabConversion = true;

            var seedAuthoring = newPrefab.GetComponent<SeedAuthoring>();
            seedAuthoring.rarePlantVariation += 2;
            seedAuthoring.rareSeedVariation++;

            newPrefab.AddComponent<GoldenSeedAuthoring>();

            var alwaysDropZero = newPrefab.GetComponent<AlwaysDropVariationZeroAuthoring>();
            if (alwaysDropZero != null)
                Object.Destroy(alwaysDropZero);
            
            KeepFarmingMod.Log.LogDebug($"Adding golden persistent seed for object {objectName}!");
            API.Authoring.RegisterAuthoringGameObject(newPrefab);
        }


        private static void AddPersistentGoldenPlant(GameObject prefabGo)
        {
            var growingAuthoring = prefabGo.GetComponent<GrowingAuthoring>();
            if (growingAuthoring == null) return;
            
            // Is it a complete plant?
            if (growingAuthoring.currentStage == growingAuthoring.highestStage) return;

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

            var dropLoot = newPrefab.GetComponent<DropsLootAuthoring>();

            if (dropLoot.Values.Count > 0)
            {
                var dropGoldenSeed = newPrefab.AddComponent<DropsGoldenSeedAuthoring>();

                dropGoldenSeed.chance = dropLoot.chance;
                dropGoldenSeed.seedId = dropLoot.Values[0].lootDropID;
                dropGoldenSeed.amount = dropLoot.Values[0].amount;
            }
            Object.Destroy(dropLoot);

            var ghost = newPrefab.GetComponent<GhostAuthoringComponent>();
            newPrefab.name += "P";
            ghost.SetValue("prefabId", GetGUID(newPrefab.name));
            ghost.ForcePrefabConversion = true;

            KeepFarmingMod.Log.LogDebug($"Adding golden persistent plant for object {objectName}!");
            API.Authoring.RegisterAuthoringGameObject(newPrefab);
        }
        
        public static string GetGUID(string objectId)
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