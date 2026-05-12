using Mods.MovableSpawners.Scripts.Data;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace MovableSpawners
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation)]
    public partial class SummonAreaIndestructibleSystem : PugSimulationSystemBase
    {
        private const float BossTriggerDistance = 100;

        protected override void OnUpdate()
        {
            using var bosses = new NativeList<BossInfo>(Allocator.Temp);

            foreach (var (objectData, transform) in 
                     SystemAPI.Query<RefRO<ObjectDataCD>, RefRO<LocalTransform>>()
                         .WithAll<BossCD>()
                         .WithNone<EntityDestroyedCD>()
                         .WithOptions(EntityQueryOptions.IncludeDisabledEntities))
            {
                bosses.Add(new BossInfo(objectData.ValueRO.objectID, transform.ValueRO));
            }
            
            var ecb = CreateCommandBuffer();
            var coreBossLookup = SystemAPI.GetComponentLookup<CoreBossSpawnCD>();
            var destroyIfNotOnTile = SystemAPI.GetComponentLookup<DestroyEntityIfNotOnTileCD>();
            
            foreach (var (transform, area, indestructibleState, entity) in
                     SystemAPI.Query<RefRO<LocalTransform>, RefRO<SummonAreaCD>, EnabledRefRW<IndestructibleCD>>()
                         .WithNone<EntityDestroyedCD>()
                         .WithOptions(EntityQueryOptions.IncludeDisabledEntities |
                                      EntityQueryOptions.IgnoreComponentEnabledState)
                         .WithEntityAccess())
            {
                var isCore = coreBossLookup.HasComponent(entity);
                if (isCore)
                {
                    var coreBoss = coreBossLookup[entity];
                    if (coreBoss.state != CoreBossSpawnState.Hidden) return;
                }
                
                var near = false;
                foreach (var boss in bosses)
                {
                    if (boss.bossID != area.ValueRO.bossToSummon && boss.bossID != area.ValueRO.optionalBossToSummon) continue;
                    if (!(math.distance(transform.ValueRO.Position, boss.transform.Position) < BossTriggerDistance)) continue;
                    
                    near = true;
                    break;
                }
                
                if (indestructibleState.ValueRO != near)
                {
                    indestructibleState.ValueRW = near;

                    if (!isCore) continue;
                    
                    var hasDestroy = destroyIfNotOnTile.HasComponent(entity);
                    if (hasDestroy && near)
                        ecb.RemoveComponent<DestroyEntityIfNotOnTileCD>(entity);
                    else if (!hasDestroy && !near)
                        ecb.AddComponent<DestroyEntityIfNotOnTileCD>(entity);
                }
            }
            
            base.OnUpdate();
        }
    }
}