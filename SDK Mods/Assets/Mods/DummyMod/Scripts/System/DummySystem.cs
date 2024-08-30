using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace DummyMod
{
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(UpdateHealthSystemGroup))]
    [UpdateBefore(typeof(UpdateHealthFromBufferSystem))]
    public partial class DummySystem : PugSimulationSystemBase
    {
        private Entity healthChangeBufferEntity;

        protected override void OnCreate()
        {
            RequireForUpdate<HealthChangeBuffer>();
            base.OnCreate();
        }

        protected override void OnStartRunning()
        {
            healthChangeBufferEntity = SystemAPI.GetSingletonEntity<HealthChangeBuffer>();
            base.OnStartRunning();
        }

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
                .WithoutBurst()
                .Schedule();

            var time = SystemAPI.Time;
            var healthChangeBufferEntityLocal = healthChangeBufferEntity;

            Job.WithCode(() =>
                {
                    var healthChangeBuffer = SystemAPI.GetBuffer<HealthChangeBuffer>(healthChangeBufferEntityLocal);
                    for (int i = 0; i < healthChangeBuffer.Length; i++)
                    {
                        Entity entity = healthChangeBuffer[i].healthChange.entity;
                        if (!SystemAPI.HasComponent<DummyCD>(entity)) return;

                        var dummy = SystemAPI.GetComponent<DummyCD>(entity);

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
                        dummy.damageSum += healthChange;
                        SystemAPI.SetComponent(entity, dummy);
                    }
                })
                .WithoutBurst()
                .Schedule();

            Entities.ForEach((
                    ref DummyCD dummy,
                    ref DynamicBuffer<DummyDamageBuffer> damageBuffer) =>
                {
                    dummy.deltaTimeSum += time.DeltaTime;
                    dummy.ticksElapsed++;

                    if (dummy.ticksElapsed >= 3)
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


                    var totalDamage = 0;
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
                .WithoutBurst()
                .Schedule();

            base.OnUpdate();
        }
    }
}