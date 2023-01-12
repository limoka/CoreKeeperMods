using System;
using PugAutomation;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace InfiniteOreBoulder
{
    public class InfiniteOreBoulderSystem : MonoBehaviour
    {
        public static InfiniteOreBoulderSystem instance;
        internal World serverWorld;
        
        private float waitTime;
        private const float refreshTime = 5;
        
        public InfiniteOreBoulderSystem(IntPtr ptr) : base(ptr) { }
        
        private void Awake()
        {
            instance = this;
        }

        public void OnServerStarted(World world)
        {
            serverWorld = world;
        }

        private void Update()
        {
            if (serverWorld == null) return;
            
            waitTime -= Time.deltaTime;

            if (waitTime <= 0)
            {
                EntityQuery query =
                    serverWorld.EntityManager.CreateEntityQuery(
                        ComponentType.ReadOnly<DropsLootWhenDamagedCD>(),
                        ComponentType.ReadOnly<PugAutomationCD>(),
                        ComponentType.ReadOnly<MineableDamageDecreaseCD>(),
                        ComponentType.ReadOnly<HealthCD>());

                NativeArray<Entity> result = query.ToEntityArray(Allocator.Temp);

                foreach (Entity entity in result)
                {
                    HealthCD healthCd = serverWorld.EntityManager.GetComponentData<HealthCD>(entity);
                    
                    if (healthCd.health < healthCd.maxHealth)
                    {
                        healthCd.health = healthCd.maxHealth;
                        serverWorld.EntityManager.SetComponentData(entity, healthCd);
                    }
                }
                
                waitTime = refreshTime;
            }
        }
    }
}