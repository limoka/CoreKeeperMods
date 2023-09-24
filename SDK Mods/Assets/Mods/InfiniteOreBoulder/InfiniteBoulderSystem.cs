using PugAutomation;
using Unity.Entities;
using Unity.NetCode;

namespace InfiniteOreBoulder
{
    [UpdateInGroup(typeof(ServerSimulationSystemGroup))]
    public partial class InfiniteBoulderSystem : PugSimulationSystemBase
    {
        protected override void OnUpdate()
        {
            Entities.ForEach((ref HealthCD healthCd, in DropsLootWhenDamagedCD dropsLootCd) =>
                {
                    if (healthCd.health < healthCd.maxHealth - dropsLootCd.damageToDealToDropLoot)
                    {
                        healthCd.health = healthCd.maxHealth;
                    }
                })
                .WithName("BoulderHeal")
                .WithBurst()
                .WithAll<PugAutomationCD>()
                .WithAll<MineableDamageDecreaseCD>()
                .WithNone<EntityDestroyedCD>()
                .WithEntityQueryOptions(EntityQueryOptions.IncludeDisabled)
                .Schedule();

            base.OnUpdate();
        }
    }
}