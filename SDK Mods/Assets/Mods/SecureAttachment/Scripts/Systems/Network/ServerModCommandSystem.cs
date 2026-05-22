using Unity.Entities;
using Unity.NetCode;

namespace SecureAttachment.Systems
{
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct ServerModCommandSystem : ISystem
    {
        private EntityArchetype _responseArchetype;
        private ComponentLookup<WrenchStateCD> _wrenchStateLookup;

        public void OnCreate(ref SystemState state)
        {
            _responseArchetype =
                state.EntityManager.CreateArchetype(typeof(SecureAttachmentRPC), typeof(SendRpcCommandRequest));
            _wrenchStateLookup = state.GetComponentLookup<WrenchStateCD>();

            state.RequireForUpdate<BeginSimulationEntityCommandBufferSystem.Singleton>();
        }

        public void OnUpdate(ref SystemState state)
        {
            _wrenchStateLookup.Update(ref state);
            
            var ecbSingleton = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            // Process Incoming Messages
            foreach (var (rpc, req, rpcEntity) in
                     SystemAPI.Query<SecureAttachmentRPC, ReceiveRpcCommandRequest>()
                         .WithEntityAccess())
            {
                var command = rpc.command;

                if (command == MessageType.CycleWrenchMode)
                    CycleWrenchMode(ecb, rpc, req.SourceConnection);

                ecb.DestroyEntity(rpcEntity);
            }
        }
        
        private void CycleWrenchMode(
            EntityCommandBuffer ecb, 
            SecureAttachmentRPC message,
            Entity sourceConnection
        ){
            Entity player = message.player;
            if (!_wrenchStateLookup.HasComponent(player)) return;
            
            var wrenchState = _wrenchStateLookup[player];

            WrenchMode next = (WrenchMode)(((int)wrenchState.mode + 1) % (int)WrenchMode.Max);
            wrenchState.mode = next;

            ecb.SetComponent(player, wrenchState);

            var response = new SecureAttachmentRPC()
            {
                command = MessageType.MessageWrenchMode,
                wrenchMode = next
            };

            SendMessage(ecb, sourceConnection, response);
        }

        private void SendMessage(EntityCommandBuffer ecb, Entity sourceConnection, SecureAttachmentRPC response)
        {
            var res = ecb.CreateEntity(_responseArchetype);
            ecb.SetComponent(res, response);
            ecb.SetComponent(res, new SendRpcCommandRequest() {TargetConnection = sourceConnection});
        }
    }
}