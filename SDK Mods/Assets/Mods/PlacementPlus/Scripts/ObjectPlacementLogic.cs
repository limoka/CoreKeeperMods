using Inventory;
using Mods.PlacementPlus.Scripts.Util;
using PlacementPlus.Access;
using PlacementPlus.Components;
using PlayerEquipment;
using PlayerState;
using PugProperties;
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
        public static bool UpdatePlaceObjectPlus(
            EquipmentUpdateAspect equipmentAspect,
            EquipmentUpdateSharedData equipmentShared,
            LookupEquipmentUpdateData lookupData, BufferLookup<GivesConditionsWhenEquippedBuffer> conditionsLookup,
            ComponentLookup<DamageReductionCD> damageReductionLookup,
            PlacementPlusState state,
            bool secondInteractHeld
        )
        {
            var containedObject = equipmentAspect.equippedObjectCD.ValueRO.containedObject;
            if (containedObject.auxDataIndex > 0) return false;

            ObjectDataCD objectData = containedObject.objectData;
            ref PugDatabase.EntityObjectInfo entityObjectInfo = ref PugDatabase.GetEntityObjectInfo(objectData.objectID,
                equipmentShared.databaseBank.databaseBankBlob, objectData.variation);

            if (!IsItemValid(ref entityObjectInfo)) return false;

            bool hasItemInMouse =
                lookupData.containedObjectsBufferLookup.TryGetBuffer(equipmentAspect.entity,
                    out DynamicBuffer<ContainedObjectsBuffer> dynamicBuffer) &&
                lookupData.craftingLookup.TryGetComponent(equipmentAspect.entity, out CraftingCD craftingCD) &&
                dynamicBuffer.Length > craftingCD.outputSlotIndex &&
                dynamicBuffer[craftingCD.outputSlotIndex].objectID > ObjectID.None;
            if (hasItemInMouse) return false;

            var nativeList = new NativeList<PlacementHandler.EntityAndInfoFromPlacement>(Allocator.Temp);
            MyPlacementHandler.UpdatePlaceablePosition(
                equipmentAspect.equippedObjectCD.ValueRO.equipmentPrefab,
                ref nativeList,
                equipmentAspect,
                equipmentShared,
                lookupData,
                state);
            nativeList.Dispose();

            equipmentAspect.equipmentSlotCD.ValueRW.slotType = (EquipmentSlotType)100;
            if (!secondInteractHeld) return false;

            return PlaceItemGrid(
                equipmentAspect,
                equipmentShared,
                lookupData,
                conditionsLookup,
                damageReductionLookup,
                state
            );
        }

        internal static bool IsItemValid(ref PugDatabase.EntityObjectInfo info)
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


            //TODO bursting
            if (PlacementPlusMod.defaultExclude.Contains(info.objectID)) return false;
            if (PlacementPlusMod.userExclude.Contains(info.objectID)) return false;

            return true;
        }

        public static bool PlaceItemGrid(
            in EquipmentUpdateAspect equipmentAspect,
            EquipmentUpdateSharedData sharedData,
            LookupEquipmentUpdateData lookupData, BufferLookup<GivesConditionsWhenEquippedBuffer> conditionsLookup,
            ComponentLookup<DamageReductionCD> damageReductionLookup,
            PlacementPlusState state
        )
        {
            ref PlacementCD placement = ref equipmentAspect.placementCD.ValueRW;
            if (placement.canPlaceOnSideOfWall) return false;
            if (!placement.canPlaceObject) return false;

            ObjectDataCD objectData = equipmentAspect.equippedObjectCD.ValueRO.containedObject.objectData;
            ref PugDatabase.EntityObjectInfo entityObjectInfo = ref PugDatabase.GetEntityObjectInfo(objectData.objectID,
                sharedData.databaseBank.databaseBankBlob, objectData.variation);

            if (placement.timeSincePlaced.isRunning &&
                placement.timeSincePlaced.GetElapsedSeconds(sharedData.currentTick, sharedData.tickRate) < 1f &&
                math.all(placement.bestPositionToPlaceAt == placement.positionLastPlacedAt))
            {
                PlacementPlusMod.Log.LogInfo("Aborting due to a second timer");
                return false;
            }

            placement.timeSincePlaced.Start(sharedData.currentTick);
            placement.positionLastPlacedAt = placement.bestPositionToPlaceAt;

            float cooldown = (lookupData.godModeLookup.IsComponentEnabled(equipmentAspect.entity) ? 0.15f : 0.25f);
            EquipmentSlot.StartCooldownForItem(equipmentAspect, sharedData, lookupData, cooldown);

            BrushRect extents = state.GetExtents();
            var center = placement.bestPositionToPlaceAt.ToInt2();
            var consumeAmount = 0;

            NativeHashMap<int3, bool> tilesChecked = new NativeHashMap<int3, bool>(32, Allocator.Temp);
            equipmentAspect.equipmentSlotCD.ValueRW.slotType = EquipmentSlotType.PlaceObjectSlot;

            bool usedShovel = false;
            bool usedPickaxe = false;

            foreach (int3 pos in extents.WithPos(center))
            {
                PlaceAt(
                    equipmentAspect,
                    sharedData,
                    lookupData,
                    conditionsLookup,
                    damageReductionLookup,
                    state,
                    tilesChecked,
                    ref entityObjectInfo,
                    ref placement,
                    ref consumeAmount,
                    pos,
                    ref usedShovel,
                    ref usedPickaxe
                );
            }

            equipmentAspect.equipmentSlotCD.ValueRW.slotType = (EquipmentSlotType)100;

            tilesChecked.Dispose();

            var inventoryChangeBuffers = lookupData.inventoryUpdateBuffer[sharedData.inventoryUpdateBufferEntity];
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

            HelperLogic.GetBestToolsSlots(
                equipmentAspect,
                sharedData,
                lookupData,
                conditionsLookup,
                out int shovelSlot,
                out int pickaxeSlot,
                out ObjectDataCD shovel,
                out ObjectDataCD pickaxe
            );

            if (usedShovel)
            {
                HelperLogic.ConsumeEquipmentInSlot(
                    equipmentAspect,
                    sharedData,
                    inventoryChangeBuffers,
                    shovelSlot,
                    shovel,
                    center.ToFloat3());
            }

            if (usedPickaxe)
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

            equipmentAspect.critterDamageFromPlacingCD.ValueRW = new CritterDamageFromPlacingCD
            {
                triggered = true,
                pos = placement.bestPositionToPlaceAt,
                size = new float3(width, 1f, height),
                canDamageFlyingCritter = false,
                killEvenIfSquashBugsIsOff = true
            };

            return true;
        }

        public static void PlaceAt(
            in EquipmentUpdateAspect equipmentAspect,
            EquipmentUpdateSharedData sharedData,
            LookupEquipmentUpdateData lookupData, BufferLookup<GivesConditionsWhenEquippedBuffer> conditionsLookup,
            ComponentLookup<DamageReductionCD> damageReductionLookup,
            PlacementPlusState state,
            NativeHashMap<int3, bool> tilesChecked,
            ref PugDatabase.EntityObjectInfo entityObjectInfo,
            ref PlacementCD placement,
            ref int consumeAmount,
            int3 position,
            ref bool usedShovel,
            ref bool usedPickaxe
        )
        {
            Entity equipmentPrefab = equipmentAspect.equippedObjectCD.ValueRO.equipmentPrefab;
            var posInt2 = position.ToInt2();

            var isTile = lookupData.tileLookup.HasComponent(equipmentPrefab);
            ObjectDataCD equippedObject = equipmentAspect.equippedObjectCD.ValueRO.containedObject.objectData;

            if (isTile && state.replaceTiles)
            {
                if (!PlayerController.CanConsumeEntityInSlot(
                        equipmentPrefab,
                        equippedObject,
                        consumeAmount + 1,
                        lookupData.cattleLookup)) return;

                if (ReplaceAt(
                        in equipmentAspect,
                        sharedData,
                        lookupData,
                        conditionsLookup,
                        damageReductionLookup,
                        ref entityObjectInfo,
                        ref placement,
                        false,
                        posInt2,
                        ref usedShovel,
                        ref usedPickaxe
                    ))
                {
                    consumeAmount++;
                }

                return;
            }

            if (!CanPlaceItem(
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

            var result = AccessExtensions.CanPlaceObjectAtPosition_PlacePublic(
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

            if (!PlayerController.CanConsumeEntityInSlot(
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
                if (targetType == TileType.wall && !tileAccessor.HasType(posInt2, TileType.ground)) return;

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
                objectPropertiesCD.TryGet(245919617 /*currentPrefabVariation*/, out placement.currentPrefabVariation);

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
                else if (objectPropertiesCD.TryGet(1273594437 /* golden plant stuff*/, out int goldenPlantVariation))
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

                var ecb = sharedData.ecb;

                Entity entity = EntityUtility.CreateEntity(
                    ecb,
                    entityObjectInfo.objectID,
                    1,
                    sharedData.databaseBank.databaseBankBlob,
                    placement.currentPrefabVariation);

                ecb.SetComponent(entity, LocalTransform.FromPosition(position));
                ecb.AddComponent(entity, new PlacedByEntityCD
                {
                    Value = equipmentAspect.entity
                });

                ecb.AddComponent<DestroyEntityIfPlacementNotValidCD>(entity);
                if (math.any(direction != 0f))
                {
                    ecb.SetComponent(entity, new DirectionCD
                    {
                        direction = direction
                    });
                }

                consumeAmount++;
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
            BufferLookup<GivesConditionsWhenEquippedBuffer> conditionsLookup,
            ComponentLookup<DamageReductionCD> damageReductionLookup,
            ref PugDatabase.EntityObjectInfo entityObjectInfo,
            ref PlacementCD placement,
            bool doConsume,
            int2 position,
            ref bool usedShovel,
            ref bool usedPickaxe
        )
        {
            int pickaxeDamage = HelperLogic.GetBestToolsSlots(
                equipmentAspect,
                sharedData,
                lookupData,
                conditionsLookup,
                out int shovelSlot,
                out int pickaxeSlot,
                out ObjectDataCD shovel,
                out ObjectDataCD pickaxe
            );

            TileAccessor tileAccessor = sharedData.tileAccessor;

            int itemTileset = entityObjectInfo.tileset;
            TileType itemTileType = entityObjectInfo.tileType;

            TileCD tile;
            bool foundTile;

            if (itemTileType == TileType.wall)
            {
                foundTile = tileAccessor.GetType(position, TileType.wall, out tile);
                var tileInfo = PugDatabase.TryGetTileItemInfo(TileType.ground, (Tileset)itemTileset, sharedData.tileWithTilesetToObjectDataMapCD);

                if (!foundTile && tileInfo.objectID != ObjectID.None)
                {
                    foundTile = tileAccessor.GetType(position, TileType.ground, out tile);
                    itemTileType = TileType.ground;
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

            if (targetObjectData.objectID == ObjectID.None ||
                targetObjectData.objectID == ObjectID.WallObsidianBlock ||
                targetObjectData.objectID == ObjectID.GroundObsidianBlock) return false;

            ref var targetObjectInfo = ref PugDatabase.GetEntityObjectInfo(
                targetObjectData.objectID,
                sharedData.databaseBank.databaseBankBlob,
                targetObjectData.variation
            );
            var itemEntity = targetObjectInfo.prefabEntities[0];

            if (tile.tileType == TileType.wall)
            {
                if (pickaxeSlot == -1) return false;

                var reduction = damageReductionLookup[itemEntity];
                if (pickaxeDamage - reduction.reduction <= 0) return false;

                usedPickaxe = true;
            }

            if (tile.tileType == TileType.ground)
            {
                if (shovelSlot == -1) return false;
                usedShovel = true;
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

            var posFloat = position.ToFloat3();
            var giveObject = targetObjectData;
            
            if (targetWallObjectData.objectID != ObjectID.None && tile.tileType == TileType.ground)
                giveObject = targetWallObjectData;
            
            EntityUtility.CreateAndDropItem(
                giveObject.objectID,
                giveObject.variation,
                1,
                posFloat,
                equipmentAspect.entity,
                sharedData.databaseBank.databaseBankBlob,
                sharedData.ecb
            );

            var isInGodMode = lookupData.godModeLookup.IsComponentEnabled(equipmentAspect.entity);

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
                        posFloat,
                        placement.currentPrefabVariation)
                });

                if (tile.tileType == TileType.ground)
                {
                    HelperLogic.ConsumeEquipmentInSlot(
                        equipmentAspect,
                        sharedData,
                        inventoryChangeBuffers,
                        shovelSlot,
                        shovel,
                        posFloat);
                }

                if (tile.tileType == TileType.wall)
                {
                    HelperLogic.ConsumeEquipmentInSlot(
                        equipmentAspect,
                        sharedData,
                        inventoryChangeBuffers,
                        pickaxeSlot,
                        pickaxe,
                        posFloat);
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
            bool flag = equipmentAspect.clientInput.ValueRO.IsButtonSet(CommandInputButtonNames.SecondInteract_Pressed) ||
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