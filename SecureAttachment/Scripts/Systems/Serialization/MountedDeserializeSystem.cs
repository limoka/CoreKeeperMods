using HarmonyLib;
using Unity.Entities;

namespace SecureAttachment.Systems
{

    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SerializationSystemGroup))]
    [UpdateAfter(typeof(DeserializeComponentsSystem))]
    public partial struct MountedDeserializeSystem : ISystem
    {
        
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BeginSimulationEntityCommandBufferSystem.Singleton>();
        }


        public void OnUpdate(ref SystemState state)
        {
            var ecbSingleton = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);
            
            foreach (var (mounted, mealsEaten, entity) in
                     SystemAPI.Query<RefRW<MountedCD>, RefRO<MealsEatenCD>>()
                         .WithNone<MountStateLoadedCD>()
                         .WithOptions(EntityQueryOptions.IncludeDisabledEntities)
                         .WithEntityAccess())
            {
                mounted.ValueRW.lastMountType = MountType.Unknown;
                mounted.ValueRW.mountType = (MountType)mealsEaten.ValueRO.Value;
                
                ecb.AddComponent<MountStateLoadedCD>(entity);
            }
        }
    }
}