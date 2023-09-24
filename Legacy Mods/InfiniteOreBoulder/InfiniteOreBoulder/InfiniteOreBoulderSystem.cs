using System;
using CoreLib.Submodules.ModComponent;
using CoreLib.Submodules.ModSystem;
using CoreLib.Util.Extensions;
using PugAutomation;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace InfiniteOreBoulder
{
    public class InfiniteOreBoulderSystem : MonoBehaviour, IPseudoServerSystem
    {
        internal World serverWorld;
        internal EntityQuery query;
        internal ModComponentDataFromEntity<HealthCD> healthGroup;

        private float waitTime;
        private const float refreshTime = 30;

        public InfiniteOreBoulderSystem(IntPtr ptr) : base(ptr) { }

        public void OnServerStarted(World world)
        {
            serverWorld = world;
            query = serverWorld.EntityManager.CreateEntityQuery(
                new EntityQueryDesc
                {
                    All = new[]
                    {
                        ComponentType.ReadOnly<DropsLootWhenDamagedCD>(),
                        ComponentType.ReadOnly<PugAutomationCD>(),
                        ComponentType.ReadOnly<MineableDamageDecreaseCD>(),
                        ComponentType.ReadOnly<HealthCD>()
                    },
                    Any = Array.Empty<ComponentType>(),
                    None = new[] { ComponentType.ReadOnly<EntityDestroyedCD>() },
                    Options = EntityQueryOptions.IncludeDisabled
                });
            healthGroup = new ModComponentDataFromEntity<HealthCD>(serverWorld.EntityManager);
        }

        public void OnServerStopped()
        {
            serverWorld = null;
        }

        private void FixedUpdate()
        {
            if (serverWorld == null) return;

            waitTime -= Time.fixedDeltaTime;

            if (waitTime <= 0)
            {
                healthGroup = new ModComponentDataFromEntity<HealthCD>(serverWorld.EntityManager);
                NativeArray<Entity> result = query.ToEntityArray(Allocator.Temp);

                foreach (Entity entity in result)
                {
                    HealthCD healthCd = healthGroup[entity];
                    if (healthCd.health < healthCd.maxHealth - 1000)
                    {
                        healthCd.health = healthCd.maxHealth;
                        healthGroup[entity] = healthCd;
                    }
                }

                waitTime = refreshTime;
            }
        }
    }
}