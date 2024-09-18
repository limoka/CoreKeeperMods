using PlacementPlus.Components;
using PlayerEquipment;
using PlayerState;
using PugTilemap;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Physics;

namespace PlacementPlus
{
    internal static class ShovelLogic
    {
        public static bool UpdateShovelPlus(
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

        public static void Dig(
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

        public static bool DigAt(
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

        public static void FindDamagedTiles(
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

        public static void PlayDigEffects(float3 position, EquipmentUpdateAspect equipmentUpdateAspect, EquipmentUpdateSharedData equipmentUpdateSharedData)
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
    }
}