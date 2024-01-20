using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace DummyMod
{
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(UpdateHealthFromBufferSystem))]
    public partial class DummySystem : PugSimulationSystemBase
    {
        protected override void OnUpdate()
        {
            var ecb = CreateCommandBuffer();

            Entities.ForEach((Entity entity, ref ObjectDataCD objectData) =>
                {
                    objectData.variation = 1;
                    objectData.variationUpdateCount++;

                    ecb.RemoveComponent<SpawnDummyCD>(entity);
                })
                .WithAll<SpawnDummyCD>()
                .Schedule();

            var time = SystemAPI.Time;

            Entities.ForEach((
                    ref DummyCD dummy,
                    ref DynamicBuffer<DummyDamageBuffer> damageBuffer,
                    ref DynamicBuffer<HealthChangeBuffer> healthChangeBuffer) =>
                {
                    int totalDamage = 0;

                    for (int i = 0; i < healthChangeBuffer.Length; i++)
                    {
                        var healthChange = healthChangeBuffer[i].healthChange.amount;
                        if (healthChange >= 0) continue;

                        healthChange *= -1;

                        if (healthChange < dummy.minDamage)
                            dummy.minDamage = healthChange;

                        if (healthChange > dummy.maxDamage)
                            dummy.maxDamage = healthChange;

                        //New average = old average * (n-1)/n + new value /n
                        int prevAverage = dummy.oldAverageDamage;
                        dummy.oldAverageDamage = dummy.averageDamage;
                        dummy.damageCount++;

                        dummy.averageDamage = prevAverage * (dummy.damageCount - 1) / dummy.damageCount + healthChange / dummy.damageCount;

                        dummy.lastDamage = healthChange;
                        totalDamage += healthChange;
                    }

                    dummy.damageSum += totalDamage;
                    dummy.deltaTimeSum += time.DeltaTime;
                    dummy.ticksElapsed++;
                    
                    if (dummy.ticksElapsed >= 5)
                    {
                        if (damageBuffer.Length >= 60)
                        {
                            damageBuffer.RemoveAt(0);
                        }

                        damageBuffer.Add(new DummyDamageBuffer()
                        {
                            damage = dummy.damageSum,
                            deltaTime = dummy.deltaTimeSum
                        });

                        dummy.damageSum = 0;
                        dummy.deltaTimeSum = 0;
                        dummy.ticksElapsed = 0;
                    }


                    totalDamage = 0;
                    float elapsedTime = 0;
                    
                    for (int i = 0; i < damageBuffer.Length; i++)
                    {
                        var damage = damageBuffer[i];
                        totalDamage += damage.damage;
                        elapsedTime += damage.deltaTime;
                    }

                    dummy.damagePerSecond = (int)math.round(totalDamage / elapsedTime);
                    
                    if (dummy.damagePerSecond > dummy.maxDamagePerSecond)
                        dummy.maxDamagePerSecond = dummy.damagePerSecond;
                })
                .Schedule();

            base.OnUpdate();
        }
    }
}