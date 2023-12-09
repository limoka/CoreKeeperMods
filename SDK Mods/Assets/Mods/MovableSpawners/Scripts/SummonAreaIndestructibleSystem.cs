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
            NativeList<Translation> bosses = new NativeList<Translation>(Allocator.TempJob);
            var ecb = CreateCommandBuffer();
            float triggerDistance = bossTriggerDistance;

            Entities.ForEach((in Translation translation) =>
            {
                bosses.Add(translation);
            })
                .WithAll<BossCD>()
                .WithNone<EntityDestroyedCD>()
                .WithEntityQueryOptions(EntityQueryOptions.IncludeDisabledEntities)
                .Schedule();

            Entities.ForEach((Entity entity, ref SummonAreaIndestructibleStateCD state, in Translation spawner) =>
                {
                    bool near = false;
                    foreach (Translation boss in bosses)
                    {
                        near |= math.distance(spawner.Value, boss.Value) < triggerDistance;
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