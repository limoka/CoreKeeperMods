using Inventory;
using System.Collections.Generic;
using Mods.PlacementPlus.Scripts.Util;
using PlacementPlus.Components;
using PlayerEquipment;
using PlayerState;
using Pug.UnityExtensions;
using Pug.Properties;
using PugTilemap;
using PugTilemap.Quads;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Entity = Unity.Entities.Entity;

namespace PlacementPlus
{
    internal static class ObjectPlacementLogic
    {
        // Bridging the "command buffer delay window" for object spawning: Spawning goes 
        // through the ecb, so it isn't reflected in the world until the next tick. 
        // If overlapping grids see that same cell as empty during that gap and double-spawn, 
        // the game's post-validation (DestroyEntityIfPlacementNotValidCD) destroys 
        // one of them, causing item drops to leak (Confirmed on 08-25: drops during hold-drag 
        // overlap zones). We track cells spawned within the last 1 second to skip the second spawn.
        private static readonly Dictionary<int2, float> s_recentObjectSpawns = new Dictionary<int2, float>();

        private static bool RecentlySpawnedAt(int2 pos)
        {
            float now = UnityEngine.Time.realtimeSinceStartup;
            if (s_recentObjectSpawns.TryGetValue(pos, out float t) && now - t < 1f)
                return true;
            if (s_recentObjectSpawns.Count > 256) s_recentObjectSpawns.Clear();
            s_recentObjectSpawns[pos] = now;
            return false;
        }

        internal static bool IsItemValid(ref PugDatabase.EntityObjectInfo info, ref ObjectPropertiesCD properties)
        {
            if (info.objectType != ObjectType.PlaceablePrefab) return false;
            if (info.tileType != TileType.floor &&
                info.tileType != TileType.wall &&
                info.tileType != TileType.bridge &&
                info.tileType != TileType.ground &&
                info.tileType != TileType.groundSlime &&
                info.tileType != TileType.chrysalis &&
                info.tileType != TileType.litFloor &&
                info.tileType != TileType.rail &&
                info.tileType != TileType.rug &&
                info.tileType != TileType.fence &&
                info.tileType != TileType.none) return false;

            if (info.prefabTileSize.x != 1 || info.prefabTileSize.y != 1) return false;
            if (!PlacementPlusMod.allowWallVariants.Value &&
                properties.Has(PropertyID.PlaceableObject.hasVariationsThatCanBePlacedOnWalls)) return false;
            
            if (!PlacementPlusMod.ignoreBuiltinExclude.Value &&
                PlacementPlusMod.defaultExclude.Contains(info.objectID)) return false;
            if (PlacementPlusMod.userExclude.Contains(info.objectID)) return false;

            return true;
        }

        // God mode is the game's "free build" switch: no item cost, no
        // durability, and (matching vanilla destruction) no drops. A creative
        // WORLD without god mode pays normally — user decision 2026-08-21;
        // an earlier attempt wrongly made every creative world free.
        private static bool IsFreePlacement(
            in EquipmentUpdateAspect equipmentAspect,
            EquipmentUpdateSharedData sharedData,
            LookupEquipmentUpdateData lookupData)
        {
            return lookupData.godModeLookup.IsComponentEnabled(equipmentAspect.entity);
        }

