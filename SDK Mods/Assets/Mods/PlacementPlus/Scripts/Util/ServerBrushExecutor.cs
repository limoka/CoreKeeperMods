using CommandMinion;
using Inventory;
using Mods.PlacementPlus.Scripts.Util;
using PlacementPlus.Commands;
using PlacementPlus.Components;
using PlacementPlus.Util;
using PlayerEquipment;
using PlayerState;
using Pug.Automation;
using Pug.Properties;
using PugTilemap;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.Physics;
using Unity.Transforms;

namespace PlacementPlus
{
    // M2 (plan-deharmony): Server-only execution engine that processes grid placement on Burst-enabled 
    // dedicated servers. In that environment, the server-side prefix of PlaceObjectSlot_Patch is never 
    // called (F-3 experiment). Therefore, when the client patch fires a BRUSH_PLACE RPC, this system 
    // compiles the exact same components that EquipmentUpdateSystem normally passes to its job 
    // (aspect, SharedData, LookupData) out of our own system state, and calls PlaceItemGrid directly.
    // In environments where the server patch is functional (DLL renaming, singleplayer, or hosting), 
    // the prefix executes the placement first, and this RPC gets caught by the timeSincePlaced check 
    // (1-second window for the same coordinate) at the very beginning of PlaceItemGrid and early-returns 
    // — ensuring zero double-placement or double-consumption.
    internal static class ServerBrushExecutor
    {
        // Called "BEFORE" scheduling the lambda inside the ServerModCommandSystem.OnUpdate body 
        // — This ensures that the lambda doesn't prematurely destroy BRUSH_PLACE entities within 
        // the exact same frame.
        // Connection tracking — Player count from the immediately preceding tick (See Process).
        private static int s_lastPlayerCount = -1;

