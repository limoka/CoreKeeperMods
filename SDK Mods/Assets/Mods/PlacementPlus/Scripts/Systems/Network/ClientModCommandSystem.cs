using System;
using PlacementPlus.Commands;
using PugMod;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

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
            rpcQueue.Enqueue(new PlacementPlusRPC
            {
                commandType = ModCommandType.CHANGE_SIZE,
                player = player,
                valueChange = direction
            });
        }
        
        public void ChangeToolMode(Entity player, bool direction)
        {
            rpcQueue.Enqueue(new PlacementPlusRPC
            {
                commandType = ModCommandType.CHANGE_TOOL_MODE,
                player = player,
                valueChange = direction ? 1 : 0
            });
        }
        
        public void ChangeOrientation(Entity player)
        {
            rpcQueue.Enqueue(new PlacementPlusRPC
            {
                commandType = ModCommandType.CHANGE_ORIENTATION,
                player = player
            });
        }
        
        public void SetReplaceState(Entity player, bool state)
        {
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
                Entity e = entityCommandBuffer.CreateEntity(rpcArchetype);
                entityCommandBuffer.SetComponent(e, component);
            }

            var ecb = CreateCommandBuffer();
            
            Entities.ForEach((Entity rpcEntity, in PlacementMessageRPC rpc) =>
                {
                    switch (rpc.messageType)
                    {
                        case ModMessageType.MODE_MESSAGE:
                            var mode1 = (BrushMode)rpc.messageData;
                            ShowMessage("PlacementPlus/ModeMessage", mode1.ToString());
                            break;
                        /*case ModMessageType.ROOFING_MODE_MESSAGE:
                            var mode2 = (RoofingToolMode)rpc.messageData;
                            ShowMessage("PlacementPlus/RoofingToolModeMessage", mode2.ToString());
                            break;*/
                        case ModMessageType.BLOCK_MODE_MESSAGE:
                            var mode3 = (BlockMode)rpc.messageData;
                            ShowMessage("PlacementPlus/BlockToolModeMessage", mode3.ToString());
                            break;
                    }
                    
                    ecb.DestroyEntity(rpcEntity);
                })
                .WithAll<ReceiveRpcCommandRequest>()
                .WithoutBurst()
                .Run();
        }

        private static void ShowMessage(string baseMsg, string modeName)
        {
            string text = API.Localization.GetLocalizedTerm(baseMsg);
            string modeText = API.Localization.GetLocalizedTerm($"PlacementPlus/{modeName}");
            string emoteText = string.Format(text, modeText);
            Vector3 PlayerCenter = Manager.main.player.center;
            
            Emote_Patch.SpawnModEmoteText(PlayerCenter, emoteText);
        }
    }
}