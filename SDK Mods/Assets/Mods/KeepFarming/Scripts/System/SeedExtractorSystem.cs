using System.Collections.Generic;
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
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation)]
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
            TryCreateSeedLookup();
            
            if (!isServer) return;

            var databaseLocal = database;
            var seedLookup = seedExtractorRecipes;
            PugTimerSystem.Timer timer = World.GetExistingSystemManaged<PugTimerSystem>().CreateTimer();
            EntityCommandBuffer ecb = CreateCommandBuffer();

            SystemAPI.TryGetSingleton(out ClientServerTickRate clientServerTickRate);
            clientServerTickRate.ResolveDefaults();
            int simulationTickRate = clientServerTickRate.SimulationTickRate;
            var flowerLookup = GetComponentLookup<FlowerCD>(true);

            Entities.ForEach((
                    Entity entity,
                    ref CraftingCD craftingCD,
                    in SeedExtractorCD seedExtractor,
                    in PugTimerRefCD pugTimerRef,
                    in DynamicBuffer<ContainedObjectsBuffer> container) =>
                {
                    var canProcess = CanProcess(container, databaseLocal, seedLookup, flowerLookup, craftingCD, seedExtractor);

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
                        timer.StartTimer(ecb, entity, math.min(1f, craftingCD.timeLeftToCraft), simulationTickRate);
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
                .WithNone<EntityDestroyedCD>()
                .WithEntityQueryOptions(EntityQueryOptions.IncludeDisabledEntities)
                .WithChangeFilter<CraftingCD>()
                .WithChangeFilter<ContainedObjectsBuffer>()
                .Schedule();
            
            uint rngSeed = PugRandom.GetSeed();
            float extractionChance = KeepFarmingMod.seedExtractionChance.Value;
            float juiceOutputChance = KeepFarmingMod.juiceOutputChance.Value;

            Entities.ForEach((Entity entity, ref PugTimerRefCD userRef) =>
                {
                    ecb.DestroyEntity(entity);
                    if (!SystemAPI.HasComponent<CraftingCD>(userRef.entity)) return;
                    if (!SystemAPI.HasComponent<SeedExtractorCD>(userRef.entity)) return;
                    
                    if (SystemAPI.HasComponent<EntityDestroyedCD>(userRef.entity)) return;

                    CraftingCD craftingCD = SystemAPI.GetComponent<CraftingCD>(userRef.entity);
                    if (craftingCD.disable != 0) return;

                    craftingCD.timeLeftToCraft -= 1f;
                    if (craftingCD.timeLeftToCraft > 0f)
                    {
                        timer.StartTimer(ecb, userRef.entity, 1f, simulationTickRate);
                        ecb.SetComponent(userRef.entity, craftingCD);
                        return;
                    }

                    DynamicBuffer<ContainedObjectsBuffer> container = SystemAPI.GetBuffer<ContainedObjectsBuffer>(userRef.entity);
                    Random random = PugRandom.GetRngFromEntity(rngSeed, userRef.entity);
                    var seedExtractor = SystemAPI.GetComponent<SeedExtractorCD>(userRef.entity);

                    Process(container, databaseLocal, seedLookup, flowerLookup,
                        craftingCD, seedExtractor, random, 
                        extractionChance, juiceOutputChance);
                    
                    var canProcess = CanProcess(container, databaseLocal, seedLookup, flowerLookup, craftingCD, seedExtractor);
                    if (canProcess)
                    {
                        craftingCD.timeLeftToCraft = seedExtractor.processingTime;
                        timer.StartTimer(ecb, userRef.entity, math.min(1f, craftingCD.timeLeftToCraft), simulationTickRate);
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
                    var lootBuffer = SystemAPI.GetBuffer<DropsLootBuffer>(flowerEntity);
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

        [GenerateTestsForBurstCompatibility]
        private static bool Process(
            DynamicBuffer<ContainedObjectsBuffer> container,
            BlobAssetReference<PugDatabase.PugDatabaseBank> database,
            NativeParallelHashMap<ObjectDataCD, SeedRecipeData> recipes,
            ComponentLookup<FlowerCD> flowerLookup,
            CraftingCD craftingCD,
            SeedExtractorCD seedExtractorCd,
            Random random,
            float extractionChance,
            float juiceOutputChance)
        {
            if (!CanProcess(container, database, recipes, flowerLookup, craftingCD, seedExtractorCd)) return false;

            ObjectDataCD objectData = container[0].objectData;
            var recipeData = recipes[objectData];

            if (random.NextFloat() < extractionChance)
            {
                var variation = 0;

                Entity plantEntity = PugDatabase.GetPrimaryPrefabEntity(objectData.objectID, database, objectData.variation);

                if (flowerLookup.HasComponent(plantEntity))
                {
                    var flower = flowerLookup[plantEntity];
                    if (flower.plantVariation > 0)
                    {
                        variation = 2;
                    }
                }
                
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

        [GenerateTestsForBurstCompatibility]
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

        [GenerateTestsForBurstCompatibility]
        private static bool CanProcess(
            DynamicBuffer<ContainedObjectsBuffer> container,
            BlobAssetReference<PugDatabase.PugDatabaseBank> database,
            NativeParallelHashMap<ObjectDataCD, SeedRecipeData> recipes,
            ComponentLookup<FlowerCD> flowerLookup,
            CraftingCD craftingCD,
            SeedExtractorCD seedExtractorCd)
        {
            if (!container.IsCreated) return false;
            
            ObjectDataCD objectData = container[0].objectData;
            if (objectData.objectID == ObjectID.None) return false;
            
            if (!recipes.ContainsKey(objectData)) return false;

            var recipeData = recipes[objectData];

            var outputVariation = 0;

            Entity plantEntity = PugDatabase.GetPrimaryPrefabEntity(objectData.objectID, database, objectData.variation);

            if (flowerLookup.HasComponent(plantEntity))
            {
                var flower = flowerLookup[plantEntity];
                if (flower.plantVariation > 0)
                {
                    outputVariation = 2;
                }
            }
            
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