        public static bool PlaceItemGrid(
            in EquipmentUpdateAspect equipmentAspect,
            EquipmentUpdateSharedData sharedData,
            LookupEquipmentUpdateData lookupData,
            PlacementPlusLookups ppLookups,
            PlacementPlusState state,
            // M2 (plan-deharmony): The BRUSH_PLACE RPC path evaluates to true — On Burst-enabled 
            // servers, vanilla code just placed 1 block and started timeSincePlaced 
            // (the same field as rows 67 & 74 of the game's PlaceObjectSlot.cs). This check 
            // causes the RPC execution to early-return (Observed: a 9x9 brush leaves only a 1x1). 
            // Even if bypassed, the occupancy/same-tile checks and "only consume actual placed cells" 
            // accounting prevent double-placement and double-consumption.
            bool ignorePlacementCooldown = false
        )
        {
            ref PlacementCD placement = ref equipmentAspect.placementCD.ValueRW;
            if (placement.canPlaceOnSideOfWall) return false;
            if (!placement.canPlaceObject) return false;

            ObjectDataCD objectData = equipmentAspect.equippedObjectCD.ValueRO.containedObject.objectData;
            ref PugDatabase.EntityObjectInfo entityObjectInfo = ref PugDatabase.GetEntityObjectInfo(objectData.objectID,
                sharedData.databaseBank.databaseBankBlob, objectData.variation);

            if (!ignorePlacementCooldown &&
                placement.timeSincePlaced.isRunning &&
                placement.timeSincePlaced.GetElapsedSeconds(sharedData.currentTick, sharedData.tickRate) < 1f &&
                math.all(placement.bestPositionToPlaceAt == placement.positionLastPlacedAt))
            {
                return false;
            }

            placement.timeSincePlaced.Start(sharedData.currentTick);
            placement.positionLastPlacedAt = placement.bestPositionToPlaceAt;

            float cooldown = (lookupData.godModeLookup.IsComponentEnabled(equipmentAspect.entity) ? 0.15f : 0.25f);
            EquipmentSlot.StartCooldownForItem(equipmentAspect, sharedData, lookupData, cooldown);

            BrushRect extents = state.GetExtents();
            var center = placement.bestPositionToPlaceAt.ToInt2();
            var consumeAmount = 0;

            // Prediction spawn authorization logic (See the spawn branch comments in PlaceAt):
            // Singleplayer / Host = serverPrefixAlive (the server prefix is alive in the same process). 
            // Dedicated servers use server-authoritative spawning regardless of the tile count — 
            // We previously allowed prediction as an exception for 1x1 (singleCell) objects, but 
            // even 1x1 objects like conveyors incurred ghost matching and rollback costs during 
            // prediction spawns. This caused continuous placement to become laggy and delay 
            // placement by 6 cells (Observed on 08-26 — multi-tile objects are server-exclusive, 
            // so they actually felt smoother). The trade-off is a half-beat delay, but this only 
            // affects objects; tile (block) placement speed remains unaffected.
            bool allowPredictedSpawn = sharedData.isServer ||
                                       EquipmentSystem_Patch.serverPrefixAlive;

            NativeHashMap<int3, bool> tilesChecked = new NativeHashMap<int3, bool>(32, Allocator.Temp);
            equipmentAspect.equipmentSlotCD.ValueRW.slotType = EquipmentSlotType.PlaceObjectSlot;

            float3 playerPosition = lookupData.localTransformLookup.GetRefRO(equipmentAspect.entity).ValueRO.Position;
            
            bool usedShovel = false;
            bool usedPickaxe = false;

            foreach (int3 pos in extents.WithPos(center))
            {
                PlaceAt(
                    equipmentAspect,
                    sharedData,
                    lookupData,
                    ppLookups,
                    state,
                    tilesChecked,
                    ref entityObjectInfo,
                    ref placement,
                    ref consumeAmount,
                    pos,
                    playerPosition,
                    ref usedShovel,
                    ref usedPickaxe,
                    allowPredictedSpawn,
                    extents.width == 0 && extents.height == 0
                );
            }

            equipmentAspect.equipmentSlotCD.ValueRW.slotType = (EquipmentSlotType)100;

            tilesChecked.Dispose();

            bool freePlacement = IsFreePlacement(in equipmentAspect, sharedData, lookupData);

            var inventoryChangeBuffers = lookupData.inventoryUpdateBuffer[sharedData.inventoryUpdateBufferEntity];
            if (!freePlacement)
            {
                inventoryChangeBuffers.Add(new InventoryChangeBuffer
                {
                    inventoryChangeData = Create.ConsumeEntityAt(
                        equipmentAspect.entity,
                        equipmentAspect.equippedObjectCD.ValueRO.equippedSlotIndex,
                        consumeAmount,
                        true,
                        lookupData.godModeLookup.IsComponentEnabled(equipmentAspect.entity),
                        center.ToFloat3(),
                        placement.currentPrefabVariation)
                });
            }

            HelperLogic.GetBestToolsSlots(
                equipmentAspect,
                sharedData,
                lookupData,
                ppLookups,
                out int shovelSlot,
                out int pickaxeSlot,
                out ObjectDataCD shovel,
                out ObjectDataCD pickaxe
            );

            if (usedShovel && !freePlacement)
            {
                HelperLogic.ConsumeEquipmentInSlot(
                    equipmentAspect,
                    sharedData,
                    inventoryChangeBuffers,
                    shovelSlot,
                    shovel,
                    center.ToFloat3());
            }

            if (usedPickaxe && !freePlacement)
            {
                HelperLogic.ConsumeEquipmentInSlot(
                    equipmentAspect,
                    sharedData,
                    inventoryChangeBuffers,
                    pickaxeSlot,
                    pickaxe,
                    center.ToFloat3());
            }

            int width = extents.width + 1;
            int height = extents.height + 1;


            var dealDamage = new DealDamageToEntityBuffer
            {
                attackType = DealDamageToEntityBuffer.AttackType.CritterDamage,
                hitPosition = placement.bestPositionToPlaceAt,
                optionalFromPosition = placement.bestPositionToPlaceAt,
                critterDamageSize = new float3(width, 1f, height),
                critterDamageCanDamageFlying = false,
                critterDamageKillEvenIfSquashBugsIsOff = true
            };
            equipmentAspect.dealDamageToEntityBuffer.Add(dealDamage);

            return true;
        }

