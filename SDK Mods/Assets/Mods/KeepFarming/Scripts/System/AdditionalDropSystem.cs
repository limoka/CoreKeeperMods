using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;
using Random = Unity.Mathematics.Random;

namespace KeepFarming
{
    [UpdateBefore(typeof(DropLootSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(RunSimulationSystemGroup))]
    public partial class AdditionalDropSystem : PugSimulationSystemBase
    {
        protected override void OnCreate()
        {
            UpdatesInRunGroup();
            NeedDatabase();
            base.OnCreate();
        }

        protected override void OnUpdate()
        {
            if (!KeepFarmingMod.enableExtraSeedChance.Value) return;
            
            float extraSeedChanceMultiplier = KeepFarmingMod.extraSeedChanceMultiplier.Value;
            if (extraSeedChanceMultiplier == 0) return;
            
            uint seed = PugRandom.GetSeed();
            var databaseLocal = database;
            var summarizedConditionsBuffer = GetBufferLookup<SummarizedConditionsBuffer>(true);
            EntityCommandBuffer ecb = CreateCommandBuffer();
            
            Entities.ForEach((Entity entity,
                    in ObjectDataCD objectData,
                    in LocalTransform transform,
                    in DynamicBuffer<DropsLootBuffer> dropsLootBuffer,
                    in GrowingCD growingCd) =>
                {
                    if (!growingCd.hasFinishedGrowing) return;
                    
                    Random random = Random.CreateFromIndex(seed ^ (uint)entity.Index ^ (uint)entity.Version);
                    KilledByPlayer killedByPlayer = SystemAPI.HasComponent<KilledByPlayer>(entity)
                        ? SystemAPI.GetComponent<KilledByPlayer>(entity)
                        : default;

                    float3 localCenter = PugDatabase.GetEntityLocalCenter(objectData.objectID, databaseLocal, objectData.variation);
                    float3 center = transform.Position + localCenter;

                    for (int i = 0; i < dropsLootBuffer.Length; i++)
                    {
                        LootDrop lootDrop = dropsLootBuffer[i].lootDrop;
                        if (lootDrop.lootDropID == ObjectID.None) continue;

                        float3 dropPosition = center + new float3(random.NextFloat(-0.3f, 0.3f), 0f, random.NextFloat(-0.3f, 0.3f));
                        
                        float chance = 0f;
                        if (SystemAPI.HasComponent<ChanceToDropLootCD>(entity))
                        {
                            chance = SystemAPI.GetComponent<ChanceToDropLootCD>(entity).chance;
                        }

                        if (killedByPlayer.playerEntity != Entity.Null && 
                            summarizedConditionsBuffer.HasBuffer(killedByPlayer.playerEntity))
                        {
                            chance += summarizedConditionsBuffer[killedByPlayer.playerEntity][(int)ConditionID.SeedDropChance].value / 100f;
                        }

                        chance *= extraSeedChanceMultiplier;

                        if (random.NextFloat() <= chance)
                        {
                            ContainedObjectsBuffer item = new ContainedObjectsBuffer
                            {
                                objectData = new ObjectDataCD
                                {
                                    objectID = lootDrop.lootDropID,
                                    amount = 1
                                }
                            };
                            Entity pullToEntity = (killedByPlayer.shouldPullLootToPlayer ? killedByPlayer.playerEntity : Entity.Null);
                            EntityUtility.DropNewEntity(ecb, item, dropPosition, databaseLocal, pullToEntity);
                        }
                    }
                })
                .WithName("ExtraSeedDrop")
                .WithAll<EntityDestroyedCD>()
                .WithAll<PlantCD>()
                .Run();

            base.OnUpdate();
        }
    }
}