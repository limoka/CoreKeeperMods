using KeepFarming.Components;
using Unity.Entities;
using Unity.Entities.Internal;
using Unity.NetCode;
using Unity.Transforms;

namespace KeepFarming
{
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(RunSimulationSystemGroup))]
    public partial class MigrationSystem : PugSimulationSystemBase
    {
        internal float timeElapsed = 0;
        private bool migrationIsDone = false;
        private int count = 0;

        protected override void OnCreate()
        {
            UpdatesInRunGroup();
            NeedDatabase();
            base.OnCreate();
        }

        protected override void OnUpdate()
        {
            if (!KeepFarmingMod.migrationMode.Value) return;
            if (migrationIsDone) return;

            timeElapsed += SystemAPI.Time.DeltaTime;
            
            if (timeElapsed < 10) return;

            var ecb = CreateCommandBuffer();
            var databaseLocal = database;
             
            count = 0;
            Entities.ForEach((
                    Entity entity, 
                    ref ObjectDataCD objectData,
                    in LocalTransform transform,
                    in GrowingCD growingCd) =>
                {
                    if (objectData.variation == 4)
                    {
                        EntityUtility.CreateEntity(ecb, transform.Position, objectData.objectID, 1, databaseLocal, out Entity newEntity, 2);
                        ecb.SetComponent(newEntity, growingCd);
                        ecb.DestroyEntity(entity);
                        
                        count++;
                    }
                })
                .WithAll<PlantCD>()
                .WithAll<DropsGoldenSeedCD>()
                .WithEntityQueryOptions(EntityQueryOptions.IncludeDisabledEntities)
                .WithoutBurst()
                .Run();

            KeepFarmingMod.Log.LogInfo($"Migrated {count} plants!");
            
            count = 0;
            Entities.ForEach((
                    Entity entity,
                    ref ObjectDataCD objectData,
                    in LocalTransform transform,
                    in GrowingCD growingCd) =>
                {
                    if (objectData.variation == 2)
                    {
                        EntityUtility.CreateEntity(ecb, transform.Position, objectData.objectID, 1, databaseLocal, out Entity newEntity, 1);
                        ecb.SetComponent(newEntity, growingCd);
                        ecb.DestroyEntity(entity);
                        
                        count++;
                    }
                })
                //.WithAll<SeedCD>()
                .WithAll<GoldenSeedCD>()
                .WithoutBurst()
                .WithEntityQueryOptions(EntityQueryOptions.IncludeDisabledEntities)
                .Run();

            KeepFarmingMod.Log.LogInfo($"Migrated {count} planted seeds!");
            
            count = 0;
            Entities.ForEach((ref DynamicBuffer<ContainedObjectsBuffer> container) =>
            {
                for (int i = 0; i < container.Length; i++)
                {
                    var item = container[i];

                    if (//PugDatabase.HasComponent<SeedCD>(item.objectData) &&
                        PugDatabase.HasComponent<GoldenSeedCD>(item.objectData))
                    {
                        var objectData = item.objectData;
                        objectData.variation = 0;
                        container[i] = new ContainedObjectsBuffer()
                        {
                            objectData = objectData
                        };
                        count++;
                    }
                }
            })
                .WithAll<InventoryBuffer>()
                .WithEntityQueryOptions(EntityQueryOptions.IncludeDisabledEntities)
                .WithoutBurst()
                .Run();
            
            KeepFarmingMod.Log.LogInfo($"Migrated {count} items!");

            migrationIsDone = true;
            
            base.OnUpdate();
        }
    }
}