        public static void PlaceAt(
            in EquipmentUpdateAspect equipmentAspect,
            EquipmentUpdateSharedData sharedData,
            LookupEquipmentUpdateData lookupData, 
            PlacementPlusLookups ppLookups,
            PlacementPlusState state,
            NativeHashMap<int3, bool> tilesChecked,
            ref PugDatabase.EntityObjectInfo entityObjectInfo,
            ref PlacementCD placement,
            ref int consumeAmount,
            int3 position,
            float3 playerPosition,
            ref bool usedShovel,
            ref bool usedPickaxe,
            // Whether client prediction spawning is allowed for objects (non-tiles) — 
            // Calculated and passed down by PlaceItemGrid (See the comments for the spawn branch below).
            bool allowPredictedSpawn = true,
            bool enforcePlacementTimer = true
        )
        {
            Entity equipmentPrefab = equipmentAspect.equippedObjectCD.ValueRO.equipmentPrefab;
            var posInt2 = position.ToInt2();

            var isTile = lookupData.tileLookup.HasComponent(equipmentPrefab);
            ObjectDataCD equippedObject = equipmentAspect.equippedObjectCD.ValueRO.containedObject.objectData;

            // Creative-only void brush (BlockMode.REMOVE). NOT the raw Clear
            // command: a cell with no tiles at all does not survive save/load —
            // the game's "empty" is a PIT tile, which RemoveTile(ground) adds
            // automatically (and its needed-tile cascade takes the wall down
            // with the ground). No cost, no drops, and never in non-creative
            // worlds even if the mode state leaks in.
            if (isTile && state.blockMode == BlockMode.REMOVE)
            {
                if (!sharedData.worldInfoCD.IsWorldModeEnabled(WorldMode.Creative)) return;

                var removeBuffer = lookupData.tileUpdateBufferLookup[sharedData.tileUpdateBufferEntity];
                var tilesAtCell = sharedData.tileAccessor.Get(posInt2, Allocator.Temp);
                for (int i = 0; i < tilesAtCell.Length; i++)
                {
                    var cellTile = tilesAtCell[i];
                    if (cellTile.tileType == TileType.pit) continue;
                    // Do not touch water — if deleted, it enters a state where it cannot handle saving, 
                    // causing it to look like a ghost block before reverting back into water upon reload 
                    // (Reported by Vavann25 on 08-26). If the terrain underneath the water is deleted, 
                    // the water will naturally flow on its own according to the game rules.
                    if (cellTile.tileType == TileType.water) continue;

                    EntityUtility.RemoveTile(
                        cellTile.tileset,
                        cellTile.tileType,
                        posInt2,
                        removeBuffer,
                        sharedData.tileAccessor);
                }
                tilesAtCell.Dispose();
                return;
            }

            // Survival Obsidian: The game rejects AddTile, but our code path finalizes the item 
            // consumption without knowing that, causing items to be lost (Observed on 08-26 — 
            // if a brush is used, this loss is multiplied by the number of cells). We skip this cell 
            // without placing or consuming anything. 
            // Why we block it here instead of using the exclusion list: Exclusions are turned off 
            // entirely by the IgnoreBuiltinExclude setting (which was the case in the user's environment), 
            // and adding it to the list completely breaks swapping/brushes while holding obsidian, 
            // even in creative mode (Observed by Vavann25). The REMOVE mode is unaffected since it 
            // already early-returns above.
            if (isTile &&
                !sharedData.worldInfoCD.IsWorldModeEnabled(WorldMode.Creative) &&
                (entityObjectInfo.objectID == ObjectID.WallObsidianBlock ||
                 entityObjectInfo.objectID == ObjectID.GroundObsidianBlock)) return;

            if (isTile && state.replaceTiles)
            {
                if (!IsFreePlacement(in equipmentAspect, sharedData, lookupData) &&
                    !PlayerController.CanConsumeEntityInSlot(
                        equipmentPrefab,
                        equippedObject,
                        consumeAmount + 1,
                        lookupData.cattleLookup)) return;

                // TOGGLE with a wall item replaces BOTH layers of the cell —
                // wall and the ground under it (user decision 2026-08-21; the
                // wall-first-else-ground original left the floor untouched).
                // ReplaceAt reads the layer from state.blockMode, and state is
                // a value copy, so forcing the mode per call is safe.
                // GROUND MUST GO FIRST: the game's own multi-layer writes queue
                // ground before the upper tile (SpawnTileOnDeath), and a ground
                // add applied after the wall add erases the freshly placed wall.
                if (state.blockMode == BlockMode.TOGGLE &&
                    entityObjectInfo.tileType == TileType.wall)
                {
                    var groundPass = state;
                    groundPass.blockMode = BlockMode.GROUND;
                    var wallPass = state;
                    wallPass.blockMode = BlockMode.WALL;

                    if (ReplaceAt(in equipmentAspect, sharedData, lookupData, ppLookups,
                            groundPass, ref entityObjectInfo, ref placement, false,
                            posInt2, playerPosition, ref usedShovel, ref usedPickaxe))
                    {
                        consumeAmount++;
                    }

                    if (ReplaceAt(in equipmentAspect, sharedData, lookupData, ppLookups,
                            wallPass, ref entityObjectInfo, ref placement, false,
                            posInt2, playerPosition, ref usedShovel, ref usedPickaxe))
                    {
                        consumeAmount++;
                    }

                    return;
                }

                if (ReplaceAt(
                        in equipmentAspect,
                        sharedData,
                        lookupData,
                        ppLookups,
                        state,
                        ref entityObjectInfo,
                        ref placement,
                        false,
                        posInt2,
                        playerPosition,
                        ref usedShovel,
                        ref usedPickaxe
                    ))
                {
                    consumeAmount++;
                }

                return;
            }

            // The game's "cross-placement prevention" timer (0.65s) is a rule designed for alternating 
            // tile placement one by one. If applied per cell within a single brush stroke, minority tiles 
            // break completely — placing a grid with mixed empty spaces and floors in simultaneous (TOGGLE) 
            // mode resulted in only walls generating while empty spaces remained unchanged (Observed on 08-27). 
            // Since multi-cell strokes are explicitly defined by the user's selected region, this rule is skipped.
            if (enforcePlacementTimer && !CanPlaceItem(
                    equipmentAspect,
                    sharedData,
                    lookupData,
                    state,
                    equipmentPrefab,
                    ref entityObjectInfo,
                    posInt2
                ))
            {
                return;
            }

            var result = MyPlacementHandler.CanPlaceObjectAtPosition(
                equipmentPrefab,
                position,
                1,
                1,
                tilesChecked,
                equipmentAspect,
                sharedData,
                lookupData
            );

            if (result == 0) return;

            if (!IsFreePlacement(in equipmentAspect, sharedData, lookupData) &&
                !PlayerController.CanConsumeEntityInSlot(
                    equipmentPrefab,
                    equippedObject,
                    consumeAmount + 1,
                    lookupData.cattleLookup)) return;

            equipmentAspect.placeObjectStateCD.ValueRW.positionToPlaceAt = placement.bestPositionToPlaceAt;
            equipmentAspect.playerStateCD.ValueRW.PushState(PlayerStateEnum.PlaceObject);

            float3 positionToPlaceAt = placement.bestPositionToPlaceAt;

            float3 direction = float3.zero;
            TileAccessor tileAccessor = sharedData.tileAccessor;

            if (isTile)
            {
                TileType targetType = GetTileTypeToPlace(
                    sharedData,
                    state,
                    posInt2,
                    ref entityObjectInfo);
                var dynamicBuffer = lookupData.tileUpdateBufferLookup[sharedData.tileUpdateBufferEntity];

                if (tileAccessor.HasType(posInt2, targetType)) return;
                if (targetType == TileType.wall && 
                    !tileAccessor.HasType(posInt2, TileType.ground) &&
                    !tileAccessor.HasType(posInt2, TileType.bridge)) return;
                if (targetType == TileType.ground && tileAccessor.HasType(posInt2, TileType.bridge)) return;

                var isInGodMode = sharedData.worldInfoCD.IsWorldModeEnabled(WorldMode.Creative);
                EntityUtility.AddTile(
                    entityObjectInfo.tileset,
                    targetType,
                    posInt2,
                    isInGodMode,
                    dynamicBuffer);

                consumeAmount++;
                placement.previouslyPlacedTileType = targetType;
            }
            else
            {
                float3 offsetPositionFloat = new float3(position.x, 0f, position.z);

                lookupData.objectPropertiesLookup.TryGetComponent(equipmentPrefab, out ObjectPropertiesCD objectPropertiesCD);
                objectPropertiesCD.TryGet(PropertyID.PlaceableObject.variationToPlace, out placement.currentPrefabVariation);

                if (lookupData.adaptiveEntityBufferLookup.TryGetBuffer(equipmentPrefab, out var dynamicBuffer2))
                {
                    if (dynamicBuffer2.IsCreated && dynamicBuffer2.Length > 0)
                    {
                        int2 int3 = offsetPositionFloat.RoundToInt2();
                        TileCD top = tileAccessor.GetTop(int3 + AdjacentDir.GetInt2(1));
                        TileCD top2 = tileAccessor.GetTop(int3 + AdjacentDir.GetInt2(16));
                        TileCD top3 = tileAccessor.GetTop(int3 + AdjacentDir.GetInt2(64));
                        TileCD top4 = tileAccessor.GetTop(int3 + AdjacentDir.GetInt2(4));
                        PlacementHandler.AdaptiveVariationCanBePlaced(placement.currentPrefabVariation, out placement.currentPrefabVariation, dynamicBuffer2,
                            top,
                            top2, top3, top4);
                    }
                }
                else if (lookupData.directionBasedOnVariationLookup.HasComponent(equipmentPrefab))
                {
                    placement.currentPrefabVariation = placement.rotationVariationToPlace;
                }
                else if (objectPropertiesCD.TryGet(PropertyID.Seed.rareSeedVariation, out int goldenPlantVariation))
                {
                    if (goldenPlantVariation > 0)
                    {
                        int chancePercent = lookupData.summarizedConditionsBufferLookup[equipmentAspect.entity][(int)ConditionID.ChanceToGainRarePlant].value;
                        if (chancePercent > 0)
                        {
                            Random rng = PugRandom.GetRng();
                            float chance = chancePercent / 100f;
                            if (rng.NextFloat() < chance)
                            {
                                placement.currentPrefabVariation = goldenPlantVariation;
                            }
                        }
                    }
                }

                if (PlacementHandler.ObjectCanBeRotated(
                        equipmentPrefab,
                        lookupData.directionBasedOnVariationLookup,
                        lookupData.objectPropertiesLookup,
                        lookupData.directionLookup) &&
                    PlacementHandler.ShouldRotatePhysics(equipmentPrefab, lookupData.directionLookup))
                {
                    direction = DirectionBasedOnVariationCD.GetDirectionFromVariation(placement.rotationVariationToPlace).ToFloat3();
                }
                else if (PlacementHandler.ObjectCanBeToggledToNewNonRotationOption(equipmentPrefab, lookupData.objectPropertiesLookup))
                    placement.currentPrefabVariation = placement.nonRotationVariationToPlace;

                // We do not spawn entities during "multi-cell" client prediction on dedicated servers — 
                // Objects predicted-spawned via grids (conveyors, etc.) continuously cycle through matching 
                // and rolling back against server ghosts, causing multi-second freezes (Observed via 
                // bisection on 08-25: blocks [tiles] are asymptomatic, but it triggers instantly the moment 
                // an object is placed — this explains the legacy mod's "conveyor 9x9 = 10-second freeze" issue). 
                // In this case, the server spawns authoritatively and replicates them down as ghosts (a half-beat delay). 
                // Singleplayer/Host (Prediction = Authoritative) and 1x1 on dedicated servers (the scale where vanilla 
                // also uses prediction spawns) maintain instant spawning — allowPredictedSpawn is calculated by 
                // PlaceItemGrid. Inventory consumption prediction (consumeAmount) is maintained either way.
                if (sharedData.isServer || allowPredictedSpawn)
                {
                    // Skip duplicate spawning due to grid overlaps in the ecb delay window (see comments at the top of the class).
                    // ⚠️ Server-"ONLY" — Because the registry log is static, in singleplayer/host environments, 
                    // the log from client-side prediction blocks the server execution running in the same process. 
                    // This caused objects to flicker, return to the hand, and require a second placement to successfully install 
                    // (Regression introduced in 1.2.0, reported by Uzume123 on 08-26). Client prediction duplicates are 
                    // handled by the game's native prediction system; the delay window that causes overlapping item drops 
                    // exists only on the server ecb side.

                    if (sharedData.isServer && RecentlySpawnedAt(posInt2)) return;

                    var ecb = sharedData.ecb;

                    Entity entity = EntityUtility.CreateEntity(
                        ecb,
                        entityObjectInfo.objectID,
                        1,
                        sharedData.databaseBank.databaseBankBlob,
                        placement.currentPrefabVariation);

                    ecb.SetComponent(entity, LocalTransform.FromPosition(position));
                    ComponentLookup<RandomCD> componentLookup = ppLookups.randomLookup;
                    if (componentLookup.HasComponent(equipmentAspect.entity))
                    {
                        componentLookup = ppLookups.randomLookup;
                        ref RandomCD valueRW = ref componentLookup.GetRefRW(equipmentAspect.entity).ValueRW;

                        ecb.SetComponent(entity, new RandomCD
                        {
                            Value = PugRandom.InheritRngFromEntity(ref valueRW.Value)
                        });
                    }
                    ComponentLookup<OwnerReferenceCD> ownerLookup = ppLookups.ownerLookup;
                    if (ownerLookup.HasComponent(equipmentPrefab))
                    {
                        ecb.SetComponent(entity, new OwnerReferenceCD
                        {
                            owner = equipmentAspect.entity
                        });
                    }
                    ComponentLookup<IsExplosiveCD> isExplosiveLookup = ppLookups.isExplosiveLookup;
                    if (isExplosiveLookup.TryGetComponent(equipmentPrefab, out var isExplosiveCD))
                    {
                        if (isExplosiveCD.bombInheritsFaction)
                        {
                            ComponentLookup<FactionCD> factionLookup = ppLookups.factionLookup;
                            if (factionLookup.HasComponent(equipmentPrefab))
                            {
                                EntityUtility.InheritFaction(ecb, equipmentAspect.entity, entity, ppLookups.factionLookup);
                            }
                        }
                        BufferLookup<SummarizedConditionsBuffer> summarizedConditionsBufferLookup = lookupData.summarizedConditionsBufferLookup;
                        if (summarizedConditionsBufferLookup.HasBuffer(equipmentPrefab))
                        {
                            EntityUtility.InheritConditionsForBomb(ecb, equipmentAspect.entity, entity, lookupData.summarizedConditionsBufferLookup);
                        }
                    }

                    ecb.AddComponent<DestroyEntityIfPlacementNotValidCD>(entity);
                    if (math.any(direction != 0f))
                    {
                        ecb.SetComponent(entity, new DirectionCD
                        {
                            direction = direction
                        });
                    }

                    // The count must be kept in lockstep with the spawn: If a client skips a spawn 
                    // but still increments the count, it will count overlapping cells (cells where the 
                    // server ghost has not replicated down yet) as "placed". This desyncs from the server's 
                    // actual consumption, and the discrepancy is returned as a dropped item (Observed on 08-25: 
                    // item drops leaking during hold-drag overlap zones). On skip, consumption defers to server finalization.
                    consumeAmount++;
                }
            }

            DynamicBuffer<GhostEffectEventBuffer> ghostEffectEventBuffer = equipmentAspect.ghostEffectEventBuffer;
            ref GhostEffectEventBufferPointerCD bufferPointer = ref equipmentAspect.ghostEffectEventBufferPointerCD.ValueRW;

            var newEvent = new GhostEffectEventBuffer
            {
                Tick = sharedData.currentTick,
                value = new EffectEventCD
                {
                    effectID = EffectID.PlaceObject,
                    position1 = positionToPlaceAt,
                    value1 = (int)entityObjectInfo.objectID,
                    vector1 = direction
                }
            };

            if (entityObjectInfo.tileType != TileType.none)
                newEvent.value.effectID = EffectID.PlaceTile;

            ghostEffectEventBuffer.AddToRingBuffer(ref bufferPointer, newEvent);
        }

