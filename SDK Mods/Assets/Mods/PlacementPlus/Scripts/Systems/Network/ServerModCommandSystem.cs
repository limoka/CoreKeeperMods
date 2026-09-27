using System;
using System.Text;
using PlacementPlus.Commands;
using PlacementPlus.Components;
using PlayerEquipment;
using PugTilemap;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace PlacementPlus.Systems.Network
{
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial class ServerModCommandSystem : PugSimulationSystemBase
    {
        private NativeHashMap<int, ObjectID> colorIndexLookup;
        private int maxPaintIndex = -1;
        private EntityArchetype responseArchetype;

        protected override void OnCreate()
        {
            responseArchetype = EntityManager.CreateArchetype(typeof(PlacementMessageRPC), typeof(SendRpcCommandRequest));
            
            NeedDatabase();
            base.OnCreate();
        }

        protected override void OnUpdate()
        {
            InitColorIndexLookup();

            // Apply hammer size (M1, plan-deharmony): The EquipmentUpdateSystem prefix is not called 
            // on Burst-enabled servers, so this continuously running system handles it instead. 
            // ⚠️ An identical line exists in the .g.cs file (__OnUpdate_450AADF4) — that file's version 
            // is the one that actually executes.
            EquipmentSystem_Patch.ApplySledgeSize(ref CheckedStateRef, isServer: true);

            bool guestMode = WorldInfo.guestMode;
            bool creativeMode = WorldInfo.IsWorldModeEnabled(WorldMode.Creative);
            var ecb = CreateCommandBuffer();

            // Handle brush placement RPCs (M2, plan-deharmony) — This must occur before scheduling 
            // the lambda, ensuring that the lambda doesn't prematurely destroy BRUSH_PLACE entities 
            // within the exact same frame.
            // ⚠️ An identical line exists in the .g.cs file — that file's version is the one that actually executes.
            ServerBrushExecutor.Process(ref CheckedStateRef, ecb);

            var colorIndexLookupLocal = colorIndexLookup;
            var databaseLocal = database;

            var paintToolLookup = GetComponentLookup<PaintToolCD>(true);
            var maxPaintIndexLocal = maxPaintIndex;
            
            var networkTime = SystemAPI.GetSingleton<NetworkTime>();
            var currentTick = networkTime.ServerTick;

            var responseArchetypeLocal = responseArchetype;

            Entities.ForEach((Entity rpcEntity, in PlacementPlusRPC rpc, in ReceiveRpcCommandRequest req) =>
                {
                    if (rpc.commandType == ModCommandType.UNDEFINED) return;

                    if (!SystemAPI.HasComponent<PlacementPlusState>(rpc.player))
                    {
                        PlacementPlusMod.Log.LogInfo($"Something is wrong! Player {rpc.player} doesn't have PlacementPlusState!");
                        ecb.DestroyEntity(rpcEntity);
                        return;
                    }
                    
                    var placementState = SystemAPI.GetComponent<PlacementPlusState>(rpc.player);
                    var inventory = SystemAPI.GetBuffer<ContainedObjectsBuffer>(rpc.player);
                    var clientInput = SystemAPI.GetComponent<ClientInput>(rpc.player);
                    ref var item = ref inventory.ElementAt(clientInput.equippedSlotIndex);

                    switch (rpc.commandType)
                    {
                        case ModCommandType.CHANGE_SIZE:
                            
                            ref var objectInfo = ref PugDatabase.GetEntityObjectInfo(item.objectID, databaseLocal, item.variation);
                            if (objectInfo.objectID == ObjectID.None) return;

                            Entity prefabEntity = objectInfo.prefabEntities[0];

                            // Sledgehammers: the hit collider is shared by every
                            // player holding that item, so the size is a single
                            // server-wide value — admins only (offline sessions
                            // report max privileges, so singleplayer always
                            // passes). Mirrored in the .g.cs — that copy runs.
                            if (objectInfo.objectType == ObjectType.Sledge)
                            {
                                int sledgeAdmin = 0;
                                if (SystemAPI.HasComponent<ConnectionAdminLevelCD>(req.SourceConnection))
                                    sledgeAdmin = SystemAPI.GetComponent<ConnectionAdminLevelCD>(req.SourceConnection).adminPrivileges;

                                if (sledgeAdmin <= 0)
                                {
                                    SendResponseMessage(responseArchetypeLocal, ecb,
                                        ModMessageType.SLEDGE_SIZE_MESSAGE, -1, req.SourceConnection);
                                    break;
                                }

                                int step = math.clamp(
                                    PlacementPlusMod.sledgeSize.Value + rpc.valueChange, 0, 3);
                                PlacementPlusMod.sledgeSize.Value = step;

                                // Entity.Null target = broadcast, so every client
                                // updates its synced copy.
                                SendResponseMessage(responseArchetypeLocal, ecb,
                                    ModMessageType.SLEDGE_SIZE_MESSAGE, step, Entity.Null);
                                break;
                            }

                            // Tools can enter the size path even if they lack ResizableTileSizeCD — 
                            // Tools that didn't have it (lower-tier shovels) were blocked by this gate, 
                            // making the +/- size adjustments completely unresponsive (Observed by SirSephiroth1 on 08-28).
                            bool sizeByVariation = SystemAPI.HasComponent<ResizableTileSizeCD>(prefabEntity) ||
                                objectInfo.objectType == ObjectType.Shovel ||
                                objectInfo.objectType == ObjectType.Hoe ||
                                objectInfo.objectType == ObjectType.RoofingTool ||
                                objectInfo.objectType == ObjectType.WaterCan;
                            if (sizeByVariation)
                            {
                                var placementSizeBuffer = SystemAPI.GetBuffer<PlacementSizeByEquipmentTypeBuffer>(rpc.player);
                                var equipmentSlot = SystemAPI.GetComponent<EquipmentSlotCD>(rpc.player);
                                
                                // If the size is adjusted during suppress(slotType=100), it gets saved to the ghost element (100), 
                                // causing a desync with the client preview (which tracks the normal slot element). 
                                // We restore the original slot using the item type to normalize the key (Observed on 08-25: 
                                // roofing behavior scaled properly by size, but the preview itself remained stuck at 9x9).
                                // An identical block exists in the .g.cs file — that file's version is the one that actually executes.
                                var sizeSlotType = equipmentSlot.slotType;
                                if (sizeSlotType == (EquipmentSlotType)100)
                                    sizeSlotType = objectInfo.objectType == ObjectType.RoofingTool
                                        ? EquipmentSlotType.RoofingToolSlot
                                        : objectInfo.objectType == ObjectType.PaintTool
                                            ? EquipmentSlotType.PaintToolSlot
                                            : objectInfo.objectType == ObjectType.Shovel
                                                ? EquipmentSlotType.ShovelSlot
                                                : objectInfo.objectType == ObjectType.Hoe
                                                    ? EquipmentSlotType.HoeSlot
                                                    : EquipmentSlotType.PlaceObjectSlot;
                                ref var element = ref placementSizeBuffer.GetElementForEquipment(sizeSlotType);

                                var size = (int)element.sizeVariationToPlace;

                                // StartOnSmallestSize says where the game starts
                                // the tool, not which way the size runs:
                                // GetTileSizeFromVariation is min(var, cap) + 1,
                                // always growing with var. Deriving the sign from
                                // it made "+" shrink the roofing gadget.
                                // Mirrored in the .g.cs — that is the copy that
                                // actually runs.
                                size += rpc.valueChange;
                                
                                if (size <= 0)
                                    size = 0;
                                if (size >= objectInfo.prefabTileSize.x - 1)
                                    size = objectInfo.prefabTileSize.x - 1;

                                element.sizeVariationToPlace = (byte)size;
                                        
                                break;
                            }
                            
                            placementState.ChangeSize(rpc.valueChange, currentTick);
                            ecb.SetComponent(rpc.player, placementState);
                            break;

                        case ModCommandType.CHANGE_TOOL_MODE:

                            int adminLevel = 0;
                            if (SystemAPI.HasComponent<ConnectionAdminLevelCD>(req.SourceConnection))
                                adminLevel = SystemAPI.GetComponent<ConnectionAdminLevelCD>(req.SourceConnection).adminPrivileges;

                            // Sledgehammer: the tool-mode key toggles square/arc.
                            // Shared value like the size — admins only. Mirrored
                            // in the .g.cs, that copy runs.
                            ref var sledgeInfo = ref PugDatabase.GetEntityObjectInfo(item.objectID, databaseLocal, item.variation);
                            if (sledgeInfo.objectType == ObjectType.Sledge)
                            {
                                if (adminLevel <= 0)
                                {
                                    SendResponseMessage(responseArchetypeLocal, ecb,
                                        ModMessageType.SLEDGE_SHAPE_MESSAGE, -1, req.SourceConnection);
                                    break;
                                }

                                bool square = !PlacementPlusMod.sledgeSquare.Value;
                                PlacementPlusMod.sledgeSquare.Value = square;

                                SendResponseMessage(responseArchetypeLocal, ecb,
                                    ModMessageType.SLEDGE_SHAPE_MESSAGE, square ? 1 : 0, Entity.Null);
                                break;
                            }

                            if (guestMode && adminLevel <= 0) break;

                            var resultMessage = ToggleToolMode(
                                colorIndexLookupLocal,
                                databaseLocal,
                                paintToolLookup,
                                ref placementState,
                                ref item,
                                rpc.valueChange > 0,
                                maxPaintIndexLocal,
                                creativeMode
                            );

                            if (resultMessage.messageType != ModMessageType.UNDEFINED)
                            {
                                SendResponseMessage(
                                    responseArchetypeLocal,
                                    ecb,
                                    resultMessage.messageType,
                                    resultMessage.messageData,
                                    req.SourceConnection
                                );
                            }
                            
                            ecb.SetComponent(rpc.player, placementState);

                            break;

                        case ModCommandType.CHANGE_ORIENTATION:

                            placementState.ToggleMode(currentTick);

                            // For shovel/hoe/roofing OFF behaves exactly like
                            // SQUARE (their size is the tool's own), so their
                            // cycle skips it. Mirrored in the .g.cs — that copy
                            // runs.
                            ref var orientInfo = ref PugDatabase.GetEntityObjectInfo(item.objectID, databaseLocal, item.variation);
                            if (placementState.mode == BrushMode.NONE &&
                                (orientInfo.objectType == ObjectType.Shovel ||
                                 orientInfo.objectType == ObjectType.Hoe ||
                                 orientInfo.objectType == ObjectType.RoofingTool))
                            {
                                placementState.ToggleMode(currentTick);
                            }

                            ecb.SetComponent(rpc.player, placementState);

                            SendResponseMessage(
                                responseArchetypeLocal,
                                ecb,
                                ModMessageType.MODE_MESSAGE,
                                (int)placementState.mode,
                                req.SourceConnection
                            );
                            break;

                        case ModCommandType.SET_REPLACE:

                            placementState.replaceTiles = rpc.valueChange > 0;
                            ecb.SetComponent(rpc.player, placementState);
                            break;

                    }

                    ecb.DestroyEntity(rpcEntity);
                })
                .WithoutBurst()
                .Schedule();

            base.OnUpdate();
        }

        private static void SendResponseMessage(
            EntityArchetype messageArchetype,
            EntityCommandBuffer ecb,
            ModMessageType message,
            int data,
            Entity targetConnection
        )
        {
            Entity e = ecb.CreateEntity(messageArchetype);
            ecb.SetComponent(e, new PlacementMessageRPC()
            {
                messageType = message,
                messageData = data
            });
            ecb.SetComponent(e, new SendRpcCommandRequest
            {
                TargetConnection = targetConnection
            });
        }
        

        private static PlacementMessageRPC ToggleToolMode(
            NativeHashMap<int, ObjectID> colorIndexLookup,
            BlobAssetReference<PugDatabase.PugDatabaseBank> database,
            ComponentLookup<PaintToolCD> paintToolLookup,
            ref PlacementPlusState state,
            ref ContainedObjectsBuffer item,
            bool backwards,
            int maxPaintIndex,
            bool creativeMode)
        {
            if (item.objectID == ObjectID.None) return default;

            ref var objectInfo = ref PugDatabase.GetEntityObjectInfo(item.objectID, database, item.variation);
            if (objectInfo.objectID == ObjectID.None) return default;

            Entity prefabEntity = objectInfo.prefabEntities[0];

            if (paintToolLookup.HasComponent(prefabEntity))
            {
                var paintTool = paintToolLookup[prefabEntity];
                CyclePaintBrush(colorIndexLookup, ref item, ref state, paintTool, backwards, maxPaintIndex);
                return default;
            }

            if (objectInfo.objectType == ObjectType.RoofingTool)
            {
                return state.ToggleRoofingMode(backwards);
            }
            
            if (objectInfo.tileType == TileType.wall)
            {
                return state.ToggleBlockMode(backwards, creativeMode);
            }

            return default;
        }

        private void InitColorIndexLookup()
        {
            if (colorIndexLookup.IsCreated) return;

            colorIndexLookup = new NativeHashMap<int, ObjectID>(16, Allocator.Persistent);


            Entities.ForEach((
                    in ObjectDataCD objectDataCd,
                    in PaintToolCD paintToolCd) =>
                {
                    if (paintToolCd.paintIndex == 0) return;

                    colorIndexLookup.Add(paintToolCd.paintIndex, objectDataCd.objectID);
                    maxPaintIndex = math.max(maxPaintIndex, paintToolCd.paintIndex);
                })
                .WithAll<Prefab>()
                .WithoutBurst()
                .Run();
            PlacementPlusMod.Log.LogInfo($"InitColorIndexLookup Done, {colorIndexLookup.Count} brushes!");
        }

        private static void CyclePaintBrush(
            NativeHashMap<int, ObjectID> colorIndexLookup,
            ref ContainedObjectsBuffer item,
            ref PlacementPlusState state,
            PaintToolCD paintToolCd,
            bool shift,
            int maxPaintIndex)
        {
            if (state.lastColorIndex == 0)
                state.lastColorIndex = paintToolCd.paintIndex;

            state.lastColorIndex += shift ? -1 : 1;
            if (state.lastColorIndex < 0)
                state.lastColorIndex = maxPaintIndex - 1;

            if (state.lastColorIndex > maxPaintIndex)
                state.lastColorIndex = 1;

            item.objectData.objectID = colorIndexLookup[state.lastColorIndex];
        }
    }
}