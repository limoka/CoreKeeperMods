using CoreLib.Util.Extensions;
using Inventory;
using PlacementPlus.Access;
using PlacementPlus.Components;
using PlayerCommand;
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
using SphereCollider = Unity.Physics.SphereCollider;

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
                        state.CheckSize(currentTick);
                        return;
                    }

                    state.currentMaxSize = GetShovelLevel(damage);
                    state.CheckSize(currentTick);
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
                    DynamicBuffer<ShovelDigQueueBuffer> digQueue,
                    in ClientInput clientInput,
                    in PlacementPlusState state,
                    in CommandDataInterpolationDelay interpolationDelay
                ) =>
                {
                    if (state.size == 0 || state.mode == BrushMode.NONE) return;

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

                    bool onCooldown = EquipmentSlot.IsItemOnCooldown(
                        equipmentAspect.equippedObjectCD.ValueRO,
                        databaseBank,
                        cooldownLookup,
                        equipmentAspect.syncedSharedCooldownTimers,
                        equipmentAspect.localPlayerSharedCooldownTimers, currentTick);
                    if (onCooldown) return;

                    var slotType = equipmentAspect.equipmentSlotCD.ValueRO.slotType;

                    if (slotType == EquipmentSlotType.PlaceObjectSlot ||
                        slotType == (EquipmentSlotType)100)
                    {
                        var success = UpdatePlaceObjectPlus(equipmentAspect, equipmentShared, lookupData, state, secondInteractHeld);
                        if (!success) return;

                        equipmentAspect.equipmentSlotCD.ValueRW.secondInteractIsPendingToBeUsed = false;
                    }
                    else if (slotType == EquipmentSlotType.ShovelSlot ||
                             slotType == (EquipmentSlotType)101)
                    {
                        var success = UpdateShovelPlus(
                            equipmentAspect,
                            equipmentShared,
                            lookupData,
                            //digSharedLocal,
                            interpolationDelay,
                            digQueue,
                            state,
                            secondInteractHeld);
                        if (!success) return;

                        equipmentAspect.equipmentSlotCD.ValueRW.secondInteractIsPendingToBeUsed = false;
                    }
                })
                .WithoutBurst()
                .Schedule();

            base.OnUpdate();
        }

        private static bool UpdateShovelPlus(
            EquipmentUpdateAspect equipmentAspect,
            EquipmentUpdateSharedData equipmentShared,
            LookupEquipmentUpdateData lookupData,
            CommandDataInterpolationDelay interpolationDelay,
            DynamicBuffer<ShovelDigQueueBuffer> digQueue,
            PlacementPlusState state,
            bool secondInteractHeld)
        {
            var nativeList = new NativeList<PlacementHandler.EntityAndInfoFromPlacement>(Allocator.Temp);
            MyPlacementHandler.UpdatePlaceablePosition(
                equipmentAspect.equippedObjectCD.ValueRO.equipmentPrefab,
                ref nativeList,
                equipmentAspect,
                equipmentShared,
                lookupData,
                state);

            if (equipmentAspect.equippedObjectCD.ValueRO.isBroken)
            {
                nativeList.Dispose();
                return false;
            }

            bool hasItemInMouse =
                lookupData.containedObjectsBufferLookup.TryGetBuffer(equipmentAspect.entity, out DynamicBuffer<ContainedObjectsBuffer> dynamicBuffer) &&
                lookupData.craftingLookup.TryGetComponent(equipmentAspect.entity, out CraftingCD craftingCD) &&
                dynamicBuffer.Length > craftingCD.outputSlotIndex &&
                dynamicBuffer[craftingCD.outputSlotIndex].objectID > ObjectID.None;
            if (hasItemInMouse)
            {
                nativeList.Dispose();
                return false;
            }

            equipmentAspect.equipmentSlotCD.ValueRW.slotType = (EquipmentSlotType)101;
            if (!secondInteractHeld) return false;

            PlacementPlusMod.Log.LogInfo($"About to dig, got {nativeList.Length} results!");

            Dig(
                ref nativeList,
                equipmentAspect,
                equipmentShared,
                lookupData,
                interpolationDelay,
                digQueue,
                state
            );

            nativeList.Dispose();
            return true;
        }

        private static void Dig(
            ref NativeList<PlacementHandler.EntityAndInfoFromPlacement> entityInfo,
            in EquipmentUpdateAspect equipmentAspect,
            EquipmentUpdateSharedData sharedData,
            LookupEquipmentUpdateData lookupData,
            CommandDataInterpolationDelay interpolationDelay,
            DynamicBuffer<ShovelDigQueueBuffer> digQueue,
            PlacementPlusState state
        )
        {
            ref PlacementCD placement = ref equipmentAspect.placementCD.ValueRW;

            float cooldown = (lookupData.godModeLookup.IsComponentEnabled(equipmentAspect.entity) ? 0.15f : 0.4f);
            EquipmentSlot.StartCooldownForItem(equipmentAspect, sharedData, lookupData, cooldown);

            BrushRect extents = state.GetExtents();
            var center = placement.bestPositionToPlaceAt.ToInt2();

            equipmentAspect.equipmentSlotCD.ValueRW.slotType = EquipmentSlotType.PlaceObjectSlot;
            equipmentAspect.playerStateCD.ValueRW.SetNextState(PlayerStateEnum.Dig);

            var count = (extents.width + 1) * (extents.height + 1);

            var brush = extents.WithPos(center);
            var tileLookup = new NativeHashMap<int2, TileData>(count, Allocator.Temp);

            FindDamagedTiles(
                sharedData,
                lookupData,
                ref tileLookup,
                interpolationDelay,
                brush
            );

            bool gotAnything = false;

            foreach (int3 pos in brush)
            {
                if (DigAt(
                        entityInfo,
                        equipmentAspect,
                        sharedData,
                        lookupData,
                        ref tileLookup,
                        digQueue,
                        pos
                    ))
                {
                    PlayDigEffects(placement.bestPositionToPlaceAt + new float3(0f, 1f, 0f) * 0.1f, equipmentAspect, sharedData);
                    gotAnything = true;
                }
            }

            tileLookup.Dispose();
            equipmentAspect.equipmentSlotCD.ValueRW.slotType = (EquipmentSlotType)101;

            if (!gotAnything) return;

            lookupData.reduceDurabilityOfEquippedTagLookup.SetComponentEnabled(equipmentAspect.entity, true);
            lookupData.reduceDurabilityOfEquippedTagLookup.GetRefRW(equipmentAspect.entity).ValueRW.triggerCounter++;

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
        }

        private static bool DigAt(
            NativeList<PlacementHandler.EntityAndInfoFromPlacement> entityInfo,
            EquipmentUpdateAspect equipmentAspect,
            EquipmentUpdateSharedData sharedData,
            LookupEquipmentUpdateData lookupData,
            ref NativeHashMap<int2, TileData> tileLookup,
            DynamicBuffer<ShovelDigQueueBuffer> digQueue,
            int3 position)
        {
            equipmentAspect.digStateCD.ValueRW.positionToPlaceAt = position;
            equipmentAspect.flattenStateCD.ValueRW.positionToPlaceAt = position;

            Entity targetEntity = default;
            int targetTileset = default;
            TileType targetTile = default;

            bool foundData = false;

            for (int i = 0; i < entityInfo.Length; i++)
            {
                var info = entityInfo[i];
                if (info.pos.x != position.x ||
                    info.pos.z != position.z) continue;

                targetEntity = info.entity;
                targetTileset = info.tileset;
                targetTile = info.tileType;
                foundData = true;
                break;
            }

            int2 posInt2 = position.ToInt2();
            
            if ((!foundData || targetEntity == Entity.Null) && 
                tileLookup.ContainsKey(posInt2))
            {
                var tileData = tileLookup[posInt2];
                targetEntity = tileData.entity;
                targetTileset = tileData.tileCd.tileset;
                targetTile = tileData.tileCd.tileType;
                foundData = true;
            }

            if (!foundData) return false;


            if (targetTile == TileType.ground ||
                targetTile == TileType.dugUpGround ||
                targetTile == TileType.wateredGround)
            {
                digQueue.Add(new ShovelDigQueueBuffer()
                {
                    position = posInt2,
                    entity = targetEntity,
                    tileset = targetTileset,
                    tileType = targetTile
                });
                return true;
            }

            Entity entity = entityInfo[0].entity;
            if (entity == Entity.Null)
            {
                var dynamicBuffer = lookupData.tileUpdateBufferLookup[sharedData.tileUpdateBufferEntity];

                PlayerController.DigUpTile(
                    targetTile,
                    targetTileset,
                    position,
                    equipmentAspect.entity,
                    dynamicBuffer,
                    sharedData.tileAccessor,
                    sharedData.databaseBank,
                    sharedData.ecb,
                    sharedData.tileWithTilesetToObjectDataMapCD,
                    lookupData.tileLookup,
                    sharedData.isFirstTimeFullyPredictingTick);

                return true;
            }

            if (lookupData.destructibleLookup.HasComponent(entity))
            {
                EntityUtility.DropDestructible(entity, equipmentAspect.entity, lookupData, sharedData);
                return true;
            }

            PlayerController.OnHarvest(
                equipmentAspect.entity,
                ref equipmentAspect.hungerCD.ValueRW,
                equipmentAspect.playerStateCD.ValueRO,
                sharedData.ecb,
                sharedData.isServer,
                entity, true,
                lookupData.plantLookup,
                lookupData.growingLookup,
                lookupData.objectDataLookup,
                lookupData.healthLookup,
                lookupData.summarizedConditionsBufferLookup,
                sharedData.achievementArchetype
            );

            Random value = equipmentAspect.randomCD.ValueRO.Value;
            Random random = new Random(value.NextUInt());
            EntityUtility.Destroy(entity,
                false,
                equipmentAspect.entity,
                lookupData.healthLookup,
                lookupData.entityDestroyedLookup,
                lookupData.dontDropSelfLookup,
                lookupData.dontDropLootLookup,
                lookupData.killedByPlayerLookup,
                lookupData.plantLookup,
                lookupData.summarizedConditionEffectsBufferLookup,
                ref random,
                lookupData.moveToPredictedByEntityDestroyedLookup,
                sharedData.currentTick
            );

            return true;
        }

        private static void FindDamagedTiles(
            EquipmentUpdateSharedData sharedData,
            LookupEquipmentUpdateData lookupData,
            ref NativeHashMap<int2, TileData> tileLookup,
            CommandDataInterpolationDelay interpolationDelay,
            BrushRect extents
        )
        {
            NativeList<ColliderCastHit> results = new NativeList<ColliderCastHit>(Allocator.Temp);

            float3 offsetVec = new float3(extents.width / 2f, 0, extents.height / 2f);
            float3 worldPos = extents.pos.ToFloat3() + offsetVec;

            float3 rectSize = new float3(extents.width + 1, 1f, extents.height + 1);
            float3 position = new float3(0, -0.5f, 0);


            PhysicsCollider collider = GetBoxCollider(position, rectSize, 0xffffffff);
            ColliderCastInput input = PhysicsManager.GetColliderCastInput(worldPos, worldPos, collider);

            sharedData.physicsWorldHistory.GetCollisionWorldFromTick(
                sharedData.currentTick, interpolationDelay.Delay,
                ref sharedData.physicsWorld,
                out CollisionWorld collisionWorld);

            bool res = collisionWorld.CastCollider(input, ref results);
            if (!res) return;

            foreach (ColliderCastHit castHit in results)
            {
                if (!lookupData.tileLookup.TryGetComponent(castHit.Entity, out TileCD tileCD)) continue;
                if (tileCD.tileType != TileType.ground) continue;

                int2 pos = collisionWorld.Bodies[castHit.RigidBodyIndex].WorldFromBody.pos.RoundToInt2();
                if (tileLookup.ContainsKey(pos)) continue;
                
                tileLookup.Add(pos, new TileData(castHit.Entity, tileCD));
            }
        }

        public static PhysicsCollider GetBoxCollider(
            float3 position,
            float3 size,
            uint layerMaskCollidesWith)
        {
            BlobAssetReference<Unity.Physics.Collider> blobAssetReference = Unity.Physics.BoxCollider.Create(new BoxGeometry()
            {
                Center = position,
                Orientation = quaternion.identity,
                Size = size,
                BevelRadius = 0.0f
            }, PhysicsManager.GetCollisionFilter(uint.MaxValue, layerMaskCollidesWith));

            return new PhysicsCollider()
            {
                Value = blobAssetReference
            };
        }

        private static void PlayDigEffects(float3 position, EquipmentUpdateAspect equipmentUpdateAspect, EquipmentUpdateSharedData equipmentUpdateSharedData)
        {
            DynamicBuffer<GhostEffectEventBuffer> ghostEffectEventBuffer = equipmentUpdateAspect.ghostEffectEventBuffer;
            ref GhostEffectEventBufferPointerCD valueRW = ref equipmentUpdateAspect.ghostEffectEventBufferPointerCD.ValueRW;
            GhostEffectEventBuffer ghostEffectEventBuffer2 = default(GhostEffectEventBuffer);
            ghostEffectEventBuffer2.Tick = equipmentUpdateSharedData.currentTick;
            ghostEffectEventBuffer2.value = new EffectEventCD
            {
                effectID = EffectID.DigGround,
                position1 = position
            };
            ghostEffectEventBuffer.AddToRingBuffer(ref valueRW, ghostEffectEventBuffer2);
        }

        private static bool UpdatePlaceObjectPlus(EquipmentUpdateAspect equipmentAspect, EquipmentUpdateSharedData equipmentShared,
            LookupEquipmentUpdateData lookupData, PlacementPlusState state, bool secondInteractHeld)
        {
            var containedObject = equipmentAspect.equippedObjectCD.ValueRO.containedObject;
            if (containedObject.auxDataIndex > 0) return false;

            ObjectDataCD objectData = containedObject.objectData;
            ref PugDatabase.EntityObjectInfo entityObjectInfo = ref PugDatabase.GetEntityObjectInfo(objectData.objectID,
                equipmentShared.databaseBank.databaseBankBlob, objectData.variation);

            if (!IsItemValid(ref entityObjectInfo)) return false;

            bool hasItemInMouse =
                lookupData.containedObjectsBufferLookup.TryGetBuffer(equipmentAspect.entity, out DynamicBuffer<ContainedObjectsBuffer> dynamicBuffer) &&
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

            return PlaceItemGrid(equipmentAspect, equipmentShared, lookupData, state);
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

            NativeHashMap<int3, bool> tilesChecked = new NativeHashMap<int3, bool>(32, Allocator.Temp);
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

            ObjectDataCD equippedObject = equipmentAspect.equippedObjectCD.ValueRO.containedObject.objectData;

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

            TileType targetTileToPlace = GetTileTypeToPlace(pos, ref objectToPlaceInfo, equipmentUpdateSharedData.tileAccessor,
                equipmentUpdateSharedData.tileWithTilesetToObjectDataMapCD);
            bool flag = equipmentUpdateAspect.clientInput.ValueRO.IsButtonSet(CommandInputButtonNames.SecondInteract_Pressed) ||
                        (targetTileToPlace != TileType.wall && targetTileToPlace != TileType.ground) ||
                        (targetTileToPlace == TileType.wall && valueRW.previouslyPlacedTileType == TileType.wall) ||
                        (targetTileToPlace == TileType.ground && valueRW.previouslyPlacedTileType == TileType.ground) || !valueRW.tilePlacementTimer.isRunning ||
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