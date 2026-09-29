using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace DummyMod
{
    [UpdateInGroup(typeof(RunSimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial class ClientModCommandSystem : PugSimulationSystemBase
    {
        private NativeQueue<DummyCommandRPC> rpcQueue;
        private EntityArchetype rpcArchetype;

        protected override void OnCreate()
        {
            UpdatesInRunGroup();
            rpcQueue = new NativeQueue<DummyCommandRPC>(Allocator.Persistent);
            rpcArchetype = EntityManager.CreateArchetype(typeof(DummyCommandRPC), typeof(SendRpcCommandRequest));

            base.OnCreate();
        }

        #region Commands

        public void ResetDummy(Entity target)
        {
            rpcQueue.Enqueue(new DummyCommandRPC()
            {
                commandType = DummyCommandType.RESET_DUMMY,
                entity0 = target,
            });
        }

        public void KillDummy(Entity target)
        {
            rpcQueue.Enqueue(new DummyCommandRPC()
            {
                commandType = DummyCommandType.DESTROY_DUMMY,
                entity0 = target
            });
        }

        #endregion

        protected override void OnUpdate()
        {
            EntityCommandBuffer entityCommandBuffer = CreateCommandBuffer();
            while (rpcQueue.TryDequeue(out DummyCommandRPC component))
            {
                Entity e = entityCommandBuffer.CreateEntity(rpcArchetype);
                entityCommandBuffer.SetComponent(e, component);
            }
        }
    }
}