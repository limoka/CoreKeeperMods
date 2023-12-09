using KeepFarming.Components;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

namespace KeepFarming
{
    [UpdateBefore(typeof(DropLootSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(RunSimulationSystemGroup))]
    public partial class GoldenSeedDropSystem : PugSimulationSystemBase
    {
        protected override void OnCreate()
        {
            UpdatesInRunGroup();
            NeedDatabase();
            base.OnCreate();
        }

        protected override void OnUpdate()
        {
            var ecb = CreateCommandBuffer();
            var databaseLocal = database;
            uint lootSeed = 0;

            var summarizedConditionsBuffer = GetBufferLookup<SummarizedConditionsBuffer>(true);

            Entities.ForEach((
                    Entity entity,
                    in ObjectDataCD objectData,
                    in Translation translation,
                    in DropsGoldenSeedCD dropsGoldenSeed,
                    in EntityDestroyedCD entityDestroyed) =>
                {
                    if (SystemAPI.HasComponent<DropLootDelayCD>(entity))
                    {
                        float value = SystemAPI.GetComponent<DropLootDelayCD>(entity).Value;
                        if (entityDestroyed.destroyTimer < value) return;
                    }

                    ecb.RemoveComponent<DropsGoldenSeedCD>(entity);
                    if (dropsGoldenSeed.seedId == ObjectID.None) return;

                    var random = PugRandom.GetRngFromEntity(lootSeed, entity);

                    KilledByPlayer killedByPlayer = SystemAPI.HasComponent<KilledByPlayer>(entity) ? SystemAPI.GetComponent<KilledByPlayer>(entity) : default;

                    float3 entityLocalCenter = PugDatabase.GetEntityLocalCenter(objectData.objectID, databaseLocal, objectData.variation);
                    float3 entityCenter = translation.Value + entityLocalCenter;
                    float3 dropPosition = entityCenter + new float3(random.NextFloat(-0.3f, 0.3f), 0f, random.NextFloat(-0.3f, 0.3f));

                    float chance = dropsGoldenSeed.chance;

                    if (SystemAPI.HasComponent<PlantCD>(entity))
                    {
                        if (SystemAPI.HasComponent<GrowingCD>(entity) && 
                            !SystemAPI.GetComponent<GrowingCD>(entity).hasFinishedGrowing)
                        {
                            chance = 1f;
                        }

                        if (killedByPlayer.playerEntity != Entity.Null && summarizedConditionsBuffer.HasBuffer(killedByPlayer.playerEntity))
                        {
                            chance += summarizedConditionsBuffer[killedByPlayer.playerEntity][127].value / 100f;
                        }
                    }

                    if (random.NextFloat() <= chance)
                    {
                        int amount = dropsGoldenSeed.amount;
                        int dropCount = 1;
                        if (!PugDatabase.GetEntityObjectInfo(dropsGoldenSeed.seedId, databaseLocal, 2).isStackable)
                        {
                            dropCount = amount;
                            amount = PugDatabase.GetEntityObjectInfo(dropsGoldenSeed.seedId, databaseLocal, 2).initialAmount;
                        }

                        ContainedObjectsBuffer containedObjectsBuffer = new ContainedObjectsBuffer
                        {
                            objectData = new ObjectDataCD
                            {
                                objectID = dropsGoldenSeed.seedId,
                                variation = 2,
                                amount = amount
                            }
                        };
                        Entity playerEntity = (killedByPlayer.shouldPullLootToPlayer ? killedByPlayer.playerEntity : Entity.Null);
                        for (int j = 0; j < dropCount; j++)
                        {
                            EntityUtility.DropNewEntity(ecb, containedObjectsBuffer, dropPosition, databaseLocal, playerEntity);
                        }
                    }
                })
                .WithAll<ObjectDataCD>()
                .WithAll<Translation>()
                .WithAll<EntityDestroyedCD>()
                .WithAll<DropsGoldenSeedCD>()
                .WithNone<PlayerGhost>()
                .WithNone<DontDropLootCD>()
                .Run();


            base.OnUpdate();
        }
    }
}