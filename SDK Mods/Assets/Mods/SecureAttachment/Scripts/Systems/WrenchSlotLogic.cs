using System;
using System.Collections.Generic;
using CoreLib.Submodule.EquipmentSlot.Interface;
using PlayerEquipment;
using PlayerState;
using Pug.UnityExtensions;
using Pug.Properties;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;

namespace SecureAttachment
{
    public class WrenchSlotLogic : IEquipmentLogic, IPlacementLogic
    {
        public bool CanUseWhileSitting => false;
        public bool CanUseWhileOnBoat => false;
        public bool CanResize => true;

        private ComponentLookup<WrenchCD> _wrenchLookup;
        private ComponentLookup<WrenchStateCD> _wrenchStateLookup;
        private ComponentLookup<MountedCD> _mountedLookup;

        public void CreateLookups(ref SystemState state)
        {
            _wrenchLookup = state.GetComponentLookup<WrenchCD>();
            _wrenchStateLookup = state.GetComponentLookup<WrenchStateCD>();
            _mountedLookup = state.GetComponentLookup<MountedCD>();
        }

        public bool Update(
            EquipmentUpdateAspect aspect,
            EquipmentUpdateSharedData sharedData,
            LookupEquipmentUpdateData lookupData,
            bool interactHeld,
            bool secondInteractHeld,
            bool hasItemInMouse)
        {
            var objectData = aspect.equippedObjectCD.ValueRO.containedObject.objectData;
            ref var objectInfo = ref PugDatabase.GetEntityObjectInfo(objectData.objectID, sharedData.databaseBank.databaseBankBlob, objectData.variation);
            if (objectInfo.objectID == ObjectID.None || objectInfo.prefabEntities.Length <= 0) return false;
            
            var prefabEntity = objectInfo.prefabEntities[0];

            if (!_wrenchLookup.HasComponent(prefabEntity)) return false;
            WrenchCD wrenchCd = _wrenchLookup[prefabEntity];
            
            var nativeList = new NativeList<PlacementHandler.EntityAndInfoFromPlacement>(Allocator.Temp);
            PlacementHandler.UpdatePlaceablePosition(
                aspect.equippedObjectCD.ValueRO.equipmentPrefab,
                ref nativeList,
                aspect,
                sharedData,
                lookupData);
            nativeList.Dispose();

            if (!secondInteractHeld) return false;

            ref PlacementCD placement = ref aspect.placementCD.ValueRW;
            
            int3 pos = placement.bestPositionToPlaceAt;
            int2 toolSize = EquipmentSlot.GetTileSizeFromVariation(aspect.equipmentSlotCD.ValueRO, in aspect.placementSizeByEquipmentTypeBuffer, objectInfo.prefabTileSize);
            
            var wrenchState = _wrenchStateLookup[aspect.entity];
            
            int successCount = 0;
            int failureCount = 0;
            
            for (int i = 0; i < toolSize.x; i++)
            {
                for (int j = 0; j < toolSize.y; j++)
                {
                    int3 currentPos = pos + new int3(i, 0, j);

                    var result = MountAtPos(aspect, sharedData, lookupData, wrenchCd, wrenchState, currentPos);

                    if (result == ActionResult.Success)
                        successCount++;
                    else if (result == ActionResult.Failure)
                        failureCount++;
                }
            }

            if (successCount == 0 && failureCount == 0) return false;
                
            aspect.playerStateCD.ValueRW.PushState(PlayerStateEnum.PlaceObject);
            float cooldown = (lookupData.godModeLookup.IsComponentEnabled(aspect.entity) ? 0.15f : 0.25f);
            EquipmentSlot.StartCooldownForItem(aspect, sharedData, lookupData, cooldown);

            if (successCount > 0)
                DoEffect(aspect, sharedData, SecureAttachmentMod.wrenchEffect, pos);
            else if (failureCount > 0)
                DoEffect(aspect, sharedData, SecureAttachmentMod.wrenchFailedEffect, pos);
            
            return false;
        }

