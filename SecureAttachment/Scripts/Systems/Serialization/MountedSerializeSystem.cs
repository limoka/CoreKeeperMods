using Unity.Entities;

namespace SecureAttachment.Systems
{
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SerializationSystemGroup))]
    [UpdateBefore(typeof(SerializeWorldSystem))]
    public partial struct MountedSerializeSystem : ISystem
    {
        private EntityQuery _triggerQ;
        private EntityQuery _blockSaveQ;

        public void OnCreate(ref SystemState state)
        {
            _triggerQ = state.GetEntityQuery(typeof(SerializeWorldSystem.SerializeWorld));
            _blockSaveQ = state.GetEntityQuery(typeof(BlockSaveCD));
            
            state.RequireForUpdate<SerializeWorldDataCD>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var serializeSingleton = SystemAPI.GetSingleton<SerializeWorldDataCD>();
        
            if (serializeSingleton.State != SerializeWorldState.Idle ||
                _triggerQ.IsEmpty ||
                !_blockSaveQ.IsEmpty)
            {
                return;
            }
            
            foreach (var (mounted, mealsEaten) in
                     SystemAPI.Query<RefRW<MountedCD>, RefRW<MealsEatenCD>>()
                         .WithOptions(EntityQueryOptions.IncludeDisabledEntities))
            {
                mealsEaten.ValueRW = new MealsEatenCD
                {
                    Value = (int)mounted.ValueRO.mountType
                };
            }
        }
    }
}