        internal static void Process(ref SystemState state, EntityCommandBuffer ecb)
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
            if (playerCount > s_lastPlayerCount && s_lastPlayerCount >= 0 &&
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
            s_lastPlayerCount = playerCount;

            // If the server prefix is active (DLL renaming, singleplayer, or hosting), the legacy path 
            // handles placing the grid — if this logic runs as well, it causes duplicate execution 
            // (overlapping sound effects and double consumption, observed on 08-22), so it remains dormant. 
            // Even if the BRUSH_PLACE entity is not consumed below, the lambda's default destruction logic will clean it up.
            if (EquipmentSystem_Patch.serverPrefixAlive) return;

            var rpcQuery = state.GetEntityQuery(
                ComponentType.ReadOnly<BrushPlaceRPC>(),
                ComponentType.ReadOnly<ReceiveRpcCommandRequest>());
            if (rpcQuery.IsEmpty) return;

            var em = state.EntityManager;
            using var rpcEntities = rpcQuery.ToEntityArray(Allocator.Temp);

            foreach (Entity rpcEntity in rpcEntities)
            {
                if (!em.Exists(rpcEntity)) continue;
                var rpc = em.GetComponentData<BrushPlaceRPC>(rpcEntity);

                // This RPC is a type that the lambda is unaware of, so it is consumed right here.
                em.DestroyEntity(rpcEntity);

                Entity player = rpc.player;
                if (player == Entity.Null || !em.Exists(player)) continue;
                if (!em.HasComponent<PlacementPlusState>(player)) continue;
                if (!em.HasComponent<PlacementCD>(player)) continue;

                // Server-side duplicate defense (Double-layered with the client guard): Re-execution leaves 
                // only side effects, amplifying prediction desyncs (Measured and observed on 08-25: 2 to 4 times per anchor). 
                // The window must be shorter than the tool's actual cooldown — because it must not swallow 
                // legitimate rapid clicking that respects the cooldown (Roofing: 0.4s, Paint: 0.25s, Shovel: 0.4s), 
                // windows are configured dynamically by type.
                float dedupWindow = rpc.kind == Commands.BrushKind.Roof ? 0.3f :
                    rpc.kind == Commands.BrushKind.Paint ? 0.2f :
                    rpc.kind == Commands.BrushKind.Tool ? 0.4f : 0.5f;
                bool dup = Unity.Mathematics.math.all(rpc.anchor == s_lastExecAnchor) &&
                           rpc.kind == s_lastExecKind &&
                           UnityEngine.Time.realtimeSinceStartup - s_lastExecReal < dedupWindow;
                if (dup) continue;
                s_lastExecAnchor = rpc.anchor;
                s_lastExecKind = rpc.kind;
                s_lastExecReal = UnityEngine.Time.realtimeSinceStartup;

                ExecuteBrushPlace(ref state, ecb, player, rpc.anchor, rpc.kind);
            }
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
        private static Unity.Mathematics.int3 s_lastExecAnchor;
        private static int s_lastExecKind = -1;
        private static float s_lastExecReal = -10f;

        internal static void SuppressVanillaPlacement(ref SystemState state)
        {
            var playerQuery = state.GetEntityQuery(
                ComponentType.ReadOnly<PlacementPlusState>(),
                ComponentType.ReadWrite<EquipmentSlotCD>(),
                ComponentType.ReadOnly<EquippedObjectCD>());
            if (playerQuery.IsEmpty) return;

            var databaseQuery = state.GetEntityQuery(ComponentType.ReadOnly<PugDatabase.DatabaseBankCD>());
            if (!databaseQuery.TryGetSingleton<PugDatabase.DatabaseBankCD>(out var databaseBank)) return;

            // The ECB Singleton is attached to a "system entity," so it isn't captured by 
            // default queries — IncludeSystems is mandatory (Round 6.5 observation: omitting this 
            // caused it to quietly break at stage 3, entirely neutralizing the suppression logic. 
            // For game code, the source generator automatically injects this option). Additionally, 
            // compilation (ecb, sharedData) is only required for updating placementCD — this is isolated 
            // so that even if it fails, the core slotType suppression continues to run.
            bool canUpdatePlacement = false;
            EquipmentUpdateSharedData sharedData = default;
            LookupEquipmentUpdateData lookupData = default;

            var ecbQuery = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<BeginSimulationEntityCommandBufferSystem.Singleton>() },
                Options = EntityQueryOptions.IncludeSystems
            });
            if (ecbQuery.TryGetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>(out var ecbSingleton))
            {
                var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);
                if (TryBuildSharedData(ref state, ecb, out sharedData))
                {
                    lookupData = BuildLookupData(ref state);
                    canUpdatePlacement = true;
                }
            }

            var aspectLookup = new EquipmentUpdateAspect.Lookup(ref state);
            var ppLookups = new PlacementPlusLookups();
            ppLookups.Init(ref state);

            var em = state.EntityManager;
            using var players = playerQuery.ToEntityArray(Allocator.Temp);

            foreach (Entity player in players)
            {
                var held = em.GetComponentData<EquippedObjectCD>(player);
                var slot = em.GetComponentData<EquipmentSlotCD>(player);

                bool suppress = false;
                var objectData = held.containedObject.objectData;
                if (objectData.objectID != ObjectID.None && held.containedObject.auxDataIndex <= 0)
                {
                    ref var info = ref PugDatabase.GetEntityObjectInfo(objectData.objectID,
                        databaseBank.databaseBankBlob, objectData.variation);
                    if (info.objectID != ObjectID.None &&
                        held.equipmentPrefab != Entity.Null &&
                        em.HasComponent<ObjectPropertiesCD>(held.equipmentPrefab))
                    {
                        var properties = em.GetComponentData<ObjectPropertiesCD>(held.equipmentPrefab);
                        suppress = ObjectPlacementLogic.IsItemValid(ref info, ref properties);
                    }
                }

                // M3: If the modification is enabled for the roof apparatus or paintbrush, we also disable 
                // the server's vanilla handling — if the vanilla roofing behavior (toggle) overlaps with our RPC 
                // execution, the same cell gets re-toggled and messed up, while for paint, it duplicates a single vanilla cell. 
                // The evaluation logic here is a mirror image of the client prefix's "defer to vanilla" condition 
                // (the state has already been synced to the server via the existing RPC). The slotType is reliable here 
                // since the reassignment system has already restored it to its normal value for this tick.
                if (!suppress && ppLookups.stateLookup.HasComponent(player))
                {
                    var pps = ppLookups.stateLookup[player];
                    if (slot.slotType == EquipmentSlotType.RoofingToolSlot)
                    // "Always" suppress — If conditioned on mod states, the server placementCD calculation 
                    // authority would bounce back and forth (vanilla ↔ ours) whenever modes toggled, 
                    // causing the preview to get stuck on stale values (Observed on 08-25). The behavior 
                    // of the default mode is instead replicated by the executor via forceRun.
                        suppress = true;
                    else if (slot.slotType == EquipmentSlotType.PaintToolSlot)
                        suppress = pps.size != 0 && pps.mode != BrushMode.NONE;
                    else if (slot.slotType == EquipmentSlotType.ShovelSlot ||
                             slot.slotType == EquipmentSlotType.HoeSlot)
                        // Tools are "always" suppressed — If the server runs vanilla logic, ① it cannot read the shape, 
                        // causing it to dig in a true square (Observed on 08-27), ② its validation desyncs from the client, 
                        // causing the preview to flicker, and ③ tools that ignore size settings (wooden/copper shovels) 
                        // get stuck at 9x9 (Observed on 08-28). Handled identically to roofing.
                        suppress = true;
                }

                // Updating server placementCD every tick using our grid logic — placementCD handles 
                // ghost synchronization (Confirmed via PlacementCDGhostComponentSerializer), so if updates stop, 
                // the frozen server values will repeatedly roll back client predictions on every snapshot, 
                // leaving preview jitter and re-simulation lag behind. This calls the exact same logic that 
                // the server prefix handled back in the dll-off days (Server execution verified).
                // ⚠️ This must happen strictly *before* changing slotType to 100 — CanPlaceObjectAtPositionForSlotType 
                // internally branches based on slotType, and if it encounters 100, it throws a "Non implemented slot" 
                // error every single tick, breaking the server calculations (Observed 4,403 times on 08-25 — we missed this 
                // for blocks because the pinned icon masked it on screen, but for roofing, which lacks pins, the preview 
                // died entirely).
                if (suppress && canUpdatePlacement)
                {
                    var aspect = aspectLookup[player];
                    var ppState = ppLookups.stateLookup[player];
                    var nativeList = new NativeList<PlacementHandler.EntityAndInfoFromPlacement>(Allocator.Temp);
                    MyPlacementHandler.UpdatePlaceablePosition(
                        held.equipmentPrefab,
                        ref nativeList,
                        aspect,
                        sharedData,
                        lookupData,
                        ppState);
                    nativeList.Dispose();
                }

                // ⚠️ Suppression (slotType=100) doesn't just disable "placement"—it shuts down the entire 
                // dispatch pipeline for that slot. Consequently, vanilla interactions handled via the 'R' key 
                // (such as rotation and size changes) break on the server side, causing the client's predicted 
                // modifications to be rolled back by incoming server snapshots (Reported by SirSephiroth1 on 08-27: 
                // changing shovel sizes with 'R' immediately reverted, and conveyor/drill rotations reverted too 
                // — the clue was that chests were completely fine since they were on the exclusion list). 
                // While suppression is active, we must manually handle those operations on behalf of the game.
                if (suppress && canUpdatePlacement && em.HasComponent<ClientInput>(player))
                {
                    var clientInput = em.GetComponentData<ClientInput>(player);
                    if (clientInput.IsButtonStateSet(CommandInputButtonStateNames.Rotate_Pressed))
                    {
                        var rotateAspect = aspectLookup[player];
                        if (slot.slotType.OwnsItsSize())
                        {
                            // Tools: Replicates the exact calculation of the game's EquipmentSlot.ChangeSize 
                            // (cannot be called directly since it is protected, so we reproduce it here). 
                            // The slot key must be the actual value rather than 100 to avoid creating ghost elements.
                            var sizeData = held.containedObject.objectData;
                            ref var sizeInfo = ref PugDatabase.GetEntityObjectInfo(
                                sizeData.objectID, databaseBank.databaseBankBlob, sizeData.variation);
                            int cap = sizeInfo.prefabTileSize.x;
                            if (cap > 1)
                            {
                                ref var sizeElement = ref rotateAspect.placementSizeByEquipmentTypeBuffer
                                    .GetElementForEquipment(slot.slotType);
                                int current = Unity.Mathematics.math.min(sizeElement.sizeVariationToPlace, cap - 1);
                                sizeElement.sizeVariationToPlace = (byte)((current + 1) % cap);
                            }
                        }
                        else
                        {
                            // Placeables/Paint: Replicates the game's exact rotation logic (public static).
                            PlaceObjectSlot.Rotate(rotateAspect, sharedData, lookupData);
                        }
                    }
                }

                if (suppress && slot.slotType != (EquipmentSlotType)100)
                {
                    slot.slotType = (EquipmentSlotType)100;
                    em.SetComponentData(player, slot);
                }
                else if (!suppress && slot.slotType == (EquipmentSlotType)100)
                {
                    slot.slotType = EquipmentSlotType.PlaceObjectSlot;
                    em.SetComponentData(player, slot);
                }
            }
        }

        private static void ExecuteBrushPlace(ref SystemState state, EntityCommandBuffer ecb, Entity player, Unity.Mathematics.int3 anchor, int kind)
        {
            if (!TryBuildSharedData(ref state, ecb, out var sharedData)) return;
            var lookupData = BuildLookupData(ref state);
            RunBrushPlace(ref state, sharedData, lookupData, player, anchor, kind);
        }

        // ⚠️ Do not use typeof(T).Name — The safety validation check rejects System.Reflection references, 
        // causing the entire mod compilation to fail (Observed on 2026-08-23). 
        // The label must be provided as a string by the caller.

        private static Entity GetSingletonEntitySafe<T>(ref SystemState state, string label) where T : unmanaged, IBufferElementData
        {
            var query = state.GetEntityQuery(ComponentType.ReadWrite<T>());
            if (!query.TryGetSingletonEntity<T>(out Entity e))
            {
                PlacementPlusMod.Log.LogWarning($"Singleton entity not found: {label}");
                return Entity.Null;
            }
            return e;
        }

        internal static bool TryBuildSharedData(ref SystemState state, EntityCommandBuffer ecb, out EquipmentUpdateSharedData sharedData)
        {
            sharedData = default;
            var networkTimeQuery = state.GetEntityQuery(ComponentType.ReadOnly<NetworkTime>());
            if (!networkTimeQuery.TryGetSingleton<NetworkTime>(out var networkTime)) return false;

            sharedData = new EquipmentUpdateSharedData
            {
                currentTick = networkTime.ServerTick,
                databaseBank = state.GetEntityQuery(ComponentType.ReadOnly<PugDatabase.DatabaseBankCD>())
                    .GetSingleton<PugDatabase.DatabaseBankCD>(),
                worldInfoCD = state.GetEntityQuery(ComponentType.ReadOnly<WorldInfoCD>())
                    .GetSingleton<WorldInfoCD>(),
                tickRate = (uint)PlatformConfiguration.Instance.SessionConfiguration.SimulationTickRate,
                physicsWorld = state.GetEntityQuery(ComponentType.ReadOnly<PhysicsWorldSingleton>())
                    .GetSingleton<PhysicsWorldSingleton>().PhysicsWorld,
                physicsWorldHistory = state.GetEntityQuery(ComponentType.ReadOnly<PhysicsWorldHistorySingleton>())
                    .GetSingleton<PhysicsWorldHistorySingleton>(),
                // GetSingletonEntity throws an exception on failure — if PugMod's ISystem wrapper 
                // silently swallows the exception, the system will break every tick without any logs 
                // (Round 6: suspected as the primary cause for why suppression was neutralized without a sound).
                inventoryUpdateBufferEntity = GetSingletonEntitySafe<InventoryChangeBuffer>(ref state, "InventoryChangeBuffer"),
                tileUpdateBufferEntity = GetSingletonEntitySafe<TileUpdateBuffer>(ref state, "TileUpdateBuffer"),
                tileAccessor = GetTileAccessor(ref state),
                tileWithTilesetToObjectDataMapCD = state.GetEntityQuery(ComponentType.ReadOnly<TileWithTilesetToObjectDataMapCD>())
                    .GetSingleton<TileWithTilesetToObjectDataMapCD>(),
                colliderCacheCD = state.GetEntityQuery(ComponentType.ReadOnly<ColliderCacheCD>())
                    .GetSingleton<ColliderCacheCD>(),
                isServer = true,
                ecb = ecb,
                isFirstTimeFullyPredictingTick = networkTime.IsFirstTimeFullyPredictingTick,
                achievementArchetype = GetAchievementArchetype(ref state)
            };
            return true;
        }

        // Compilation now runs every tick (placementCD synchronization) — identical to the game, 
        // TileAccessor is only created once and then updated, and the archetype is also cached once 
        // (safe because dedicated servers use one world per process).
        private static TileAccessor s_tileAccessor;
        private static bool s_tileAccessorReady;
        private static EntityArchetype s_achievementArchetype;

        private static TileAccessor GetTileAccessor(ref SystemState state)
        {
            if (!s_tileAccessorReady)
            {
                s_tileAccessor = new TileAccessor(ref state);
                s_tileAccessorReady = true;
            }
            else
            {
                s_tileAccessor.Update(ref state);
            }
            return s_tileAccessor;
        }

        private static EntityArchetype GetAchievementArchetype(ref SystemState state)
        {
            if (!s_achievementArchetype.Valid)
                s_achievementArchetype = AchievementSystem.GetRpcArchetype(state.EntityManager);
            return s_achievementArchetype;
        }

        internal static LookupEquipmentUpdateData BuildLookupData(ref SystemState state)
        {
            return new LookupEquipmentUpdateData
            {
                secondaryUseLookup = state.GetComponentLookup<SecondaryUseCD>(),
                cooldownLookup = state.GetComponentLookup<CooldownCD>(),
                warmupLookup = state.GetComponentLookup<WarmupCD>(),
                consumeManaLookup = state.GetComponentLookup<ConsumesManaCD>(),
                levelLookup = state.GetComponentLookup<LevelCD>(),
                levelEntitiesLookup = state.GetBufferLookup<LevelEntitiesBuffer>(),
                parchementRecipeLookup = state.GetComponentLookup<RecipeCD>(),
                objectDataLookup = state.GetComponentLookup<ObjectDataCD>(),
                attackWithEquipmentLookup = state.GetComponentLookup<AttackWithEquipmentTag>(),
                inventoryUpdateBuffer = state.GetBufferLookup<InventoryChangeBuffer>(),
                cattleLookup = state.GetComponentLookup<CattleCD>(),
                petCandyLookup = state.GetComponentLookup<PetCandyCD>(),
                potionLookup = state.GetComponentLookup<PotionCD>(),
                localTransformLookup = state.GetComponentLookup<LocalTransform>(),
                petLookup = state.GetComponentLookup<PetCD>(),
                playAnimationStateLookup = state.GetComponentLookup<PlayAnimationStateCD>(),
                simulateLookup = state.GetComponentLookup<Simulate>(),
                waitingForEatableSlotConsumeResultLookup = state.GetComponentLookup<WaitingForEatableSlotConsumeResultCD>(),
                tileUpdateBufferLookup = state.GetBufferLookup<TileUpdateBuffer>(),
                tileLookup = state.GetComponentLookup<TileCD>(),
                objectPropertiesLookup = state.GetComponentLookup<ObjectPropertiesCD>(),
                adaptiveEntityBufferLookup = state.GetBufferLookup<AdaptiveEntityBuffer>(),
                directionBasedOnVariationLookup = state.GetComponentLookup<DirectionBasedOnVariationCD>(),
                directionLookup = state.GetComponentLookup<DirectionCD>(),
                sizeVariationLookup = state.GetComponentLookup<ResizableTileSizeCD>(),
                playerGhostLookup = state.GetComponentLookup<PlayerGhost>(),
                minionLookup = state.GetComponentLookup<MinionCD>(),
                indestructibleLookup = state.GetComponentLookup<IndestructibleCD>(),
                plantLookup = state.GetComponentLookup<PlantCD>(),
                critterLookup = state.GetComponentLookup<CritterCD>(),
                fireflyLookup = state.GetComponentLookup<FireflyCD>(),
                requiresDrillLookup = state.GetComponentLookup<RequiresDrillCD>(),
                surfacePriorityLookup = state.GetComponentLookup<SurfacePriorityCD>(),
                electricityLookup = state.GetComponentLookup<ElectricityCD>(),
                eventTerminalLookup = state.GetComponentLookup<EventTerminalCD>(),
                waterSourceLookup = state.GetComponentLookup<WaterSourceCD>(),
                paintToolLookup = state.GetComponentLookup<PaintToolCD>(),
                paintableObjectLookup = state.GetComponentLookup<PaintableObjectCD>(),
                growingLookup = state.GetComponentLookup<GrowingCD>(),
                healthLookup = state.GetComponentLookup<HealthCD>(),
                summarizedConditionsBufferLookup = state.GetBufferLookup<SummarizedConditionsBuffer>(),
                reduceDurabilityOfEquippedTagLookup = state.GetComponentLookup<ReduceDurabilityOfEquippedTriggerCD>(),
                summarizedConditionEffectsBufferLookup = state.GetBufferLookup<SummarizedConditionEffectsBuffer>(),
                entityDestroyedLookup = state.GetComponentLookup<EntityDestroyedCD>(),
                dontDropSelfLookup = state.GetComponentLookup<DontDropSelfCD>(),
                dontDropLootLookup = state.GetComponentLookup<DontDropLootCD>(),
                killedByPlayerLookup = state.GetComponentLookup<KilledByPlayerCD>(),
                destructibleLookup = state.GetComponentLookup<DestructibleObjectCD>(),
                canBeRemovedByWaterLookup = state.GetComponentLookup<CanBeRemovedByWaterCD>(),
                groundDecorationLookup = state.GetComponentLookup<GroundDecorationCD>(),
                diggableLookup = state.GetComponentLookup<DiggableCD>(),
                pseudoTileLookup = state.GetComponentLookup<PseudoTileCD>(),
                dontBlockDiggingLookup = state.GetComponentLookup<DontBlockDiggingCD>(),
                fullnessLookup = state.GetComponentLookup<FullnessCD>(),
                godModeLookup = state.GetComponentLookup<GodModeCD>(),
                containedObjectsBufferLookup = state.GetBufferLookup<ContainedObjectsBuffer>(),
                inventoryBufferLookup = state.GetBufferLookup<InventoryBuffer>(),
                anvilLookup = state.GetComponentLookup<AnvilCD>(),
                waypointLookup = state.GetComponentLookup<WayPointCD>(),
                craftingLookup = state.GetComponentLookup<CraftingCD>(),
                proximityTriggerLookup = state.GetComponentLookup<ProximityTriggerCD>(),
                commandMinionLookup = state.GetComponentLookup<CommandMinionWeaponCD>(),
                rootPlantLookup = state.GetComponentLookup<RootPlantCD>(),
                triggerSelectNewEnemyToAttackCommandLookup = state.GetComponentLookup<TriggerSelectEnemyToAttackForMinionCommandCD>(),
                triggerAnimationOnDeathLookup = state.GetComponentLookup<TriggerAnimationOnDeathCD>(),
                moveToPredictedByEntityDestroyedLookup = state.GetComponentLookup<MoveToPredictedByEntityDestroyedCD>(),
                hasExplodedLookup = state.GetComponentLookup<HasExplodedCD>()
            };
        }

        private static void RunBrushPlace(
            ref SystemState state,
            EquipmentUpdateSharedData sharedData,
            LookupEquipmentUpdateData lookupData,
            Entity player,
            Unity.Mathematics.int3 anchor,
            int kind)
        {
            var aspectLookup = new EquipmentUpdateAspect.Lookup(ref state);
            EquipmentUpdateAspect aspect = aspectLookup[player];

            // The server's placementCD holds the value calculated by the vanilla 1x1 logic — 
            // we swap the anchor with the grid anchor sent by the client (Round 1 observation: if not swapped, 
            // only half is placed in the wrong spot). We also force canPlaceObject to true since vanilla 
            // might have disabled it based on its own anchor — per-cell safety is authoritatively handled by 
            // PlaceAt/ReplaceAt (placement), paintable validation (paint), and IsWallTile validation (roofing).
            ref PlacementCD placementRef = ref aspect.placementCD.ValueRW;
            placementRef.bestPositionToPlaceAt = anchor;
            placementRef.canPlaceObject = true;
            placementRef.canPlaceOnSideOfWall = false;

            var ppLookups = new PlacementPlusLookups();
            ppLookups.Init(ref state);
            PlacementPlusState ppState = ppLookups.stateLookup[player];

            switch (kind)
            {
                case Commands.BrushKind.Roof:
                    // Roofing and paint logic internally call GetLookups(isServer) — on Burst-enabled 
                    // servers, the prefix that normally populates this is never called, so we Init directly 
                    // here (harmless since it is a reconcile-style recall).
                    EquipmentSystem_Patch.plusLookupsServer.Init(ref state);
                    // Normalizing the slot during execution — the same mechanism that PlaceItemGrid performs on its own. 
                    // The size calculation (GetTileSizeFromVariation) looks up buffer elements using the slotType, 
                    // so if it runs while left at 100 due to suppression, it reads a ghost element (100) and desyncs the size 
                    // (Observed on 08-25: the execution logic and the preview tracked entirely different elements). 
                    // Reverts back to 100 once finished — suppression will overwrite it again before the next tick's dispatch anyway.
                    aspect.equipmentSlotCD.ValueRW.slotType = EquipmentSlotType.RoofingToolSlot;
                    RoofingToolSlot_Patch.Run(aspect, sharedData, lookupData, forceRun: true);
                    aspect.equipmentSlotCD.ValueRW.slotType = (EquipmentSlotType)100;
                    break;
                case Commands.BrushKind.Tool:
                {
                    EquipmentSystem_Patch.plusLookupsServer.Init(ref state);
                    var held = state.EntityManager.GetComponentData<EquippedObjectCD>(player);
                    var heldData = held.containedObject.objectData;
                    ref var heldInfo = ref PugDatabase.GetEntityObjectInfo(
                        heldData.objectID, sharedData.databaseBank.databaseBankBlob, heldData.variation);
                    bool isHoe = heldInfo.objectType == ObjectType.Hoe;

                    // Normalizing the slot during execution — because the size calculation looks up buffer 
                    // elements using the slotType (same mechanism as roofing/paint, see the ghost element trap).
                    aspect.equipmentSlotCD.ValueRW.slotType =
                        isHoe ? EquipmentSlotType.HoeSlot : EquipmentSlotType.ShovelSlot;

                    // Positions are accurate even if the server calculates them on its own (square mode remains accurate even now). 
                    // Since "shape" is the only thing we are adding, we filter and pass the list tailored to the brush layout.
                    var digList = new NativeList<PlacementHandler.EntityAndInfoFromPlacement>(Allocator.Temp);
                    MyPlacementHandler.UpdatePlaceablePosition(
                        held.equipmentPrefab, ref digList, aspect, sharedData, lookupData, ppState);

                    if (isHoe) VanillaTools.HoeDig(ref digList, aspect, sharedData, lookupData);
                    else VanillaTools.ShovelDig(ref digList, aspect, sharedData, lookupData);

                    digList.Dispose();
                    aspect.equipmentSlotCD.ValueRW.slotType = (EquipmentSlotType)100;
                    break;
                }
                case Commands.BrushKind.Paint:
                    EquipmentSystem_Patch.plusLookupsServer.Init(ref state);
                    aspect.equipmentSlotCD.ValueRW.slotType = EquipmentSlotType.PaintToolSlot;
                    PaintToolSlot_Patch.PaintGrid(aspect, sharedData, lookupData, ppState,
                        ignoreGates: true);
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