        public static bool ReplaceAt(
            in EquipmentUpdateAspect equipmentAspect,
            EquipmentUpdateSharedData sharedData,
            LookupEquipmentUpdateData lookupData,
            PlacementPlusLookups ppLookups,
            PlacementPlusState state,
            ref PugDatabase.EntityObjectInfo entityObjectInfo,
            ref PlacementCD placement,
            bool doConsume,
            int2 position,
            float3 playerPosition,
            ref bool usedShovel,
            ref bool usedPickaxe
        )
        {
            int pickaxeDamage = HelperLogic.GetBestToolsSlots(
                equipmentAspect,
                sharedData,
                lookupData,
                ppLookups,
                out int shovelSlot,
                out int pickaxeSlot,
                out ObjectDataCD shovel,
                out ObjectDataCD pickaxe
            );
            
            lookupData.summarizedConditionEffectsBufferLookup.TryGetBuffer(equipmentAspect.entity, out var conditionsBuffer);

            var baseDamage = conditionsBuffer[(int)ConditionEffect.Mining].value;
            var miningMult = conditionsBuffer[(int)ConditionEffect.MiningPercentage].value;

            var finalMiningDamage = (baseDamage + 20 + pickaxeDamage) * (1f + miningMult / 100f);

            TileAccessor tileAccessor = sharedData.tileAccessor;

            int itemTileset = entityObjectInfo.tileset;
            TileType itemTileType = entityObjectInfo.tileType;

            TileCD tile = new TileCD();
            bool foundTile = false;

            if (itemTileType == TileType.wall)
            {
                var groundTileInfo = PugDatabase.TryGetTileItemInfo(TileType.ground, (Tileset)itemTileset, sharedData.tileWithTilesetToObjectDataMapCD);
                
                switch (state.blockMode)
                {
                    case BlockMode.TOGGLE:
                        foundTile = tileAccessor.GetType(position, TileType.wall, out tile);

                        if (!foundTile && groundTileInfo.objectID != ObjectID.None)
                        {
                            foundTile = tileAccessor.GetType(position, TileType.ground, out tile);
                            itemTileType = TileType.ground;
                        }
                        break;
                    case BlockMode.GROUND:
                        if (groundTileInfo.objectID != ObjectID.None)
                        {
                            foundTile = tileAccessor.GetType(position, TileType.ground, out tile);
                            itemTileType = TileType.ground;
                        }
                        break;
                    case BlockMode.WALL:
                        foundTile = tileAccessor.GetType(position, TileType.wall, out tile);
                        
                        break;
                }
            }
            else
            {
                foundTile = tileAccessor.GetType(position, itemTileType, out tile);
            }

            if (!foundTile) return false;
            if (tile.tileset == itemTileset) return false;

            var targetObjectData = PugDatabase.GetObjectData(tile.tileset, tile.tileType, sharedData.databaseBank.databaseBankBlob);
            var targetWallObjectData = PugDatabase.GetObjectData(tile.tileset, TileType.wall, sharedData.databaseBank.databaseBankBlob);

            if (targetObjectData.objectID == ObjectID.None) return false;

            // Obsidian blocking is a survival mode rule — replacement is allowed in creative mode 
            // (Reported by Vavann25 on 08-26: silicate worked fine, but obsidian alone failed).
            bool replaceCreative = sharedData.worldInfoCD.IsWorldModeEnabled(WorldMode.Creative);
            if (!replaceCreative &&
                (targetObjectData.objectID == ObjectID.WallObsidianBlock ||
                 targetObjectData.objectID == ObjectID.GroundObsidianBlock)) return false;

            ref var targetObjectInfo = ref PugDatabase.GetEntityObjectInfo(
                targetObjectData.objectID,
                sharedData.databaseBank.databaseBankBlob,
                targetObjectData.variation
            );
            var itemEntity = targetObjectInfo.prefabEntities[0];

            if (tile.tileType == TileType.wall)
            {
                // Creative mode also bypasses the pickaxe requirement (Observed on 08-26: when bypassing 
                // only the mining power check, floors worked but walls didn't — this line was blocking it first).
                if (!replaceCreative && pickaxeSlot == -1) return false;

                var reduction = ppLookups.damageReductionLookup[itemEntity];
                // Creative mode also bypasses the mining power check — because obsidian has a high 
                // reduction value, it gets blocked here again even if the hardcoding is lifted.
                if (!replaceCreative && finalMiningDamage - reduction.reduction <= 0)
                {
                    DynamicBuffer<GhostEffectEventBuffer> ghostEffectEventBuffer = equipmentAspect.ghostEffectEventBuffer;
                    ref GhostEffectEventBufferPointerCD bufferPointer = ref equipmentAspect.ghostEffectEventBufferPointerCD.ValueRW;
                    
                    var newEvent = new GhostEffectEventBuffer
                    {
                        Tick = sharedData.currentTick,
                        value = new EffectEventCD
                        {
                            entity = equipmentAspect.entity,
                            localOnlyEffect = 1,
                            effectID = EffectID.Emote,
                            value1 = (int)Emote.EmoteType.NeedHigherMiningSkill
                        }
                    };

                    ghostEffectEventBuffer.AddToRingBuffer(ref bufferPointer, newEvent);
                    return false;
                }

                // Durability marking is also omitted during the toolless creative mode bypass.
                if (pickaxeSlot != -1) usedPickaxe = true;
            }

            if (tile.tileType == TileType.ground)
            {
                if (!replaceCreative && shovelSlot == -1) return false;
                if (shovelSlot != -1) usedShovel = true;
            }

            var tileUpdateBuffer = lookupData.tileUpdateBufferLookup[sharedData.tileUpdateBufferEntity];

            EntityUtility.RemoveTile(
                tile.tileset,
                TileType.dugUpGround,
                position,
                tileUpdateBuffer,
                tileAccessor);

            EntityUtility.AddTile(
                itemTileset,
                itemTileType,
                position,
                sharedData.worldInfoCD.IsWorldModeEnabled(WorldMode.Creative),
                tileUpdateBuffer);

            var isInGodMode = IsFreePlacement(in equipmentAspect, sharedData, lookupData);

            // God-mode destruction gives no drops in vanilla, so god-mode
            // replacing shouldn't hand back the old tile either (user decision
            // 2026-08-21).
            if (!isInGodMode)
            {
                var giveObject = targetObjectData;

                if (targetWallObjectData.objectID != ObjectID.None && tile.tileType == TileType.ground)
                    giveObject = targetWallObjectData;

                EntityUtility.CreateAndDropItem(
                    giveObject.objectID,
                    giveObject.variation,
                    1,
                    playerPosition,
                    equipmentAspect.entity,
                    sharedData.databaseBank.databaseBankBlob,
                    sharedData.ecb
                );
            }

            if (doConsume && !isInGodMode)
            {
                var inventoryChangeBuffers = lookupData.inventoryUpdateBuffer[sharedData.inventoryUpdateBufferEntity];

                inventoryChangeBuffers.Add(new InventoryChangeBuffer
                {
                    inventoryChangeData = Create.ConsumeEntityAt(
                        equipmentAspect.entity,
                        equipmentAspect.equippedObjectCD.ValueRO.equippedSlotIndex,
                        1,
                        true,
                        lookupData.godModeLookup.IsComponentEnabled(equipmentAspect.entity),
                        playerPosition,
                        placement.currentPrefabVariation)
                });

                if (tile.tileType == TileType.ground && shovelSlot != -1)
                {
                    HelperLogic.ConsumeEquipmentInSlot(
                        equipmentAspect,
                        sharedData,
                        inventoryChangeBuffers,
                        shovelSlot,
                        shovel,
                        playerPosition);
                }

                if (tile.tileType == TileType.wall && pickaxeSlot != -1)
                {
                    HelperLogic.ConsumeEquipmentInSlot(
                        equipmentAspect,
                        sharedData,
                        inventoryChangeBuffers,
                        pickaxeSlot,
                        pickaxe,
                        playerPosition);
                }
            }

            return true;
        }


