using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

namespace MovableSpawners
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation)]
    public partial class SummonAreaIndestructibleSystem : PugSimulationSystemBase
    {
        public static float bossTriggerDistance = 100;

        protected override void OnUpdate()
        {
            NativeList<LocalTransform> bosses = new NativeList<LocalTransform>(Allocator.TempJob);
            var ecb = CreateCommandBuffer();
            float triggerDistance = bossTriggerDistance;

            Entities.ForEach((in LocalTransform transform) =>
            {
                bosses.Add(transform);
            })
                .WithAll<BossCD>()
                .WithNone<EntityDestroyedCD>()
                .WithEntityQueryOptions(EntityQueryOptions.IncludeDisabledEntities)
                .Schedule();

            Entities.ForEach((Entity entity, ref SummonAreaIndestructibleStateCD state, in LocalTransform spawner) =>
                {
                    bool near = false;
                    foreach (LocalTransform boss in bosses)
                    {
                        near |= math.distance(spawner.Position, boss.Position) < triggerDistance;
                        if (near) break;
                    }

                    if (state.lastFoundBoss != near)
                    {
                        if (near)
                            ecb.AddComponent<IndestructibleCD>(entity);
                        else
                            ecb.RemoveComponent<IndestructibleCD>(entity);
                    }

                    state.lastFoundBoss = near;
                })
                .WithAll<SummonAreaCD>()
                .WithNone<EntityDestroyedCD>()
                .WithEntityQueryOptions(EntityQueryOptions.IncludeDisabledEntities)
                .WithDisposeOnCompletion(bosses)
                .Schedule();
            
            base.OnUpdate();
        }
    }
}