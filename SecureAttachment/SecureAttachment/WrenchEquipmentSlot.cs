using System;
using System.Collections.Generic;
using CoreLib;
using CoreLib.Submodules.Equipment;
using CoreLib.Submodules.ModComponent;
using CoreLib.Submodules.ModEntity;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;

using Il2CppInterop.Runtime.Attributes;

namespace SecureAttachment
{
    public class WrenchEquipmentSlot : PlaceObjectSlot, IModEquipmentSlot
    {
        private int size = 0;

        public const string WrenchObjectType = "SecureAttachment:Wrench";
        
        public WrenchEquipmentSlot(IntPtr ptr) : base(ptr) { }

        public override EquipmentSlotType slotType => EquipmentSlotModule.GetEquipmentSlotType<WrenchEquipmentSlot>();


        public override void OnEquip(PlayerController player)
        {
            this.CallBase<PlaceObjectSlot, Action<PlayerController>>(nameof(OnEquip), player);
            PlacementHandler.SetAllowPlacingAnywhere(true);
        }

        public override void OnUnequip(PlayerController player)
        {
            this.CallBase<PlaceObjectSlot, Action<PlayerController>>(nameof(OnUnequip), player);
             PlacementHandler.SetAllowPlacingAnywhere(false);
        }

        public override void HandleInput(bool interactPressed, bool interactReleased, bool secondInteractPressed, bool secondInteractReleased, bool interactIsHeldDown,
            bool secondInteractIsHeldDown)
        {
            if (interactPressed)
            {
                var pos = placementHandler.bestPositionToPlaceAt;
                WrenchCD wrenchCd = ComponentModule.GetPugComponentData<WrenchCD>(objectData.objectID);
                
                List<Entity> targets = FindPotentialTargets(pos, wrenchCd.wrenchTier);
                EntityManager entityManager = world.EntityManager;
                
                foreach (Entity target in targets)
                {
                    ObjectDataCD objectDataCd = entityManager.GetComponentData<ObjectDataCD>(target);
                    MountedCD mountedCd = default;
                    if (entityManager.HasModComponent<MountedCD>(target))
                        mountedCd = entityManager.GetModComponentData<MountedCD>(target);

                    Translation translation = entityManager.GetComponentData<Translation>(target);
                    int2 objectPos = translation.Value.RoundToInt2();

                    ObjectInfo objectInfo = PugDatabase.GetObjectInfo(objectDataCd.objectID, objectDataCd.variation);
                    Vector2Int prefabTileSize = objectInfo.prefabTileSize;

                    Vector3Int worldPos = EntityMonoBehaviour.ToWorldFromRender(pos);
                    for (int x = 0; x < prefabTileSize.x; x++)
                    {
                        for (int y = 0; y < prefabTileSize.y; y++)
                        {
                            int2 testPos = objectPos + new int2(x, y);
                            if (math.all(testPos == worldPos.ToInt2()))
                            {
                                AttackWithItem();

                                if (mountedCd.wrenchTier <= wrenchCd.wrenchTier)
                                {
                                    slotOwner.playerCommandSystem.DestroyEntity(target, slotOwner.entity);
                                    AudioManager.SfxMono(SecureAttachmentPlugin.wrenchSfx, 1, 1, 0, true);
                                }
                                else
                                {
                                    AudioManager.SfxMono(SfxID.clunk, 1, 1, 0, true);
                                }

                                return;
                            }
                        }
                    }
                    
                }
            }
        }
        
        public BrushRect GetExtents()
        {
            int width = size;
            int height = size;
            
            int xOffset = (int)MathF.Floor(width / 2f);
            int yOffset = (int)MathF.Floor(height / 2f);

            return new BrushRect(xOffset, yOffset, width, height);
        }


        [HideFromIl2Cpp]
        private List<Entity> FindPotentialTargets(Vector3Int center, int wrenchTier)
        {
            CollisionWorld collisionWorld = PhysicsManager.GetCollisionWorld();
            PhysicsManager physicsManager = Manager.physics;
            BrushRect extents = GetExtents();

            float offset = size % 2 == 0 ? 0 : 0.5f;
            float3 worldPos = EntityMonoBehaviour.ToWorldFromRender(center).ToFloat3() + new float3(offset, 0, offset);

            float3 rectSize = new float3(extents.width + 1, 1f, extents.height + 1);

            PhysicsCollider collider = physicsManager.GetBoxCollider(new float3(0, 0, 0), rectSize, 0xffffffff);
            ColliderCastInput input = PhysicsManager.GetColliderCastInput(worldPos, worldPos, collider);

            NativeList<ColliderCastHit> results = new NativeList<ColliderCastHit>(Allocator.Temp);

            bool res = collisionWorld.CastCollider(input, ref results);
            List<Entity> targets = new List<Entity>(results.Length);
            if (!res) return targets;

            // ReSharper disable once ForCanBeConvertedToForeach
            for (int i = 0; i < results.Length; i++)
            {
                ColliderCastHit castHit = results[i];
                Entity entity = castHit.Entity;

                bool hasComponent =  world.EntityManager.HasComponent<PlaceableObjectCD>(entity);
                if (!hasComponent) continue;

                bool hasInventory =  world.EntityManager.HasComponent<InventoryCD>(entity);
                bool hasMounted = world.EntityManager.HasModComponent<MountedCD>(entity);

                if (hasMounted || hasInventory)
                {
                    targets.Add(entity);
                }
                
            }

            return targets;
        }

        public ObjectType GetSlotObjectType()
        {
            return EntityModule.GetObjectType(WrenchObjectType);
        }
        
        private ContainedObjectsBuffer AsBuffer(ObjectDataCD objectDataCd)
        {
            return new ContainedObjectsBuffer()
            {
                objectData = objectDataCd
            };
        }

        public void UpdateSlotVisuals(PlayerController controller)
        {
            ObjectDataCD objectDataCd = controller.GetHeldObject();
            ObjectInfo objectInfo = PugDatabase.GetObjectInfo(objectDataCd.objectID, objectDataCd.variation);

            ContainedObjectsBuffer objectsBuffer = AsBuffer(objectDataCd);
        
            controller.ActivateCarryableItemSpriteAndSkin(
                controller.carryablePlaceItemSprite,
                controller.carryablePlaceItemPugSprite,
                controller.carryableSwingItemSkinSkin,
                objectInfo, 
                objectsBuffer);
            controller.carryablePlaceItemSprite.sprite = objectInfo.smallIcon;
            controller.carryablePlaceItemColorReplacer.UpdateColorReplacerFromObjectData(objectsBuffer);
        }
    }
}