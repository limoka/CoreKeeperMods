using System;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace InstantPortalCharge
{
    public class PortalChargeSystem : MonoBehaviour
    {
        public static PortalChargeSystem instance;

        internal World serverWorld;

        private float waitTime;
        private const float refreshTime = 5;

        public PortalChargeSystem(IntPtr ptr) : base(ptr) { }

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
                    serverWorld.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<PortalCD>());

                NativeArray<Entity> result = query.ToEntityArray(Allocator.Temp);

                foreach (Entity entity in result)
                {
                    ObjectDataCD objectData = EntityUtility.GetObjectData(entity, serverWorld);
                    if (objectData.amount < 1200)
                    {
                        objectData.amount = 1200;
                        serverWorld.EntityManager.SetComponentData(entity, objectData);
                    }
                }

                result.Dispose();
                waitTime = refreshTime;
            }
        }
    }
}