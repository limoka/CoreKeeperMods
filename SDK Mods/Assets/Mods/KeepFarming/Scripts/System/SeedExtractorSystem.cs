using System.Collections.Generic;
using System.Runtime.CompilerServices;
using KeepFarming.Components;
using PugAutomation;
using PugMod;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using UnityEngine;
using Random = Unity.Mathematics.Random;

namespace KeepFarming
{
    [UpdateInWorld(TargetWorld.Server)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class SeedExtractorSystem : PugSimulationSystemBase
    {
        public class JuiceData
        {
            public string plantName;
            public string juiceName;
        }
        
        public struct SeedRecipeData
        {
            public ObjectID seedID;
            public ObjectID juiceID;
        }
        
        internal static List<JuiceData> juiceData = new List<JuiceData>();
        
        internal static NativeParallelHashMap<ObjectDataCD, SeedRecipeData> seedExtractorRecipes;
        
        protected override void OnCreate()
        {
            NeedDatabase();
            base.OnCreate();
        }

        protected override void OnUpdate()
        {
            var databaseLocal = database;

            TryCreateSeedLookup();
            
            var seedLookup = seedExtractorRecipes;
            PugTimerSystem.Timer timer = World.GetExistingSystem<PugTimerSystem>().CreateTimer();
            EntityCommandBuffer ecb = CreateCommandBuffer();

            Entities.ForEach((
                    Entity entity,
                    ref CraftingCD craftingCD,
                    in SeedExtractorCD seedExtractor,
                    in PugTimerRefCD pugTimerRef,
                    in DynamicBuffer<ContainedObjectsBuffer> container) =>
                {
                    var canProcess = CanProcess(container, databaseLocal, seedLookup, craftingCD, seedExtractor);

                    if (!canProcess)
                    {
                        craftingCD.timeLeftToCraft = 0f;
                        craftingCD.currentlyCraftingIndex = -1;
                    }
                    else if (craftingCD.currentlyCraftingIndex != 1)
                    {
                        craftingCD.timeLeftToCraft = seedExtractor.processingTime;
                        craftingCD.currentlyCraftingIndex = 1;
                    }

                    if (canProcess && craftingCD.disable == 0 && pugTimerRef.entity == Entity.Null)
                    {
                        timer.StartTimer(ecb, entity, math.min(1f, craftingCD.timeLeftToCraft));
                        Debug.Log("Starting Seed Extractor recipe timer!");
                    }
                })
                .WithName("SeedExtractorStart")
                .WithAll<SeedExtractorCD>()
                .WithAll<CraftingCD>()
                .WithAll<CanCraftObjectsBuffer>()
                .WithAll<PugAutomationCD>()
                .WithAll<ObjectDataCD>()
                .WithAll<ContainedObjectsBuffer>()
                .WithNone<CattleCD>()
                .WithEntityQueryOptions(EntityQueryOptions.IncludeDisabled)
                .WithChangeFilter<CraftingCD>()
                .WithChangeFilter<ContainedObjectsBuffer>()
                .Schedule();
            
            uint rngSeed = PugRandom.GetSeed();
            float extractionChance = KeepFarmingMod.seedExtractionChance.Value;
            float juiceOutputChance = KeepFarmingMod.juiceOutputChance.Value;

            Entities.ForEach((Entity entity, ref PugTimerRefCD userRef) =>
                {
                    ecb.DestroyEntity(entity);
                    if (!HasComponent<CraftingCD>(userRef.entity)) return;
                    if (!HasComponent<SeedExtractorCD>(userRef.entity)) return;

                    CraftingCD craftingCD = GetComponent<CraftingCD>(userRef.entity);
                    if (craftingCD.disable != 0) return;

                    craftingCD.timeLeftToCraft -= 1f;
                    if (craftingCD.timeLeftToCraft > 0f)
                    {
                        timer.StartTimer(ecb, userRef.entity, 1f);
                        ecb.SetComponent(userRef.entity, craftingCD);
                        return;
                    }

                    DynamicBuffer<ContainedObjectsBuffer> container = GetBuffer<ContainedObjectsBuffer>(userRef.entity);
                    Random random = Random.CreateFromIndex(rngSeed ^ (uint)userRef.entity.Index ^ (uint)userRef.entity.Version);
                    var seedExtractor = GetComponent<SeedExtractorCD>(userRef.entity);

                    Process(container, databaseLocal, seedLookup, 
                        craftingCD, seedExtractor, random, 
                        extractionChance, juiceOutputChance);
                    
                    var canProcess = CanProcess(container, databaseLocal, seedLookup, craftingCD, seedExtractor);
                    if (canProcess)
                    {
                        craftingCD.timeLeftToCraft = seedExtractor.processingTime;
                        timer.StartTimer(ecb, userRef.entity, math.min(1f, craftingCD.timeLeftToCraft));
                    }
                    else
                    {
                        craftingCD.timeLeftToCraft = 0f;
                        craftingCD.currentlyCraftingIndex = -1;
                    }

                    ecb.SetComponent(userRef.entity, craftingCD);
                })
                .WithName("SeedExtractorOnTrigger")
                .WithAll<SeedExtractorTimerTriggerCD>()
                .WithAll<PugTimerRefCD>()
                .Schedule();

            base.OnUpdate();
        }

        private bool TryCreateSeedLookup()
        {
            if (seedExtractorRecipes.IsCreated) return false;
            
            seedExtractorRecipes = new NativeParallelHashMap<ObjectDataCD, SeedRecipeData>(10, Allocator.Persistent);

            var juiceLookup = new Dictionary<ObjectID, ObjectID>();
            foreach (var data in juiceData)
            {
                var plant = API.Authoring.GetObjectID(data.plantName);
                if (!juiceLookup.ContainsKey(plant))
                {
                    var juice = API.Authoring.GetObjectID(data.juiceName);
                    juiceLookup.Add(plant, juice);
                }
            }

            Entities.ForEach((in ObjectDataCD objectData, in FlowerCD flower) =>
                {
                    Entity flowerEntity = PugDatabase.GetPrimaryPrefabEntity(flower.plantID, database, flower.plantVariation);
                    var lootBuffer = GetBuffer<DropsLootBuffer>(flowerEntity);
                    if (lootBuffer.Length <= 0) return;

                    var seedObjectID = lootBuffer[0].lootDrop.lootDropID;
                    if (seedObjectID == ObjectID.None) return;

                    ObjectDataCD fruitData = objectData;
                    ObjectID juiceID = ObjectID.None;
                    if (juiceLookup.ContainsKey(flower.plantID))
                    {
                        juiceID = juiceLookup[flower.plantID];
                    }
                    
                    if (!seedExtractorRecipes.ContainsKey(fruitData))
                        seedExtractorRecipes.Add(fruitData, new SeedRecipeData()
                        {
                            seedID = seedObjectID,
                            juiceID = juiceID
                        });
                })
                .WithAll<ObjectDataCD>()
                .WithAll<FlowerCD>()
                .WithAll<CookingIngredientCD>()
                .WithAll<Prefab>()
                .WithoutBurst()
                .Run();

            return true;
        }

        [BurstCompatible]
        private static bool Process(
            DynamicBuffer<ContainedObjectsBuffer> container,
            BlobAssetReference<PugDatabase.PugDatabaseBank> database,
            NativeParallelHashMap<ObjectDataCD, SeedRecipeData> recipes,
            CraftingCD craftingCD,
            SeedExtractorCD seedExtractorCd,
            Random random,
            float extractionChance,
            float juiceOutputChance)
        {
            if (!CanProcess(container, database, recipes, craftingCD, seedExtractorCd)) return false;

            ObjectDataCD objectData = container[0].objectData;
            var recipeData = recipes[objectData];

            if (random.NextFloat() < extractionChance)
            {
                ref PugDatabase.EntityObjectInfo plantObjectInfo = ref PugDatabase.GetEntityObjectInfo(objectData.objectID, database, objectData.variation);
                var variation = plantObjectInfo.rarity > Rarity.Common ? 2 : 0;
                
                AddItem(container, craftingCD.outputSlotIndex, recipeData.seedID, variation);
            }

            if (random.NextFloat() < juiceOutputChance)
            {
                AddItem(container, seedExtractorCd.juiceOutputSlot, recipeData.juiceID, 0);
            }
            
            objectData.amount--;
            if (objectData.amount <= 0)
            {
                objectData = new ObjectDataCD();
            }

            container[0] = new ContainedObjectsBuffer()
            {
                objectData = objectData
            };
            return true;
        }

        [BurstCompatible]
        private static void AddItem(DynamicBuffer<ContainedObjectsBuffer> container, int slotIndex, ObjectID itemId, int variation)
        {
            ObjectDataCD outputData = container[slotIndex].objectData;

            if (outputData.objectID == ObjectID.None)
            {
                outputData = new ObjectDataCD
                {
                    objectID = itemId,
                    amount = 1,
                    variation = variation
                };
            }
            else
            {
                outputData.amount += 1;
            }

            container[slotIndex] = new ContainedObjectsBuffer
            {
                objectData = outputData
            };
        }

        [BurstCompatible]
        private static bool CanProcess(
            DynamicBuffer<ContainedObjectsBuffer> container,
            BlobAssetReference<PugDatabase.PugDatabaseBank> database,
            NativeParallelHashMap<ObjectDataCD, SeedRecipeData> recipes,
            CraftingCD craftingCD,
            SeedExtractorCD seedExtractorCd)
        {
            ObjectDataCD objectData = container[0].objectData;
            if (objectData.objectID == ObjectID.None) return false;
            
            if (!recipes.ContainsKey(objectData)) return false;

            var recipeData = recipes[objectData];
            ref PugDatabase.EntityObjectInfo plantObjectInfo = ref PugDatabase.GetEntityObjectInfo(objectData.objectID, database, objectData.variation);
            var outputVariation = plantObjectInfo.rarity > Rarity.Common ? 2 : 0;
            
            ObjectDataCD outputData = container[craftingCD.outputSlotIndex].objectData;
            ref PugDatabase.EntityObjectInfo outputObjectInfo = ref PugDatabase.GetEntityObjectInfo(recipeData.seedID, database, 0);

            if (outputData.objectID != ObjectID.None &&
                (!outputObjectInfo.isStackable ||
                 outputData.objectID != recipeData.seedID ||
                 outputData.variation != outputVariation ||
                 outputData.amount >= 999))
            {
                return false;
            }

            ObjectDataCD juiceOutputData = container[seedExtractorCd.juiceOutputSlot].objectData;
            outputObjectInfo = ref PugDatabase.GetEntityObjectInfo(recipeData.juiceID, database, 0);

            if (juiceOutputData.objectID != ObjectID.None &&
                (!outputObjectInfo.isStackable ||
                 juiceOutputData.objectID != recipeData.juiceID ||
                 juiceOutputData.amount >= 999))
            {
                return false;
            }

            return true;

        }
    }
}