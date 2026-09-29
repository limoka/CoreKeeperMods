using Unity.Entities;

namespace SecureAttachment.Systems
{
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct MountingSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (mounted, health, indestructible) in
                     SystemAPI.Query<RefRW<MountedCD>, RefRW<HealthCD>, EnabledRefRW<IndestructibleCD>>()
                         .WithNone<EntityDestroyedCD>()
                         .WithOptions(EntityQueryOptions.IncludeDisabledEntities |
                                      EntityQueryOptions.IgnoreComponentEnabledState))
            {
                var mountType = mounted.ValueRO.mountType;
                var lastMountType = mounted.ValueRO.lastMountType;
                
                if (mountType == lastMountType) continue;
                
                switch (mountType)
                {
                    case MountType.None:
                    {
                        if (health.ValueRO.maxHealth > 2)
                        {
                            health.ValueRW.maxHealth = 2;
                            health.ValueRW.health = 2;
                        }

                        if (indestructible.ValueRO)
                            indestructible.ValueRW = false;
                        break;
                    }
                    case MountType.IncreaseHP:
                    {
                        if (health.ValueRO.maxHealth <= 2)
                        {
                            health.ValueRW.maxHealth = 10;
                            health.ValueRW.health = 10;
                        }

                        if (indestructible.ValueRO)
                            indestructible.ValueRW = false;
                        break;
                    }
                    case MountType.Indestructible:
                    {
                        if (health.ValueRO.maxHealth > 2)
                        {
                            health.ValueRW.maxHealth = 2;
                            health.ValueRW.health = 2;
                        }

                        if (!indestructible.ValueRO)
                            indestructible.ValueRW = true;
                        break;
                    }
                }

                mounted.ValueRW.lastMountType = mountType;
            }
        }
    }
}