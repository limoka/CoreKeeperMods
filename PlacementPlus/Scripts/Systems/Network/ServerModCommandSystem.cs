using Mods.PlacementPlus.Scripts.Util;
using PlacementPlus.Commands;
using PlacementPlus.Components;
using PlayerEquipment;
using PugTilemap;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace PlacementPlus.Systems.Network
{
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial class ServerModCommandSystem : PugSimulationSystemBase
    {
        private NativeHashMap<int, ObjectID> _colorIndexLookup;
        private int _maxPaintIndex = -1;

        private EntityArchetype _responseArchetype;
        private EntityArchetype _achievementArchetype;

        protected override void OnCreate()
        {
            _responseArchetype =
                EntityManager.CreateArchetype(typeof(PlacementMessageRPC), typeof(SendRpcCommandRequest));
            _achievementArchetype = EntityManager.CreateArchetype(typeof(AchievementSystem.AchievementRpc),
                typeof(SendRpcCommandRequest));

            NeedDatabase();
            base.OnCreate();
        }

        protected override void OnUpdate()
        {
            InitColorIndexLookup();

            // Apply hammer size (M1, plan-deharmony): The EquipmentUpdateSystem prefix is not called 
            // on Burst-enabled servers, so this continuously running system handles it instead. 
            EquipmentSystem_Patch.ApplySledgeSize(ref CheckedStateRef, isServer: true);

            bool guestMode = WorldInfo.guestMode;
            bool creativeMode = WorldInfo.IsWorldModeEnabled(WorldMode.Creative);
            var ecb = CreateCommandBuffer();

            // Handle brush placement RPCs (M2, plan-deharmony) — This must occur before scheduling 
            // the lambda, ensuring that the lambda doesn't prematurely destroy BRUSH_PLACE entities 
            // within the exact same frame.
            Process(ref CheckedStateRef, ecb);

            var colorIndexLookupLocal = _colorIndexLookup;
            var databaseLocal = database;

            var paintToolLookup = GetComponentLookup<PaintToolCD>(true);
            var maxPaintIndexLocal = _maxPaintIndex;

            var networkTime = SystemAPI.GetSingleton<NetworkTime>();
            var currentTick = networkTime.ServerTick;

            var responseArchetypeLocal = _responseArchetype;

            foreach (var (rpc, req, rpcEntity)
                     in SystemAPI.Query<PlacementPlusRPC, ReceiveRpcCommandRequest>()
                         .WithEntityAccess())
            {
                if (rpc.commandType == ModCommandType.UNDEFINED) continue;

                if (!SystemAPI.HasComponent<PlacementPlusState>(rpc.player))
                {
                    PlacementPlusMod.Log.LogInfo(
                        $"Something is wrong! Player {rpc.player} doesn't have PlacementPlusState!");
                    ecb.DestroyEntity(rpcEntity);
                    continue;
                }

                var placementState = SystemAPI.GetComponent<PlacementPlusState>(rpc.player);
                var inventory = SystemAPI.GetBuffer<ContainedObjectsBuffer>(rpc.player);
                var clientInput = SystemAPI.GetComponent<ClientInput>(rpc.player);
                ref var item = ref inventory.ElementAt(clientInput.equippedSlotIndex);

                switch (rpc.commandType)
                {
                    case ModCommandType.CHANGE_SIZE:

                        ref var objectInfo =
                            ref PugDatabase.GetEntityObjectInfo(item.objectID, databaseLocal, item.variation);
                        if (objectInfo.objectID == ObjectID.None) continue;

                        Entity prefabEntity = objectInfo.prefabEntities[0];

                        // Sledgehammers: the hit collider is shared by every
                        // player holding that item, so the size is a single
                        // server-wide value — admins only (offline sessions
                        // report max privileges, so singleplayer always
                        // passes).
                        if (objectInfo.objectType == ObjectType.Sledge)
                        {
                            int sledgeAdmin = 0;
                            if (SystemAPI.HasComponent<ConnectionAdminLevelCD>(req.SourceConnection))
                                sledgeAdmin = SystemAPI.GetComponent<ConnectionAdminLevelCD>(req.SourceConnection)
                                    .adminPrivileges;

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
                            var placementSizeBuffer =
                                SystemAPI.GetBuffer<PlacementSizeByEquipmentTypeBuffer>(rpc.player);
                            var equipmentSlot = SystemAPI.GetComponent<EquipmentSlotCD>(rpc.player);

                            // If the size is adjusted during suppress(slotType=100), it gets saved to the ghost element (100), 
                            // causing a desync with the client preview (which tracks the normal slot element). 
                            // We restore the original slot using the item type to normalize the key (Observed on 08-25: 
                            // roofing behavior scaled properly by size, but the preview itself remained stuck at 9x9).
                            var sizeSlotType = equipmentSlot.slotType;
                            if (sizeSlotType == (EquipmentSlotType)100)
                            {
                                sizeSlotType = objectInfo.objectType switch
                                {
                                    ObjectType.RoofingTool => EquipmentSlotType.RoofingToolSlot,
                                    ObjectType.PaintTool => EquipmentSlotType.PaintToolSlot,
                                    ObjectType.Shovel => EquipmentSlotType.ShovelSlot,
                                    ObjectType.Hoe => EquipmentSlotType.HoeSlot,
                                    _ => EquipmentSlotType.PlaceObjectSlot
                                };
                            }

                            ref var element = ref placementSizeBuffer.GetElementForEquipment(sizeSlotType);

                            var size = (int)element.sizeVariationToPlace;

                            // StartOnSmallestSize says where the game starts
                            // the tool, not which way the size runs:
                            // GetTileSizeFromVariation is min(var, cap) + 1,
                            // always growing with var. Deriving the sign from
                            // it made "+" shrink the roofing gadget.
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
                            adminLevel = SystemAPI.GetComponent<ConnectionAdminLevelCD>(req.SourceConnection)
                                .adminPrivileges;

                        // Sledgehammer: the tool-mode key toggles square/arc.
                        // Shared value like the size — admins only.
                        ref var sledgeInfo =
                            ref PugDatabase.GetEntityObjectInfo(item.objectID, databaseLocal, item.variation);
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

                        // For shovel/hoe/roofing/paint brush OFF behaves exactly like
                        // SQUARE (their size is the tool's own), so their
                        // cycle skips it.
                        ref var orientInfo =
                            ref PugDatabase.GetEntityObjectInfo(item.objectID, databaseLocal, item.variation);
                        if (placementState.mode == BrushMode.NONE &&
                            (orientInfo.objectType == ObjectType.Shovel ||
                             orientInfo.objectType == ObjectType.Hoe ||
                             orientInfo.objectType == ObjectType.RoofingTool ||
                             orientInfo.objectType == ObjectType.PaintTool))
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
            }

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
            if (_colorIndexLookup.IsCreated) return;

            _colorIndexLookup = new NativeHashMap<int, ObjectID>(16, Allocator.Persistent);

            foreach (var (objectData, paintTool)
                     in SystemAPI.Query<ObjectDataCD, PaintToolCD>()
                         .WithAll<Prefab>())
            {
                if (paintTool.paintIndex == 0) continue;

                _colorIndexLookup.Add(paintTool.paintIndex, objectData.objectID);
                _maxPaintIndex = math.max(_maxPaintIndex, paintTool.paintIndex);
            }
            PlacementPlusMod.Log.LogInfo($"InitColorIndexLookup Done, {_colorIndexLookup.Count} brushes!");
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

        private void Process(
            ref SystemState state,
            EntityCommandBuffer ecb
        )
        {
            UpdatePlayerCount(ref state, ecb);

            // If the server prefix is active (DLL renaming, singleplayer, or hosting), the legacy path 
            // handles placing the grid — if this logic runs as well, it causes duplicate execution 
            // (overlapping sound effects and double consumption, observed on 08-22), so it remains dormant. 
            // Even if the BRUSH_PLACE entity is not consumed below, the lambda's default destruction logic will clean it up.
            if (EquipmentSystem_Patch.serverPrefixAlive) return;

            var tileAccessor = CreateTileAccessor();
            var ppStateLookup = state.GetComponentLookup<PlacementPlusState>();
            var placementLookup = state.GetComponentLookup<PlacementCD>();

            var aspectLookup = new EquipmentUpdateAspect.Lookup(ref state);

            var ppLookups = new PlacementPlusLookups();
            ppLookups.Init(ref state);

            if (!Helpers.TryBuildSharedData(
                    ref state,
                    ecb,
                    out var sharedData,
                    tileAccessor,
                    _achievementArchetype
                )) return;

            var lookupData = Helpers.BuildLookupData(ref state);

            // Roofing and paint logic internally call GetLookups(isServer) — on Burst-enabled 
            // servers, the prefix that normally populates this is never called, so we Init directly 
            // here (harmless since it is a reconcile-style recall).
            EquipmentSystem_Patch.plusLookupsServer.Init(ref state);

            foreach (var (rpc, rpcEntity) in SystemAPI.Query<BrushPlaceRPC>()
                         .WithAll<ReceiveRpcCommandRequest>()
                         .WithEntityAccess())
            {
                ecb.DestroyEntity(rpcEntity);

                Entity player = rpc.player;
                if (player == Entity.Null) continue;
                if (!ppStateLookup.HasComponent(player)) continue;
                if (!placementLookup.HasComponent(player)) continue;

                // Server-side duplicate defense (Double-layered with the client guard): Re-execution leaves 
                // only side effects, amplifying prediction desyncs (Measured and observed on 08-25: 2 to 4 times per anchor). 
                // The window must be shorter than the tool's actual cooldown — because it must not swallow 
                // legitimate rapid clicking that respects the cooldown (Roofing: 0.4s, Paint: 0.25s, Shovel: 0.4s), 
                // windows are configured dynamically by type.
                float dedupWindow = rpc.kind switch
                {
                    BrushKind.Roof => 0.3f,
                    BrushKind.Tool => 0.4f,
                    _ => 0.5f
                };
                bool dup = math.all(rpc.anchor == _lastExecAnchor) &&
                           rpc.kind == _lastExecKind &&
                           UnityEngine.Time.realtimeSinceStartup - _lastExecReal < dedupWindow;
                if (dup) continue;
                _lastExecAnchor = rpc.anchor;
                _lastExecKind = rpc.kind;
                _lastExecReal = UnityEngine.Time.realtimeSinceStartup;

                PlacementPlusState ppState = ppLookups.stateLookup[player];
                EquipmentUpdateAspect aspect = aspectLookup[player];

                RunBrushPlace(
                    aspect,
                    ppState,
                    sharedData,
                    lookupData,
                    ppLookups,
                    rpc.anchor,
                    rpc.kind
                );
            }
        }

        // Called "BEFORE" scheduling the lambda inside the ServerModCommandSystem.OnUpdate body 
        // — This ensures that the lambda doesn't prematurely destroy BRUSH_PLACE entities within 
        // the exact same frame.
        // Connection tracking — Player count from the immediately preceding tick (See Process).
        private int _lastPlayerCount = -1;

        private void UpdatePlayerCount(ref SystemState state, EntityCommandBuffer ecb)
        {
            // One-time hammer setting synchronization upon connection — If a new client doesn't know 
            // the server's values (step/shape), it will draw the preview using its own local cfg, 
            // resulting in a "9x9 preview but 3x3 strike" desync (Observed on 08-25; it only synced properly 
            // after pressing num+/-). Broadcast to everyone when a new player is detected — SLEDGE_SYNC 
            // is silent, so it won't cause network/chat spam.
            // Placed before the serverPrefixAlive guard: This is also required for DLL-renamed servers and hosts.

            var playerCountQuery = state.GetEntityQuery(
                ComponentType.ReadOnly<PlacementPlusState>(),
                ComponentType.ReadOnly<PlayerGhost>());

            int playerCount = playerCountQuery.CalculateEntityCount();

            if (playerCount > _lastPlayerCount && _lastPlayerCount >= 0 &&
                PlacementPlusMod.sledgeSize != null)
            {
                int packed = PlacementPlusMod.sledgeSize.Value * 2 +
                             ((PlacementPlusMod.sledgeSquare?.Value ?? true) ? 1 : 0);
                Entity msg = ecb.CreateEntity();
                ecb.AddComponent(msg, new PlacementMessageRPC
                {
                    messageType = ModMessageType.SLEDGE_SYNC_MESSAGE,
                    messageData = packed
                });
                ecb.AddComponent(msg, new SendRpcCommandRequest { TargetConnection = Entity.Null });
            }

            _lastPlayerCount = playerCount;
        }

        // Disabling vanilla server placement: EquipmentUpdateSystem's dispatch branches based on slotType, 
        // and if the value is 100 (out of bounds), no slots are processed — we maintain this same mechanism 
        // on the server side (the exact value PlaceItemGrid leaves at 100 upon finishing) that the legacy 
        // mod used in dll-off mode. Through testing, we confirmed slotType is not a [GhostField], meaning it 
        // is not replicated down to the client. However, because SelectedEquipmentChangeSystem reassigns 
        // slotType "every single tick" (not just when it changes — a correction from Round 6 observations), 
        // we must overwrite it every tick as well.
        // If not disabled, vanilla will continuously place a 1x1 at its own anchor (Round 2 observation: 
        // a conveyor 5x5 becomes 25+1, firing a rapid succession of 1x1 placements immediately after install).
        // (We previously noted a "cannot place on wall sides" constraint here, but it was disproven through 
        // testing after lookupData was completed — wall torch placement works fine, 08-25 round.)
        // Called by VanillaPlacementSuppressSystem — the exact "slot" (position) between the every-tick 
        // reassignment (Before group) and the dispatch (Update group) is absolutely vital.
        // Used for server-side double-execution defense (See Process).
        private int3 _lastExecAnchor;
        private BrushKind _lastExecKind = BrushKind.None;
        private float _lastExecReal = -10f;

        private static void RunBrushPlace(
            EquipmentUpdateAspect aspect,
            PlacementPlusState ppState,
            EquipmentUpdateSharedData sharedData,
            LookupEquipmentUpdateData lookupData,
            PlacementPlusLookups ppLookups,
            int3 anchor,
            BrushKind kind)
        {
            // The server's placementCD holds the value calculated by the vanilla 1x1 logic — 
            // we swap the anchor with the grid anchor sent by the client (Round 1 observation: if not swapped, 
            // only half is placed in the wrong spot). We also force canPlaceObject to true since vanilla 
            // might have disabled it based on its own anchor — per-cell safety is authoritatively handled by 
            // PlaceAt/ReplaceAt (placement), paintable validation (paint), and IsWallTile validation (roofing).
            ref PlacementCD placementRef = ref aspect.placementCD.ValueRW;
            placementRef.bestPositionToPlaceAt = anchor;
            placementRef.canPlaceObject = true;
            placementRef.canPlaceOnSideOfWall = false;

            switch (kind)
            {
                case BrushKind.Roof:

                    // Normalizing the slot during execution — the same mechanism that PlaceItemGrid performs on its own. 
                    // The size calculation (GetTileSizeFromVariation) looks up buffer elements using the slotType, 
                    // so if it runs while left at 100 due to suppression, it reads a ghost element (100) and desyncs the size 
                    // (Observed on 08-25: the execution logic and the preview tracked entirely different elements). 
                    // Reverts back to 100 once finished — suppression will overwrite it again before the next tick's dispatch anyway.
                    aspect.equipmentSlotCD.ValueRW.slotType = EquipmentSlotType.RoofingToolSlot;
                    RoofingToolSlot_Patch.Run(aspect, sharedData, lookupData, forceRun: true);
                    aspect.equipmentSlotCD.ValueRW.slotType = (EquipmentSlotType)100;
                    break;
                case BrushKind.Tool:

                    var held = aspect.equippedObjectCD.ValueRO;
                    var heldData = held.containedObject.objectData;
                    ref var heldInfo = ref PugDatabase.GetEntityObjectInfo(
                        heldData.objectID, sharedData.databaseBank.databaseBankBlob, heldData.variation);

                    var slotType = heldInfo.objectType switch
                    {
                        ObjectType.Hoe => EquipmentSlotType.HoeSlot,
                        ObjectType.Shovel => EquipmentSlotType.ShovelSlot,
                        ObjectType.PaintTool => EquipmentSlotType.PaintToolSlot,
                        _ => EquipmentSlotType.PlaceObjectSlot
                    };


                    // Normalizing the slot during execution — because the size calculation looks up buffer 
                    // elements using the slotType (same mechanism as roofing/paint, see the ghost element trap).
                    aspect.equipmentSlotCD.ValueRW.slotType = slotType;

                    // Positions are accurate even if the server calculates them on its own (square mode remains accurate even now). 
                    // Since "shape" is the only thing we are adding, we filter and pass the list tailored to the brush layout.
                    var digList = new NativeList<PlacementHandler.EntityAndInfoFromPlacement>(Allocator.Temp);
                    MyPlacementHandler.UpdatePlaceablePosition(
                        held.equipmentPrefab, ref digList, aspect, sharedData, lookupData, ppState);

                    switch (slotType)
                    {
                        case EquipmentSlotType.HoeSlot:
                            VanillaTools.HoeDig(ref digList, aspect, sharedData, lookupData);
                            break;
                        case EquipmentSlotType.ShovelSlot:
                            VanillaTools.ShovelDig(ref digList, aspect, sharedData, lookupData);
                            break;
                        case EquipmentSlotType.PaintToolSlot:
                            VanillaTools.PaintPlace(ref digList, aspect, sharedData, lookupData);
                            break;
                    }

                    digList.Dispose();
                    aspect.equipmentSlotCD.ValueRW.slotType = (EquipmentSlotType)100;
                    break;

                default:
                    ObjectPlacementLogic.PlaceItemGrid(aspect, sharedData, lookupData, ppLookups, ppState,
                        ignorePlacementCooldown: true);
                    break;
            }
        }
    }
}