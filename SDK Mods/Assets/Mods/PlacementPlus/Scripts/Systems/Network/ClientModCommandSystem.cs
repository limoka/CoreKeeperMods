using PlacementPlus.Commands;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace PlacementPlus.Systems.Network
{
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial class ClientModCommandSystem : PugSimulationSystemBase
    {
        private NativeQueue<PlacementPlusRPC> rpcQueue;
        private EntityArchetype rpcArchetype;

        protected override void OnCreate()
        {
            UpdatesInRunGroup();
            rpcQueue = new NativeQueue<PlacementPlusRPC>(Allocator.Persistent);
            rpcArchetype = EntityManager.CreateArchetype(typeof(PlacementPlusRPC), typeof(SendRpcCommandRequest));

            base.OnCreate();
        }

        #region Commands

        public void ChangeSize(Entity player, int direction)
        {
            PlacementPlusMod.Log.LogInfo($"Send command: {ModCommandType.CHANGE_SIZE}, player: {player}");
            rpcQueue.Enqueue(new PlacementPlusRPC
            {
                commandType = ModCommandType.CHANGE_SIZE,
                player = player,
                valueChange = direction
            });
        }
        
        public void ChangeToolMode(Entity player, bool direction)
        {
            PlacementPlusMod.Log.LogInfo($"Send command: {ModCommandType.CHANGE_TOOL_MODE}, player: {player}");
            rpcQueue.Enqueue(new PlacementPlusRPC
            {
                commandType = ModCommandType.CHANGE_TOOL_MODE,
                player = player,
                valueChange = direction ? 1 : 0
            });
        }
        
        public void ChangeOrientation(Entity player)
        {
            PlacementPlusMod.Log.LogInfo($"Send command: {ModCommandType.CHANGE_ORIENTATION}, player: {player}");
            rpcQueue.Enqueue(new PlacementPlusRPC
            {
                commandType = ModCommandType.CHANGE_ORIENTATION,
                player = player
            });
        }
        
        public void SetReplaceState(Entity player, bool state)
        {
            PlacementPlusMod.Log.LogInfo($"Send command: {ModCommandType.SET_REPLACE}, player: {player}");
            rpcQueue.Enqueue(new PlacementPlusRPC
            {
                commandType = ModCommandType.SET_REPLACE,
                player = player,
                valueChange = state ? 1 : 0
            });
        }

        #endregion

        protected override void OnUpdate()
        { 
            EntityCommandBuffer entityCommandBuffer = CreateCommandBuffer();
            while (rpcQueue.TryDequeue(out PlacementPlusRPC component))
            {
                PlacementPlusMod.Log.LogInfo($"Sending RPC: {component.commandType}");
                Entity e = entityCommandBuffer.CreateEntity(rpcArchetype);
                entityCommandBuffer.SetComponent(e, component);
            }
        }
    }
}