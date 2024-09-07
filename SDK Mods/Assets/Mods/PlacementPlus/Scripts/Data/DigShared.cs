using Inventory;
using PlayerEquipment;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.NetCode;
using Unity.Physics;

namespace PlacementPlus
{
    public struct DigShared
    {
        public PhysicsWorldHistorySingleton physicsWorldHistory;
        [ReadOnly] public PhysicsWorld physicsWorld;
        public ColliderCacheCD colliderCache;
     
        [NativeDisableUnsafePtrRestriction] private EntityQuery physicsWorldHistoryQuery;
        [NativeDisableUnsafePtrRestriction] private EntityQuery physicsWorldQuery;

        public DigShared(ref SystemState state)
        {
            physicsWorldHistory = new PhysicsWorldHistorySingleton();
            physicsWorld = new PhysicsWorld();
            colliderCache = new ColliderCacheCD();
            physicsWorldHistoryQuery = state.GetEntityQuery(ComponentType.ReadOnly<PhysicsWorldHistorySingleton>());
            physicsWorldQuery = state.GetEntityQuery(ComponentType.ReadOnly<PhysicsWorldSingleton>());
            
            state.RequireForUpdate<PhysicsWorldSingleton>();
            state.RequireForUpdate<WorldInfoCD>();
            state.RequireForUpdate<ColliderCacheCD>();
        }
        
        public void Init(ref SystemState state)
        {
            colliderCache = state.GetSingleton<ColliderCacheCD>();
        }
        
        public void Update(ref SystemState state)
        {
            physicsWorldHistory = physicsWorldHistoryQuery.GetSingleton<PhysicsWorldHistorySingleton>();
            physicsWorld = physicsWorldQuery.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld;
        }
    }
}