        private ActionResult MountAtPos(
            EquipmentUpdateAspect aspect, 
            EquipmentUpdateSharedData sharedData,
            LookupEquipmentUpdateData lookupData, 
            WrenchCD wrench,
            WrenchStateCD wrenchState,
            int3 pos
        ) {
            var targets = new NativeList<Entity>(Allocator.Temp);
            FindPotentialTargets(sharedData, lookupData, ref targets, pos);

            Entity target = Entity.Null;
            
            foreach (Entity entity in targets)
            {
                LocalTransform transform = lookupData.localTransformLookup[entity];
                int2 objectPos = transform.Position.RoundToInt2();

                if (!math.all(objectPos == pos.ToInt2())) continue;

                target = entity;
                break;
            }

            if (target == Entity.Null) return ActionResult.Skipped;
            
            MountedCD mountedCd = default;

            if (_mountedLookup.HasComponent(target))
                mountedCd = _mountedLookup[target];

            switch (wrenchState.mode)
            {
                case WrenchMode.Auto:
                {
                    if (mountedCd.mountType == MountType.None)
                    {
                        MakeMounted(sharedData, mountedCd, wrench, target);
                        return ActionResult.Success;
                    }
            
                    if (mountedCd.mountType <= wrench.mountType)
                    {
                        Unmount(aspect, sharedData, lookupData, target);
                        return ActionResult.Success;
                    }

                    return ActionResult.Failure;
                }
                case WrenchMode.Mount:
                {
                    if (mountedCd.mountType != MountType.None)
                        return ActionResult.Skipped;
                    
                    MakeMounted(sharedData, mountedCd, wrench, target);
                    return ActionResult.Success;
                }
                case WrenchMode.Unmount:
                {
                    if (mountedCd.mountType > wrench.mountType) 
                        return ActionResult.Failure;
                    
                    Unmount(aspect, sharedData, lookupData, target);
                    return ActionResult.Success;

                }
            }
            
            return ActionResult.Skipped;
        }

        private static void Unmount(
            EquipmentUpdateAspect aspect, 
            EquipmentUpdateSharedData sharedData,
            LookupEquipmentUpdateData lookupData, 
            Entity target)
        {
            EntityUtility.Destroy(
                target, false, 
                aspect.entity, 
                lookupData.healthLookup,
                lookupData.entityDestroyedLookup, 
                lookupData.dontDropSelfLookup, 
                lookupData.dontDropLootLookup, 
                lookupData.killedByPlayerLookup,
                lookupData.plantLookup, 
                lookupData.summarizedConditionEffectsBufferLookup, 
                ref aspect.randomCD.ValueRW.Value, 
                lookupData.moveToPredictedByEntityDestroyedLookup, 
                sharedData.currentTick);
        }

        private static void MakeMounted(
            EquipmentUpdateSharedData sharedData, 
            MountedCD mountedIn,
            WrenchCD wrench,
            Entity target
        ) {
            MountedCD mounted = mountedIn;
            mounted.mountType = wrench.mountType;
            sharedData.ecb.SetComponent(target, mounted);
        }

        private static void DoEffect(
            EquipmentUpdateAspect equipmentAspect, 
            EquipmentUpdateSharedData sharedData, 
            EffectID effectID,
            int3 pos)
        {
            DynamicBuffer<GhostEffectEventBuffer> ghostEffectEventBuffer = equipmentAspect.ghostEffectEventBuffer;
            ref var bufferPtr = ref equipmentAspect.ghostEffectEventBufferPointerCD.ValueRW;
            
            var buffer = new GhostEffectEventBuffer
            {
                Tick = sharedData.currentTick,
                value = new EffectEventCD
                {
                    effectID = effectID,
                    position1 = pos
                }
            };
            ghostEffectEventBuffer.AddToRingBuffer(ref bufferPtr, buffer);
        }

        private void FindPotentialTargets(
            EquipmentUpdateSharedData sharedData,
            LookupEquipmentUpdateData lookupData,
            ref NativeList<Entity> targets,
            int3 center)
        {
            NativeList<ColliderCastHit> results = new NativeList<ColliderCastHit>(Allocator.Temp);

            PhysicsCollider collider = GetBoxCollider(new float3(0, -0.5f, 0), new float3(1, 1, 1), 0xffffffff);
            ColliderCastInput input = PhysicsManager.GetColliderCastInput(center, center, collider);

            sharedData.physicsWorldHistory.GetCollisionWorldFromTick(
                sharedData.currentTick, 1U,
                ref sharedData.physicsWorld,
                out CollisionWorld collisionWorld);

            bool res = collisionWorld.CastCollider(input, ref results);
            if (!res) return;

            // ReSharper disable once ForCanBeConvertedToForeach
            for (int i = 0; i < results.Length; i++)
            {
                ColliderCastHit castHit = results[i];
                Entity entity = castHit.Entity;
                
                if (!lookupData.objectPropertiesLookup.HasComponent(entity)) continue;

                var properties = lookupData.objectPropertiesLookup[entity];
                if (!properties.IsValid || !properties.Has(PropertyID.PlaceableObject.placeableObject)) continue;

                bool hasMounted = _mountedLookup.HasComponent(entity);
                if (!hasMounted) continue;
                
                targets.Add(entity);
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

        public int CanPlaceObjectAtPosition(Entity placementPrefab, int3 posToPlaceAt, int width, int height, NativeHashMap<int3, bool> tilesChecked,
            ref NativeList<PlacementHandler.EntityAndInfoFromPlacement> diggableEntityAndInfos, in EquipmentUpdateAspect equipmentUpdateAspect, in EquipmentUpdateSharedData equipmentUpdateSharedData,
            in LookupEquipmentUpdateData equipmentUpdateLookupData)
        {   
            return width * height;
        }
    }
}