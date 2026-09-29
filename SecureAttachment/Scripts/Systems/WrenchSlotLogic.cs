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

            var targets = FindTargetsIn(sharedData, lookupData, pos, toolSize);
            if (!targets.IsCreated) return false;
            
            int successCount = 0;
            int failureCount = 0;

            for (int i = 0; i < targets.Length; i++)
            {
                var target = targets[i];

                var result = MountEntity(aspect, sharedData, lookupData, wrenchCd, wrenchState, target);

                if (result == ActionResult.Success)
                    successCount++;
                else if (result == ActionResult.Failure)
                    failureCount++;
            }

            targets.Dispose();

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

        private ActionResult MountEntity(
            EquipmentUpdateAspect aspect, 
            EquipmentUpdateSharedData sharedData,
            LookupEquipmentUpdateData lookupData, 
            WrenchCD wrench,
            WrenchStateCD wrenchState,
            Entity target
        ) {
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

        private NativeList<Entity> FindTargetsIn(
            EquipmentUpdateSharedData sharedData,
            LookupEquipmentUpdateData lookupData,
            int3 pos,
            int2 toolSize
        )
        {
            // Calculate the total bounding box of grid area
            float3 minCorner = pos;
            float3 maxCorner = pos + new int3(toolSize.x - 1, 0, toolSize.y - 1);
            
            float3 center = (minCorner + maxCorner) * 0.5f;
            float3 halfExtents = ((maxCorner - minCorner) + new float3(1f, 1f, 1f)) * 0.5f;
            
            NativeList<DistanceHit> hits = new NativeList<DistanceHit>(Allocator.Temp);
            CollisionFilter filter = CollisionFilter.Default;

            sharedData.physicsWorldHistory.GetCollisionWorldFromTick(
                sharedData.currentTick, 1U,
                ref sharedData.physicsWorld,
                out CollisionWorld collisionWorld);

            bool hasHits =
                collisionWorld.OverlapBox(center, quaternion.identity, halfExtents, ref hits, filter);

            if (!hasHits)
            {
                hits.Dispose();
                return default;
            }

            // 3. Collect and map unique entities to their rounded grid positions
            // A single entity might span across multiple cells, but the map ensures 1:1 lookups
            var results = new NativeList<Entity>(hits.Length, Allocator.Temp);

            // ReSharper disable once ForCanBeConvertedToForeach
            for (int i = 0; i < hits.Length; i++)
            {
                Entity entity = hits[i].Entity;
                
                if (!lookupData.objectPropertiesLookup.HasComponent(entity)) continue;
                if (!lookupData.localTransformLookup.HasComponent(entity)) continue;

                var properties = lookupData.objectPropertiesLookup[entity];
                if (!properties.IsValid || !properties.Has(PropertyID.PlaceableObject.placeableObject)) continue;

                bool hasMounted = _mountedLookup.HasComponent(entity);
                if (!hasMounted) continue;
                
                LocalTransform transform = lookupData.localTransformLookup[entity];
                int2 objectPos = transform.Position.RoundToInt2();
                if (objectPos.x < minCorner.x || objectPos.x > maxCorner.x) continue;
                if (objectPos.y < minCorner.z || objectPos.y > maxCorner.z) continue;
                

                // Overwrite or add the entity to its grid position coordinate
                results.Add(entity);
            }

            hits.Dispose();
            return results;
        }

        public int CanPlaceObjectAtPosition(Entity placementPrefab, int3 posToPlaceAt, int width, int height, NativeHashMap<int3, bool> tilesChecked,
            ref NativeList<PlacementHandler.EntityAndInfoFromPlacement> diggableEntityAndInfos, in EquipmentUpdateAspect equipmentUpdateAspect, in EquipmentUpdateSharedData equipmentUpdateSharedData,
            in LookupEquipmentUpdateData equipmentUpdateLookupData)
        {   
            return width * height;
        }
    }
}