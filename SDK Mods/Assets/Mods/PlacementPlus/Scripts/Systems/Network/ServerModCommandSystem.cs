using System;
using System.Text;
using PlacementPlus.Commands;
using PlacementPlus.Components;
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

        protected override void OnCreate()
        {
            NeedDatabase();
            base.OnCreate();
        }

        protected override void OnUpdate()
        {
            InitColorIndexLookup();

            bool guestMode = WorldInfo.guestMode;
            var ecb = CreateCommandBuffer();

            var colorIndexLookupLocal = colorIndexLookup;
            var databaseLocal = database;

            var paintToolLookup = GetComponentLookup<PaintToolCD>(true);
            var maxPaintIndexLocal = maxPaintIndex;
            
            var networkTime = SystemAPI.GetSingleton<NetworkTime>();
            var currentTick = networkTime.ServerTick;

            Entities.ForEach((Entity rpcEntity, in PlacementPlusRPC rpc, in ReceiveRpcCommandRequest req) =>
                {
                    PlacementPlusMod.Log.LogInfo($"Got PlacementPlusRPC: {rpc.commandType}, player: {rpc.player}, value: {rpc.valueChange}");
                    if (rpc.commandType == ModCommandType.UNDEFINED) return;

                    if (!SystemAPI.HasComponent<PlacementPlusState>(rpc.player))
                    {
                        PlacementPlusMod.Log.LogInfo($"Something is wrong! Player {rpc.player} doesn't have PlacementPlusState!");
                        ecb.DestroyEntity(rpcEntity);
                        return;
                    }
                    
                    var placementState = SystemAPI.GetComponent<PlacementPlusState>(rpc.player);

                    switch (rpc.commandType)
                    {
                        case ModCommandType.CHANGE_SIZE:

                            placementState.ChangeSize(rpc.valueChange, currentTick);
                            ecb.SetComponent(rpc.player, placementState);
                            break;

                        case ModCommandType.CHANGE_TOOL_MODE:

                            int adminLevel = 0;
                            if (SystemAPI.HasComponent<ConnectionAdminLevelCD>(req.SourceConnection))
                                adminLevel = SystemAPI.GetComponent<ConnectionAdminLevelCD>(req.SourceConnection).Value;

                            if (guestMode && adminLevel <= 0) break;

                            var inventory = SystemAPI.GetBuffer<ContainedObjectsBuffer>(rpc.player);
                            var clientInput = SystemAPI.GetComponent<ClientInput>(rpc.player);

                            ref var item = ref inventory.ElementAt(clientInput.equippedSlotIndex);

                            ToggleToolMode(
                                colorIndexLookupLocal,
                                databaseLocal,
                                paintToolLookup,
                                ref placementState,
                                ref item,
                                rpc.valueChange > 0,
                                maxPaintIndexLocal
                            );
                            
                            ecb.SetComponent(rpc.player, placementState);

                            break;

                        case ModCommandType.CHANGE_ORIENTATION:

                            placementState.ToggleMode(currentTick);
                            ecb.SetComponent(rpc.player, placementState);
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

        private static void ToggleToolMode(
            NativeHashMap<int, ObjectID> colorIndexLookup,
            BlobAssetReference<PugDatabase.PugDatabaseBank> database,
            ComponentLookup<PaintToolCD> paintToolLookup,
            ref PlacementPlusState state,
            ref ContainedObjectsBuffer item,
            bool backwards,
            int maxPaintIndex)
        {
            if (item.objectID == ObjectID.None) return;

            ref var objectInfo = ref PugDatabase.GetEntityObjectInfo(item.objectID, database, item.variation);
            if (objectInfo.objectID == ObjectID.None) return;

            Entity prefabEntity = objectInfo.prefabEntities[0];

            if (paintToolLookup.HasComponent(prefabEntity))
            {
                var paintTool = paintToolLookup[prefabEntity];
                CyclePaintBrush(colorIndexLookup, ref item, ref state, paintTool, backwards, maxPaintIndex);
            }
            else if (objectInfo.objectType == ObjectType.RoofingTool)
            {
                state.ToggleRoofingMode(backwards);
            }
            else if (objectInfo.tileType == TileType.wall)
            {
                state.ToggleBlockMode(backwards);
            }
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