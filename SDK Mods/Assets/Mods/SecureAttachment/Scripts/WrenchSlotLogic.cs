using System;
using System.Collections.Generic;
using CoreLib.Submodule.EquipmentSlot.Interface;
using PlayerEquipment;
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

        private ComponentLookup<WrenchCD> wrenchCDLookup;
        private ComponentLookup<MountedCD> mountedCDLookup;

        private ComponentLookup<QueueHitTriggerCD> queueHitLookup;
        
        private int placeableHash = Property.StringToHash("PlaceableObject/placeableObject");

        public void CreateLookups(ref SystemState state)
        {
            wrenchCDLookup = state.GetComponentLookup<WrenchCD>();
            mountedCDLookup = state.GetComponentLookup<MountedCD>();
            
            queueHitLookup = state.GetComponentLookup<QueueHitTriggerCD>();
        }

        public bool Update(
            EquipmentUpdateAspect aspect,
            EquipmentUpdateSharedData sharedData,
            LookupEquipmentUpdateData lookupData,
            bool interactHeld,
            bool secondInteractHeld)
        {
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

            var pos = placement.bestPositionToPlaceAt;

            ObjectDataCD objectData = aspect.equippedObjectCD.ValueRO.containedObject.objectData;
            var prefabEntity = PugDatabase.GetPrimaryPrefabEntity(
                objectData.objectID,
                sharedData.databaseBank.databaseBankBlob,
                objectData.variation
            );

            if (!wrenchCDLookup.HasComponent(prefabEntity)) return false;
            WrenchCD wrenchCd = wrenchCDLookup[prefabEntity];

            var targets = new NativeList<Entity>(Allocator.Temp);
            FindPotentialTargets(sharedData, lookupData, ref targets, pos, wrenchCd.wrenchTier);

            foreach (Entity target in targets)
            {
                MountedCD mountedCd = default;

                if (mountedCDLookup.HasComponent(target))
                    mountedCd = mountedCDLookup[target];

                LocalTransform transform = lookupData.localTransformLookup[target];
                int2 objectPos = transform.Position.RoundToInt2();

                if (math.all(objectPos == pos.ToInt2()))
                {
                    queueHitLookup.SetComponentEnabled(aspect.entity, true);
                    float cooldown = (lookupData.godModeLookup.IsComponentEnabled(aspect.entity) ? 0.15f : 0.25f);
                    EquipmentSlot.StartCooldownForItem(aspect, sharedData, lookupData, cooldown);

                    if (mountedCd.wrenchTier <= wrenchCd.wrenchTier)
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

                        DoEffect(aspect, sharedData, SecureAttachmentMod.wrenchEffect, pos);
                    }
                    else
                    {
                        DoEffect(aspect, sharedData, EffectID.FailedHit, pos);
                    }

                    return true;
                }
            }
            
            return false;
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
            int3 center,
            int wrenchTier)
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
                if (!properties.IsValid || !properties.Has(placeableHash)) continue;

                bool hasInventory = lookupData.containedObjectsBufferLookup.HasBuffer(entity);
                bool hasMounted = mountedCDLookup.HasComponent(entity);

                if (hasMounted || hasInventory)
                {
                    targets.Add(entity);
                }
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