        public static TileType GetTileTypeToPlace(
            EquipmentUpdateSharedData sharedData,
            PlacementPlusState state,
            int2 pos,
            ref PugDatabase.EntityObjectInfo info)
        {
            if (info.tileType != TileType.wall)
                return info.tileType;
            var tileLookup = sharedData.tileAccessor;

            ObjectDataCD tileData;
            switch (state.blockMode)
            {
                case BlockMode.TOGGLE:
                    tileData = PugDatabase.TryGetTileItemInfo(TileType.ground, (Tileset)info.tileset, sharedData.tileWithTilesetToObjectDataMapCD);
                    if (!tileLookup.HasType(pos, TileType.ground) &&
                        !tileLookup.HasType(pos, TileType.bridge) &&
                        tileData.objectID != ObjectID.None)
                    {
                        return TileType.ground;
                    }

                    break;
                case BlockMode.GROUND:
                    tileData = PugDatabase.TryGetTileItemInfo(TileType.ground, (Tileset)info.tileset, sharedData.tileWithTilesetToObjectDataMapCD);
                    if (tileData.objectID != ObjectID.None)
                    {
                        return TileType.ground;
                    }

                    break;
                case BlockMode.WALL:
                    tileData = PugDatabase.TryGetTileItemInfo(TileType.wall, (Tileset)info.tileset, sharedData.tileWithTilesetToObjectDataMapCD);
                    if (tileData.objectID != ObjectID.None)
                    {
                        return TileType.wall;
                    }

                    break;
            }

            return info.tileType;
        }

