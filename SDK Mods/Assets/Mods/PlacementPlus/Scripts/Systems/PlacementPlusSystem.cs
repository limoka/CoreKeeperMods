using CoreLib.Util.Extensions;
using Inventory;
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
using Unity.NetCode;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using Random = Unity.Mathematics.Random;

namespace PlacementPlus.Systems
{
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(EquipmentUpdateSystemGroup))]
    [UpdateBefore(typeof(EquipmentUpdateSystem))]
    public partial class PlacementPlusSystem : PugSimulationSystemBase
    {
        private uint _tickRate;
        private EntityArchetype _achievementArchetype;

        protected override void OnCreate()
        {
            _tickRate = (uint)NetworkingManager.GetSimulationTickRateForPlatform();
            _achievementArchetype = AchievementSystem.GetRpcArchetype(EntityManager);

            RequireForUpdate<PhysicsWorldSingleton>();
            RequireForUpdate<WorldInfoCD>();
            RequireForUpdate<TileWithTilesetToObjectDataMapCD>();

            NeedDatabase();
            base.OnCreate();
        }

        protected override void OnUpdate()
        {
            var worldInfoCD = SystemAPI.GetSingleton<WorldInfoCD>();
            var networkTime = SystemAPI.GetSingleton<NetworkTime>();
            var currentTick = networkTime.ServerTick;

            var databaseBank = SystemAPI.GetSingleton<PugDatabase.DatabaseBankCD>();

            var cooldownLookup = GetComponentLookup<CooldownCD>(true);
            var ecb = CreateCommandBuffer();
            var tileAccessor = CreateTileAccessor();

            var givesConditionsLookup = SystemAPI.GetBufferLookup<GivesConditionsWhenEquippedBuffer>();

            Entities.ForEach((
                    ref PlacementPlusState state,
                    in EquippedObjectCD equippedObjectCD
                ) =>
                {
                    int maxSize = PlacementPlusMod.maxSize.Value - 1;

                    ObjectDataCD objectData = equippedObjectCD.containedObject.objectData;
                    ref PugDatabase.EntityObjectInfo entityObjectInfo = ref PugDatabase.GetEntityObjectInfo(objectData.objectID,
                        databaseBank.databaseBankBlob, objectData.variation);

                    int damage = GetShovelDamage(objectData, ref entityObjectInfo, givesConditionsLookup);
                    if (damage == 0)
                    {
                        state.currentMaxSize = maxSize;
                        return;
                    }

                    state.currentMaxSize = GetShovelLevel(damage);
                })
                .WithName("UpdateMaxSize")
                .WithoutBurst()
                .Schedule();

            var equipmentShared = new EquipmentUpdateSharedData
            {
                currentTick = currentTick,
                databaseBank = databaseBank,
                worldInfoCD = worldInfoCD,
                tickRate = _tickRate,
                physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld,
                physicsWorldHistory = SystemAPI.GetSingleton<PhysicsWorldHistorySingleton>(),
                inventoryUpdateBufferEntity = SystemAPI.GetSingletonEntity<InventoryChangeBuffer>(),
                tileUpdateBufferEntity = SystemAPI.GetSingletonEntity<TileUpdateBuffer>(),
                tileAccessor = tileAccessor,
                tileWithTilesetToObjectDataMapCD = SystemAPI.GetSingleton<TileWithTilesetToObjectDataMapCD>(),
                colliderCacheCD = SystemAPI.GetSingleton<ColliderCacheCD>(),
                isServer = isServer,
                ecb = ecb,
                isFirstTimeFullyPredictingTick = networkTime.IsFirstTimeFullyPredictingTick,
                achievementArchetype = _achievementArchetype
            };

            var lookupData = new LookupEquipmentUpdateData
            {
                secondaryUseLookup = SystemAPI.GetComponentLookup<SecondaryUseCD>(),
                cooldownLookup = SystemAPI.GetComponentLookup<CooldownCD>(),
                consumeManaLookup = SystemAPI.GetComponentLookup<ConsumesManaCD>(),
                levelLookup = SystemAPI.GetComponentLookup<LevelCD>(),
                levelEntitiesLookup = SystemAPI.GetBufferLookup<LevelEntitiesBuffer>(),
                parchementRecipeLookup = SystemAPI.GetComponentLookup<ParchmentRecipeCD>(),
                objectDataLookup = SystemAPI.GetComponentLookup<ObjectDataCD>(),
                attackWithEquipmentLookup = SystemAPI.GetComponentLookup<AttackWithEquipmentTag>(),
                inventoryUpdateBuffer = SystemAPI.GetBufferLookup<InventoryChangeBuffer>(),
                cattleLookup = SystemAPI.GetComponentLookup<CattleCD>(),
                petCandyLookup = SystemAPI.GetComponentLookup<PetCandyCD>(),
                potionLookup = SystemAPI.GetComponentLookup<PotionCD>(),
                localTransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(),
                petLookup = SystemAPI.GetComponentLookup<PetCD>(),
                playAnimationStateLookup = SystemAPI.GetComponentLookup<PlayAnimationStateCD>(),
                simulateLookup = SystemAPI.GetComponentLookup<Simulate>(),
                waitingForEatableSlotConsumeResultLookup = SystemAPI.GetComponentLookup<WaitingForEatableSlotConsumeResultCD>(),
                tileUpdateBufferLookup = SystemAPI.GetBufferLookup<TileUpdateBuffer>(),
                tileLookup = SystemAPI.GetComponentLookup<TileCD>(),
                objectPropertiesLookup = SystemAPI.GetComponentLookup<ObjectPropertiesCD>(),
                adaptiveEntityBufferLookup = SystemAPI.GetBufferLookup<AdaptiveEntityBuffer>(),
                directionBasedOnVariationLookup = SystemAPI.GetComponentLookup<DirectionBasedOnVariationCD>(),
                directionLookup = SystemAPI.GetComponentLookup<DirectionCD>(),
                playerGhostLookup = SystemAPI.GetComponentLookup<PlayerGhost>(),
                minionLookup = SystemAPI.GetComponentLookup<MinionCD>(),
                indestructibleLookup = SystemAPI.GetComponentLookup<IndestructibleCD>(),
                plantLookup = SystemAPI.GetComponentLookup<PlantCD>(),
                critterLookup = SystemAPI.GetComponentLookup<CritterCD>(),
                fireflyLookup = SystemAPI.GetComponentLookup<FireflyCD>(),
                requiresDrillLookup = SystemAPI.GetComponentLookup<RequiresDrillCD>(),
                surfacePriorityLookup = SystemAPI.GetComponentLookup<SurfacePriorityCD>(),
                electricityLookup = SystemAPI.GetComponentLookup<ElectricityCD>(),
                eventTerminalLookup = SystemAPI.GetComponentLookup<EventTerminalCD>(),
                waterSourceLookup = SystemAPI.GetComponentLookup<WaterSourceCD>(),
                paintToolLookup = SystemAPI.GetComponentLookup<PaintToolCD>(),
                paintableObjectLookup = SystemAPI.GetComponentLookup<PaintableObjectCD>(),
                growingLookup = SystemAPI.GetComponentLookup<GrowingCD>(),
                healthLookup = SystemAPI.GetComponentLookup<HealthCD>(),
                summarizedConditionsBufferLookup = SystemAPI.GetBufferLookup<SummarizedConditionsBuffer>(),
                reduceDurabilityOfEquippedTagLookup = SystemAPI.GetComponentLookup<ReduceDurabilityOfEquippedTriggerCD>(),
                summarizedConditionEffectsBufferLookup = SystemAPI.GetBufferLookup<SummarizedConditionEffectsBuffer>(),
                entityDestroyedLookup = SystemAPI.GetComponentLookup<EntityDestroyedCD>(),
                dontDropSelfLookup = SystemAPI.GetComponentLookup<DontDropSelfCD>(),
                dontDropLootLookup = SystemAPI.GetComponentLookup<DontDropLootCD>(),
                killedByPlayerLookup = SystemAPI.GetComponentLookup<KilledByPlayerCD>(),
                destructibleLookup = SystemAPI.GetComponentLookup<DestructibleObjectCD>(),
                canBeRemovedByWaterLookup = SystemAPI.GetComponentLookup<CanBeRemovedByWaterCD>(),
                groundDecorationLookup = SystemAPI.GetComponentLookup<GroundDecorationCD>(),
                diggableLookup = SystemAPI.GetComponentLookup<DiggableCD>(),
                pseudoTileLookup = SystemAPI.GetComponentLookup<PseudoTileCD>(),
                dontBlockDiggingLookup = SystemAPI.GetComponentLookup<DontBlockDiggingCD>(),
                fullnessLookup = SystemAPI.GetComponentLookup<FullnessCD>(),
                godModeLookup = SystemAPI.GetComponentLookup<GodModeCD>(),
                containedObjectsBufferLookup = SystemAPI.GetBufferLookup<ContainedObjectsBuffer>(),
                anvilLookup = SystemAPI.GetComponentLookup<AnvilCD>(),
                waypointLookup = SystemAPI.GetComponentLookup<WayPointCD>(),
                craftingLookup = SystemAPI.GetComponentLookup<CraftingCD>(),
                triggerAnimationOnDeathLookup = SystemAPI.GetComponentLookup<TriggerAnimationOnDeathCD>(),
                moveToPredictedByEntityDestroyedLookup = SystemAPI.GetComponentLookup<MoveToPredictedByEntityDestroyedCD>(),
                hasExplodedLookup = SystemAPI.GetComponentLookup<HasExplodedCD>()
            };

            Entities.ForEach((
                    EquipmentUpdateAspect equipmentAspect,
                    in ClientInput clientInput,
                    in PlacementPlusState state) =>
                {
                    if (state.size == 0) return;

                    bool interactHeldRaw = clientInput.IsButtonSet(CommandInputButtonNames.Interact_HeldDown);
                    bool secondInteractHeldRaw = clientInput.IsButtonSet(CommandInputButtonNames.SecondInteract_HeldDown);
                    if (!PlayerController.CurrentStateAllowInteractions(
                            worldInfoCD, equipmentAspect.playerGhost.ValueRO,
                            equipmentAspect.playerStateCD.ValueRO,
                            equipmentAspect.equipmentSlotCD.ValueRO, secondInteractHeldRaw && !interactHeldRaw, clientInput))
                    {
                        return;
                    }

                    bool interactHeld = interactHeldRaw | equipmentAspect.equipmentSlotCD.ValueRW.interactIsPendingToBeUsed;
                    bool secondInteractHeld = secondInteractHeldRaw | equipmentAspect.equipmentSlotCD.ValueRW.secondInteractIsPendingToBeUsed;

                    var slotType = equipmentAspect.equipmentSlotCD.ValueRO.slotType;
                    if (slotType != EquipmentSlotType.PlaceObjectSlot &&
                        slotType != (EquipmentSlotType)100) return;

                    bool onCooldown = EquipmentSlot.IsItemOnCooldown(
                        equipmentAspect.equippedObjectCD.ValueRO,
                        databaseBank,
                        cooldownLookup,
                        equipmentAspect.syncedSharedCooldownTimers,
                        equipmentAspect.localPlayerSharedCooldownTimers, currentTick);
                    if (onCooldown) return;

                    var containedObject = equipmentAspect.equippedObjectCD.ValueRO.containedObject;
                    if (containedObject.auxDataIndex > 0) return;

                    ObjectDataCD objectData = containedObject.objectData;
                    ref PugDatabase.EntityObjectInfo entityObjectInfo = ref PugDatabase.GetEntityObjectInfo(objectData.objectID,
                        equipmentShared.databaseBank.databaseBankBlob, objectData.variation);

                    if (!IsItemValid(ref entityObjectInfo)) return;

                    bool hasItemInMouse =
                        lookupData.containedObjectsBufferLookup.TryGetBuffer(equipmentAspect.entity, out DynamicBuffer<ContainedObjectsBuffer> dynamicBuffer) &&
                        lookupData.craftingLookup.TryGetComponent(equipmentAspect.entity, out CraftingCD craftingCD) &&
                        dynamicBuffer.Length > craftingCD.outputSlotIndex &&
                        dynamicBuffer[craftingCD.outputSlotIndex].objectID > ObjectID.None;
                    if (hasItemInMouse) return;

                    PlacementPlusMod.Log.LogInfo(
                        $"IsServer: {equipmentShared.isServer}, secondInteractIsPendingToBeUsed state: {equipmentAspect.equipmentSlotCD.ValueRW.secondInteractIsPendingToBeUsed}");

                    NativeList<PlacementHandler.EntityAndInfoFromPlacement> nativeList =
                        new NativeList<PlacementHandler.EntityAndInfoFromPlacement>(Allocator.Temp);
                    MyPlacementHandler.UpdatePlaceablePosition(
                        equipmentAspect.equippedObjectCD.ValueRO.equipmentPrefab,
                        ref nativeList,
                        equipmentAspect,
                        equipmentShared,
                        lookupData,
                        state);
                    nativeList.Dispose();

                    equipmentAspect.equipmentSlotCD.ValueRW.slotType = (EquipmentSlotType)100;

                    if (!secondInteractHeld)
                    {
                        return;
                    }

                    PlacementPlusMod.Log.LogInfo($"IsServer: {equipmentShared.isServer}, PlaceItemGrid");
                    var success = PlaceItemGrid(equipmentAspect, equipmentShared, lookupData, state);
                    PlacementPlusMod.Log.LogInfo($"IsServer: {equipmentShared.isServer},PlaceItemGrid, success: {success}");
                    if (!success) return;

                    equipmentAspect.equipmentSlotCD.ValueRW.secondInteractIsPendingToBeUsed = false;
                    PlacementPlusMod.Log.LogInfo($"IsServer: {equipmentShared.isServer}, Reset secondInteractIsPendingToBeUsed");
                })
                .WithoutBurst()
                .Schedule();

            base.OnUpdate();
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

        private static bool PlaceItemGrid(
            in EquipmentUpdateAspect equipmentAspect,
            EquipmentUpdateSharedData sharedData,
            LookupEquipmentUpdateData lookupData,
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

            NativeHashMap<int3, bool> tilesChecked = new NativeHashMap<int3, bool>(32,Allocator.Temp);
            equipmentAspect.equipmentSlotCD.ValueRW.slotType = EquipmentSlotType.PlaceObjectSlot;
            
            foreach (int3 pos in extents.WithPos(center))
            {
                PlaceAt(
                    equipmentAspect,
                    sharedData,
                    lookupData,
                    tilesChecked,
                    ref entityObjectInfo,
                    ref placement,
                    ref consumeAmount,
                    pos
                );
            }
            
            equipmentAspect.equipmentSlotCD.ValueRW.slotType = (EquipmentSlotType)100;

            tilesChecked.Dispose();

            PlacementPlusMod.Log.LogInfo($"Is Server: {sharedData.isServer}, about to consume {consumeAmount}!");
            lookupData.inventoryUpdateBuffer[sharedData.inventoryUpdateBufferEntity].Add(new InventoryChangeBuffer
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
            
            return true;
        }

        public static bool PlaceAt(
            in EquipmentUpdateAspect equipmentAspect,
            EquipmentUpdateSharedData sharedData,
            LookupEquipmentUpdateData lookupData,
            NativeHashMap<int3, bool> tilesChecked,
            ref PugDatabase.EntityObjectInfo entityObjectInfo,
            ref PlacementCD placement,
            ref int consumeAmount,
            int3 position
        )
        {
            Entity equipmentPrefab = equipmentAspect.equippedObjectCD.ValueRO.equipmentPrefab;

            if (!CanPlaceItem(equipmentPrefab, ref entityObjectInfo, position, equipmentAspect, sharedData,
                    lookupData))
            {
                return false;
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
            
            if (result == 0) return false;

            ObjectDataCD equippedObject = equipmentAspect.equippedObjectCD.ValueRO.containedObject.objectData;

            if (!PlayerController.CanConsumeEntityInSlot(
                    equipmentPrefab,
                    equippedObject,
                    consumeAmount + 1,
                    lookupData.cattleLookup)) return false;

            equipmentAspect.placeObjectStateCD.ValueRW.positionToPlaceAt = placement.bestPositionToPlaceAt;
            equipmentAspect.playerStateCD.ValueRW.PushState(PlayerStateEnum.PlaceObject);

            float3 positionToPlaceAt = placement.bestPositionToPlaceAt;

            float3 direction = float3.zero;
            TileAccessor tileAccessor = sharedData.tileAccessor;

            if (lookupData.tileLookup.HasComponent(equipmentPrefab))
            {
                TileType tile = GetTileTypeToPlace(position, ref entityObjectInfo, sharedData.tileAccessor, sharedData.tileWithTilesetToObjectDataMapCD);
                var dynamicBuffer = lookupData.tileUpdateBufferLookup[sharedData.tileUpdateBufferEntity];

                EntityUtility.AddTile(
                    entityObjectInfo.tileset,
                    tile,
                    new int2(position.x, position.z),
                    sharedData.worldInfoCD.IsWorldModeEnabled(WorldMode.Creative),
                    dynamicBuffer);

                consumeAmount++;
                placement.previouslyPlacedTileType = tile;
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
            return true;
        }

        private static bool CanPlaceItem(Entity placementPrefab, ref PugDatabase.EntityObjectInfo objectToPlaceInfo, int3 pos,
            in EquipmentUpdateAspect equipmentUpdateAspect, in EquipmentUpdateSharedData equipmentUpdateSharedData,
            in LookupEquipmentUpdateData equipmentUpdateLookupData)
        {
            ref PlacementCD valueRW = ref equipmentUpdateAspect.placementCD.ValueRW;
            ComponentLookup<TileCD> tileLookup = equipmentUpdateLookupData.tileLookup;
            if (!tileLookup.HasComponent(placementPrefab))
            {
                valueRW.tilePlacementTimer.Stop(equipmentUpdateSharedData.currentTick);
                return true;
            }

            TileType tileTypeToPlace = GetTileTypeToPlace(pos, ref objectToPlaceInfo, equipmentUpdateSharedData.tileAccessor,
                equipmentUpdateSharedData.tileWithTilesetToObjectDataMapCD);
            bool flag = equipmentUpdateAspect.clientInput.ValueRO.IsButtonSet(CommandInputButtonNames.SecondInteract_Pressed) ||
                        (tileTypeToPlace != TileType.wall && tileTypeToPlace != TileType.ground) ||
                        (tileTypeToPlace == TileType.wall && valueRW.previouslyPlacedTileType == TileType.wall) ||
                        (tileTypeToPlace == TileType.ground && valueRW.previouslyPlacedTileType == TileType.ground) || !valueRW.tilePlacementTimer.isRunning ||
                        valueRW.tilePlacementTimer.IsTimerElapsed(equipmentUpdateSharedData.currentTick);
            if (flag)
            {
                valueRW.tilePlacementTimer.Start(equipmentUpdateSharedData.currentTick, 0.65f, equipmentUpdateSharedData.tickRate);
            }

            return flag;
        }

        private static TileType GetTileTypeToPlace(int3 pos, ref PugDatabase.EntityObjectInfo objectToPlaceInfo, in TileAccessor tileAccessor,
            in TileWithTilesetToObjectDataMapCD tileWithTilesetToObjectDataMapCD)
        {
            int2 @int = pos.ToInt2();
            if (objectToPlaceInfo.tileType == TileType.wall)
            {
                TileAccessor tileAccessor2 = tileAccessor;
                if (!tileAccessor2.HasType(@int, TileType.ground))
                {
                    tileAccessor2 = tileAccessor;
                    if (!tileAccessor2.HasType(@int, TileType.bridge) &&
                        PugDatabase.TryGetTileItemInfo(TileType.ground, (Tileset)objectToPlaceInfo.tileset, tileWithTilesetToObjectDataMapCD).objectID !=
                        ObjectID.None)
                    {
                        return TileType.ground;
                    }
                }
            }

            return objectToPlaceInfo.tileType;
        }

        private static bool IsPlacingWallAfterPreviouslyPlacedGround(in PlacementCD placementCD, ref PugDatabase.EntityObjectInfo objectToPlaceInfo)
        {
            return placementCD.previouslyPlacedTileType == TileType.ground && objectToPlaceInfo.tileType == TileType.wall;
        }

        public static int GetShovelDamage(
            ObjectDataCD item,
            ref PugDatabase.EntityObjectInfo objectInfo,
            BufferLookup<GivesConditionsWhenEquippedBuffer> conditionsLookup)
        {
            if (item.objectID == ObjectID.None) return 0;
            if (item.amount == 0) return 0;

            if (objectInfo.objectType != ObjectType.Shovel) return 0;

            var entity = objectInfo.prefabEntities[0];
            if (!conditionsLookup.TryGetBuffer(entity, out var buffer)) return 0;

            foreach (GivesConditionsWhenEquippedBuffer condition in buffer)
            {
                if (condition.equipmentCondition.id != ConditionID.DiggingIncrease) continue;

                return condition.equipmentCondition.value;
            }

            return 0;
        }

        internal static int GetShovelLevel(int diggingDamage)
        {
            return diggingDamage switch
            {
                < 30 => 0,
                < 40 => 1,
                < 60 => 2,
                < 80 => 3,
                < 160 => 4,
                < 210 => 5,
                _ => 6
            };
        }
    }
}