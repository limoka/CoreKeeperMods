using System;
using CoreLib.Submodule.EquipmentSlot;
using Unity.Entities;
using Unity.NetCode;

namespace SecureAttachment.Systems
{
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct ClientModCommandSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BeginSimulationEntityCommandBufferSystem.Singleton>();
        }
        
        public void OnUpdate(ref SystemState state)
        {
            var ecbSingleton = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);
            
            foreach (var (rpc, rpcEntity) in SystemAPI.Query<SecureAttachmentRPC>()
                         .WithAll<ReceiveRpcCommandRequest>()
                         .WithEntityAccess())
            {
                var command = rpc.command;

                if (command == MessageType.MessageWrenchMode)
                    ShowWrenchModeMessage(rpc);

                ecb.DestroyEntity(rpcEntity);
            }
        }
        
        private static void ShowWrenchModeMessage(SecureAttachmentRPC message)
        {
            var pc = Manager.main.player;
            if (pc == null) return;

            var mode = message.wrenchMode;
            Emote.EmoteType emote = mode switch
            {
                WrenchMode.Auto => SecureAttachmentMod.emoteWrenchAuto,
                WrenchMode.Mount => SecureAttachmentMod.emoteWrenchMount,
                WrenchMode.Unmount => SecureAttachmentMod.emoteWrenchUnmount,
                _ => throw new ArgumentOutOfRangeException()
            };

            EquipmentSlotModule.SpawnModEmoteText(pc.center, emote);
        }
    }
}