        public static bool CanPlaceItem(
            in EquipmentUpdateAspect equipmentAspect,
            in EquipmentUpdateSharedData sharedData,
            in LookupEquipmentUpdateData lookupData,
            PlacementPlusState state,
            Entity placementPrefab,
            ref PugDatabase.EntityObjectInfo objectToPlaceInfo,
            int2 pos
        )
        {
            ref PlacementCD valueRW = ref equipmentAspect.placementCD.ValueRW;
            ComponentLookup<TileCD> tileLookup = lookupData.tileLookup;
            if (!tileLookup.HasComponent(placementPrefab))
            {
                valueRW.tilePlacementTimer.Stop(sharedData.currentTick);
                return true;
            }

            TileType targetTileToPlace = GetTileTypeToPlace(
                sharedData,
                state,
                pos,
                ref objectToPlaceInfo);
            bool flag = equipmentAspect.clientInput.ValueRO.IsButtonStateSet(CommandInputButtonStateNames.SecondInteract_Pressed) ||
                        (targetTileToPlace != TileType.wall && targetTileToPlace != TileType.ground) ||
                        (targetTileToPlace == TileType.wall && valueRW.previouslyPlacedTileType == TileType.wall) ||
                        (targetTileToPlace == TileType.ground && valueRW.previouslyPlacedTileType == TileType.ground) ||
                        !valueRW.tilePlacementTimer.isRunning ||
                        valueRW.tilePlacementTimer.IsTimerElapsed(sharedData.currentTick);
            if (flag)
            {
                valueRW.tilePlacementTimer.Start(sharedData.currentTick, 0.65f, sharedData.tickRate);
            }

            return flag;
        }

        public static bool IsPlacingWallAfterPreviouslyPlacedGround(in PlacementCD placementCD, ref PugDatabase.EntityObjectInfo objectToPlaceInfo)
        {
            return placementCD.previouslyPlacedTileType == TileType.ground && objectToPlaceInfo.tileType == TileType.wall;
        }
    }
}