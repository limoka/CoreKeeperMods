using System;
using I2.Loc;
using PlacementPlus.Commands;
using PugMod;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using UnityEngine;

namespace PlacementPlus.Systems.Network
{
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial class ClientModCommandSystem : PugSimulationSystemBase
    {
        private NativeQueue<PlacementPlusRPC> rpcQueue;
        private EntityArchetype rpcArchetype;

        // M2 (plan-deharmony): Brush placement commands require an anchor payload (int3), 
        // so they use a separate RPC type — we cannot add fields to the existing struct 
        // because its serializer is locked into a generated `.g.cs` file.
        private NativeQueue<BrushPlaceRPC> brushRpcQueue;
        private EntityArchetype brushRpcArchetype;

        protected override void OnCreate()
        {
            UpdatesInRunGroup();
            rpcQueue = new NativeQueue<PlacementPlusRPC>(Allocator.Persistent);
            rpcArchetype = EntityManager.CreateArchetype(typeof(PlacementPlusRPC), typeof(SendRpcCommandRequest));

            brushRpcQueue = new NativeQueue<BrushPlaceRPC>(Allocator.Persistent);
            brushRpcArchetype = EntityManager.CreateArchetype(typeof(BrushPlaceRPC), typeof(SendRpcCommandRequest));

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
        
        // M2 (plan-deharmony): On the exact frame the client patch executes a placement, 
        // it also commands the server to execute it — since the server-side patch is inactive 
        // on Burst-enabled servers, this RPC is the sole execution path. 
        // anchor = The grid anchor calculated by the client.
        public void SendBrushPlace(Entity player, int3 anchor, int kind = BrushKind.Place)
        {
            brushRpcQueue.Enqueue(new BrushPlaceRPC
            {
                player = player,
                anchor = anchor,
                kind = kind
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

            // M2: Brush placement command drain. ⚠️ An identical block exists in the .g.cs file — 
            // that file's version is the one that actually executes.
            while (brushRpcQueue.TryDequeue(out BrushPlaceRPC brushComponent))
            {
                Entity e = entityCommandBuffer.CreateEntity(brushRpcArchetype);
                entityCommandBuffer.SetComponent(e, brushComponent);
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
                        case ModMessageType.ROOFING_MODE_MESSAGE:
                            var mode2 = (RoofingToolMode)rpc.messageData;
                            ShowMessage("PlacementPlus/RoofingToolModeMessage", mode2.ToString());
                            break;
                        case ModMessageType.BLOCK_MODE_MESSAGE:
                            var mode3 = (BlockMode)rpc.messageData;
                            ShowMessage("PlacementPlus/BlockToolModeMessage", mode3.ToString());
                            break;
                        case ModMessageType.SLEDGE_SIZE_MESSAGE:
                            if (rpc.messageData < 0)
                            {
                                ShowSimpleMessage( "AdminsOnly");
                                break;
                            }
                            PlacementPlusMod.sledgeSizeSynced = rpc.messageData;
                            int size = 3 + 2 * rpc.messageData;
                            ShowMessageFormatted("PlacementPlus/SledgeSize", size);
                            break;
                        case ModMessageType.SLEDGE_SYNC_MESSAGE:
                            // Connection synchronization — silent (updates 'synced' only, without a speech bubble).
                            // ⚠️ An identical case exists in the .g.cs file — that file's version is the one that actually executes.
                            PlacementPlusMod.sledgeSizeSynced = rpc.messageData / 2;
                            PlacementPlusMod.sledgeShapeSynced = rpc.messageData % 2;
                            break;
                        case ModMessageType.SLEDGE_SHAPE_MESSAGE:
                            if (rpc.messageData < 0)
                            {
                                ShowSimpleMessage( "AdminsOnly");
                                break;
                            }
                            PlacementPlusMod.sledgeShapeSynced = rpc.messageData;
                            var square = rpc.messageData == 1;
                            var message = square ? "SHAPE_SQUARE" : "SHAPE_ARC";
                            
                            ShowMessage("PlacementPlus/SledgeMode", message);
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
            string modeText = API.Localization.GetLocalizedTerm($"PlacementPlus/{modeName}");
            Vector3 playerCenter = Manager.main.player.center;
            
            if (PlacementPlusMod.shortMessages.Value)
            {
                Emote_Patch.SpawnModEmoteText(playerCenter, modeText);
            }
            else
            {
                string text = API.Localization.GetLocalizedTerm(baseMsg);
                string emoteText = string.Format(text, modeText);
            
                Emote_Patch.SpawnModEmoteText(playerCenter, emoteText);
            }
        }
        
        private static void ShowMessageFormatted(string baseMsg, int value)
        {
            string text = API.Localization.GetLocalizedTerm(baseMsg);
            string emoteText = string.Format(text, value);
            Vector3 playerCenter = Manager.main.player.center;
            
            Emote_Patch.SpawnModEmoteText(playerCenter, emoteText);
        }
        
        private static void ShowSimpleMessage(string msg)
        {
            string text = API.Localization.GetLocalizedTerm($"PlacementPlus/{msg}");
            Vector3 playerCenter = Manager.main.player.center;
            
            Emote_Patch.SpawnModEmoteText(playerCenter, text);
        }

        // These run as ordinary statics (the generated lambda calls them), so the
        // wording lives in one place. NONE reads as OFF because it means "brush
        // off" — 1x1 for placeables, vanilla for tools. The roofing stock mode
        // reads as DEFAULT, but the wall-block TOGGLE stays TOGGLE: from 2x2 up
        // it places ground and wall at the same time, which is not the vanilla
        // default behaviour — don't "fix" it to DEFAULT.


    }
}