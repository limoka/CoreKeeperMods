using PugAutomation;
using Unity.Entities;
using Unity.NetCode;

namespace InfiniteOreBoulder
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial class InfiniteBoulderSystem : PugSimulationSystemBase
    {
        protected override void OnUpdate()
        {
            Entities.ForEach((ref HealthCD healthCd) =>
                {
                    if (healthCd.health < healthCd.maxHealth / 2)
                    {
                        healthCd.health = healthCd.maxHealth;
                    }
                })
                .WithName("BoulderHeal")
                .WithBurst()
                .WithAll<PugAutomationCD>()
                .WithAll<DropsLootWhenDamagedCD>()
                .WithAll<MineableDamageDecreaseCD>()
                .WithNone<EntityDestroyedCD>()
                .WithEntityQueryOptions(EntityQueryOptions.IncludeDisabledEntities)
                .Schedule();

            base.OnUpdate();
        }
    }
}