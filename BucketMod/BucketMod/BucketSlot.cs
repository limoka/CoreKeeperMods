using System;
using CoreLib;
using CoreLib.Submodules.CustomEntity;
using CoreLib.Submodules.Equipment;
using InventoryHandlerSystem;
using PugTilemap;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics.Authoring;
using UnityEngine;

namespace BucketMod;

public class BucketSlot : PlaceObjectSlot, IModEquipmentSlot
{
    public const string BucketObjectType = "BucketMod:Bucket";

    public static ObjectID bucketObjectID;
    public static ObjectID canObjectID;

    public static int2[] positions =
    {
        new int2(0, 0),
        new int2(-1, 0),
        new int2(0, 1),
        new int2(1, 0),
        new int2(0, -1),
        new int2(-1, 1),
        new int2(1, 1),
        new int2(1, -1),
        new int2(-1, 1)
    };

    public BucketSlot(IntPtr ptr) : base(ptr) { }

    public override EquipmentSlotType slotType => EquipmentSlotModule.GetEquipmentSlotType<BucketSlot>();

    public static void Init()
    {
        bucketObjectID = CustomEntityModule.GetObjectId("BucketMod:Bucket");
        canObjectID = CustomEntityModule.GetObjectId("BucketMod:PressureCan");
    }

    public static int GetCapacity(ObjectID objectID)
    {
        if (objectID == bucketObjectID)
        {
            return 1;
        }

        if (objectID == canObjectID)
        {
            return 5;
        }

        return 0;
    }

    public static int ParseVariation(int variation, out int count)
    {
        count = variation / 100;
        if (count == 0) return -1;
        
        int tileset = variation - 100 * count;
        return tileset;
    }

    public static int GetVariation(int tileset, int count)
    {
        if (count <= 0) return 0;
        return 100 * count + tileset;
    }

    public override void OnEquip(PlayerController player)
    {
        this.CallBase<PlaceObjectSlot, Action<PlayerController>>(nameof(OnEquip), player);

        ObjectDataCD heldObject = player.GetHeldObject();
        UpdateConditions(heldObject.variation == 0);
    }

    public override void PlaceItem() { }

    private void UpdateConditions(bool take)
    {
        placementHandler.canOnlyBePlaceOnObjects.Add(ObjectID.Water);
        placementHandler.canOnlyBePlaceOnObjects.Add(ObjectID.Lava);
        if (!take)
            placementHandler.canOnlyBePlaceOnObjects.Add(ObjectID.Pit);

        placementHandler.canPlaceOnWater = true;
        placementHandler.canPlaceOnLava = true;

        collidesWith = new PhysicsCategoryTags()
        {
            Category00 = true,
            Category02 = true,
            Category04 = true,
            Category06 = true
        };
    }

    public override void HandleInput(
        bool interactPressed,
        bool interactReleased,
        bool secondInteractPressed,
        bool secondInteractReleased,
        bool interactIsHeldDown,
        bool secondInteractIsHeldDown)
    {
        ObjectDataCD heldObject = slotOwner.GetHeldObject();
        UpdateConditions(heldObject.variation == 0);

        if (secondInteractPressed && heldObject.variation != 0)
        {
            ReleaseLiquid(heldObject);
        }else if (interactPressed)
        {
            FillContainer(heldObject);
        }
    }

    private void FillContainer(ObjectDataCD heldObject)
    {
        int tileset  = ParseVariation(heldObject.variation, out int count);
        int capacity = GetCapacity(heldObject.objectID);

        if (count >= capacity) return;
        int fillAmount = capacity - count;

        Vector3Int pos = placementHandler.bestPositionToPlaceAt;
        int2 position = new int2(pos.x, pos.z);

        if (placementHandler.CanPlaceObjectAtPosition(pos, 1, 1) > 0)
        {
            InventoryHandler inventory = slotOwner.playerInventoryHandler;
            int gotCount = 0;

            for (int i = 0; i < 9; i++)
            {
                int2 offset = positions[i];
                int2 newPos = position + offset;
                if (Manager.multiMap.GetTileTypeAt(newPos, TileType.water, out TileInfo tileInfo))
                {
                    if (tileset != -1 && tileInfo.tileset != tileset) continue;

                    slotOwner.pugMapSystem.RemoveTileOverride(newPos, TileType.water);
                    slotOwner.pugMapSystem.AddTileOverride(newPos, (int)Tileset.Dirt, TileType.pit);
                    slotOwner.playerCommandSystem.RemoveTile(newPos, tileInfo.tileset, TileType.water);
                    slotOwner.playerCommandSystem.AddTile(newPos, (int)Tileset.Dirt, TileType.pit);
                    gotCount++;
                    tileset = tileInfo.tileset;
                    
                    if (gotCount >= fillAmount)
                        break;
                }
            }

            if (gotCount > 0)
            {
                heldObject.variation = GetVariation(tileset, count + gotCount);
                inventory.DestroyObject(slotOwner.equippedSlotIndex, heldObject.objectID);
                inventory.CreateItem(slotOwner.equippedSlotIndex, heldObject.objectID, 1, slotOwner.WorldPosition, heldObject.variation);
                inventory.SetOverride(slotOwner.equippedSlotIndex, heldObject, 3);
            }
        }
    }

    private void ReleaseLiquid(ObjectDataCD heldObject)
    {
        int tileset = ParseVariation(heldObject.variation, out int count);
        if (tileset < 0)
        {
            InventoryHandler inventory = slotOwner.playerInventoryHandler;
            heldObject.variation = 0;
            inventory.DestroyObject(slotOwner.equippedSlotIndex, heldObject.objectID);
            inventory.CreateItem(slotOwner.equippedSlotIndex, heldObject.objectID, 1, slotOwner.WorldPosition, 0);
            inventory.SetOverride(slotOwner.equippedSlotIndex, heldObject, 3);
            return;
        }

        Vector3Int pos = placementHandler.bestPositionToPlaceAt;
        int2 position = new int2(pos.x, pos.z);

        if (placementHandler.CanPlaceObjectAtPosition(pos, 1, 1) > 0)
        {
            slotOwner.pugMapSystem.AddTileOverride(position, tileset, TileType.water);
            slotOwner.playerCommandSystem.AddTile(position, tileset, TileType.water);
            InventoryHandler inventory = slotOwner.playerInventoryHandler;

            int variation = GetVariation(tileset, count - 1);
            heldObject.variation = variation;

            inventory.DestroyObject(slotOwner.equippedSlotIndex, heldObject.objectID);
            inventory.CreateItem(slotOwner.equippedSlotIndex, heldObject.objectID, 1, slotOwner.WorldPosition, variation);
            inventory.SetOverride(slotOwner.equippedSlotIndex, heldObject, 3);
        }
    }

    public ObjectType GetSlotObjectType()
    {
        return CustomEntityModule.GetObjectType(BucketObjectType);
    }

    public void UpdateSlotVisuals(PlayerController controller)
    {
        ObjectDataCD objectDataCd = controller.GetHeldObject();
        ObjectInfo objectInfo = PugDatabase.GetObjectInfo(objectDataCd.objectID, objectDataCd.variation);
        
        controller.ActivateCarryableItemSpriteAndSkin(
            controller.carryablePlaceItemSprite,
            controller.carryableSwingItemSkinSkin,
            objectInfo);
        controller.carryablePlaceItemSprite.sprite = objectInfo.smallIcon;
        controller.carryablePlaceItemColorReplacer.UpdateColorReplacerFromObjectData(objectDataCd);
    }
}