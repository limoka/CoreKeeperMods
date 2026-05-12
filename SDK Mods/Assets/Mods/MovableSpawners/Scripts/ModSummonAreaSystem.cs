using Mods.MovableSpawners.Scripts.Data;
using Unity.Entities;
using Unity.Transforms;
using SimulationSystemGroup = Unity.Entities.SimulationSystemGroup;
using SystemAPI = Unity.Entities.SystemAPI;
using WorldSystemFilterFlags = Unity.Entities.WorldSystemFilterFlags;

namespace MovableSpawners
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial class ModSummonAreaSystem : PugSimulationSystemBase
    {
        protected override void OnCreate()
        {
            base.OnCreate();
            NeedDatabase();

            var query = GetEntityQuery(
                ComponentType.ReadOnly<ModSummonAreaCD>(),
                ComponentType.Exclude<Prefab>()
            );
            RequireForUpdate(query);
        }

        protected override void OnUpdate()
        {
            var ecb = CreateCommandBuffer();
            var databaseBank = SystemAPI.GetSingleton<PugDatabase.DatabaseBankCD>();

            var coreBossLookup = SystemAPI.GetComponentLookup<CoreBossSpawnCD>();
            var disablePhysicsLookup = SystemAPI.GetComponentLookup<DisablePhysicsCD>();

            foreach (var (area, transform, entity) in
                     SystemAPI.Query<RefRO<ModSummonAreaCD>, RefRO<LocalTransform>>()
                         .WithNone<EntityDestroyedCD>()
                         .WithEntityAccess())
            {
                var objectID = area.ValueRO.objectToSpawn;

                var prefabEntity = PugDatabase.GetPrimaryPrefabEntity(objectID, databaseBank.databaseBankBlob);
                if (prefabEntity == Entity.Null)
                {
                    MovableSpawnersMod.Log.LogWarning(
                        $"Could not find entity prefab for {(int)objectID}. Make sure it is correctly set up in its prefab."
                    );
                    return;
                }

                
                var newEntity = ecb.Instantiate(prefabEntity);
                ecb.SetComponent(newEntity, new ObjectDataCD()
                {
                    objectID = objectID,
                    amount = 1
                });
                ecb.SetComponent(newEntity, transform.ValueRO);
                

                if (coreBossLookup.TryGetComponent(prefabEntity, out var coreBoss))
                {
                    coreBoss.state = CoreBossSpawnState.Hidden;
                    ecb.SetComponent(newEntity, coreBoss);
                    ecb.SetComponent(newEntity, new HealthCD()
                    {
                        health = 2,
                        maxHealth = 2
                    });

                    if (disablePhysicsLookup.HasComponent(prefabEntity))
                        ecb.SetComponentEnabled<DisablePhysicsCD>(newEntity, true);

                    ecb.SetBuffer<ConditionsBuffer>(newEntity);
                    ecb.SetBuffer<CompanionEntityBuffer>(newEntity);
                }

                ecb.DestroyEntity(entity);
            }

            base.OnUpdate();
        }
    }
}