using System;
using PlacementPlus.Util;
using PugTilemap;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Math = System.Math;

namespace PlacementPlus;

public static class BrushExtension
{
    public static int size = 0;
    public static int currentRotation = 0;
    public static bool forceRotation = true;
    public static bool replaceTiles = false;

    public static BrushMode mode = BrushMode.SQUARE;

    public static bool brushChanged;

    private static PugDatabase.DatabaseBankCD databaseBlob;

    public static void ChangeRotation(int polarity)
    {
        currentRotation += polarity;
        if (currentRotation >= 4)
        {
            currentRotation = 0;
        }

        brushChanged = true;
    }

    public static void ChangeSize(int polarity)
    {
        size += polarity;
        size = Math.Clamp(size, 0, PlacementPlusPlugin.maxSize.Value - 1);

        brushChanged = true;
    }

    public static void ToggleMode()
    {
        int newMode = (int) mode + 1;
        if (newMode >= (int) BrushMode.MAX)
        {
            newMode = (int) BrushMode.NONE;
        }

        mode = (BrushMode) newMode;
        brushChanged = true;
    }

    public static BrushRect GetExtents(bool withRotation)
    {
        int width;
        int height;

        if (withRotation && !mode.IsSquare())
        {
            float angle = currentRotation * Mathf.PI / 2f;
            if (mode.IsVertical())
            {
                angle += Mathf.PI / 2f;
            }

            width = (int) MathF.Abs(MathF.Cos(angle)) * size;
            height = (int) MathF.Abs(MathF.Sin(angle)) * size;
        }
        else
        {
            width = mode.IsHorizontal() ? size : 0;
            height = mode.IsVertical() ? size : 0;
        }


        int xOffset = (int) MathF.Floor(width / 2f);
        int yOffset = (int) MathF.Floor(height / 2f);

        return new BrushRect(xOffset, yOffset, width, height);
    }

    public static bool IsItemValid(ObjectInfo info)
    {
        if (info == null) return false;
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

        if (PlacementPlusPlugin.defaultExclude.Contains(info.objectID)) return false;
        if (PlacementPlusPlugin.userExclude.Contains(info.objectID)) return false;

        return true;
    }

    internal static void PlayEffects(PlaceObjectSlot slot, Vector3Int initialPos, ObjectInfo itemInfo)
    {
        PlayerController pc = slot.slotOwner;
        pc.PlaceObject(initialPos);
        AudioManager.SfxFollowTransform(SfxID.shoop, pc.transform, 1, 1, 0.1f);

        EffectEventCD effect = new EffectEventCD
        {
            position1 = initialPos.ToFloat3(),
            effectID = EffectID.PlaceObject
        };

        if (itemInfo.tileType != TileType.none)
        {
            effect.effectID = EffectID.PlaceTile;
            effect.tileInfo = new TileInfo
            {
                tileset = itemInfo.tileset,
                tileType = itemInfo.tileType
            };
        }

        EntityUtility.PlayEffectEventClient(effect);
    }

    internal static void PlaceGrid(PlaceObjectSlot slot, Vector3Int center, ObjectDataCD item, ObjectInfo itemInfo)
    {
        slot.StartCooldownForItem(slot.SLOT_COOLDOWN);
        PlayerController pc = slot.slotOwner;
        int consumeAmount = 0;

        bool directionByVariation = PugDatabase.HasComponent<DirectionBasedOnVariationCD>(item);

        BrushRect extents = GetExtents(directionByVariation);
        int conditionValue = EntityUtility.GetConditionValue(ConditionID.ChanceToGainRarePlant, pc.entity, slot.world);

        for (int x = extents.minX; x <= extents.maxX; x++)
        {
            for (int y = extents.minY; y <= extents.maxY; y++)
            {
                Vector3Int pos = center + new Vector3Int(x, 0, y);
                ObjectDataCD data = pc.GetHeldObject();

                if (slot.placementHandler.CanPlaceObjectAtPosition(pos, 1, 1) <= 0) continue;
                if (!pc.CanConsumeEntityInSlot(slot, consumeAmount + 1)) continue;

                consumeAmount++;
                int variation = -1;
                if (PugDatabase.HasComponent<TileCD>(itemInfo.objectID))
                {
                    int2 position = new int2(pos.x, pos.z);

                    pc.pugMapSystem.RemoveTileOverride(position, TileType.debris);
                    pc.pugMapSystem.RemoveTileOverride(position, TileType.debris2);
                    pc.pugMapSystem.RemoveTileOverride(position, TileType.smallGrass);
                    pc.pugMapSystem.RemoveTileOverride(position, TileType.smallStones);

                    pc.pugMapSystem.AddTileOverride(position, itemInfo.tileset, itemInfo.tileType);
                    pc.playerCommandSystem.AddTile(position, itemInfo.tileset, itemInfo.tileType);
                }
                else
                {
                    if (PugDatabase.HasComponent<SeedCD>(item))
                    {
                        SeedCD seedCd = PugDatabase.GetComponent<SeedCD>(item);
                        if (seedCd.rarePlantVariation > 0 && conditionValue > 0)
                        {
                            if (conditionValue / 100f > PugRandom.GetRng().NextFloat())
                            {
                                variation = seedCd.rareSeedVariation;
                            }
                        }
                    }
                    else if (directionByVariation)
                    {
                        variation = currentRotation;
                    }

                    ObjectDataCD newObj = data;
                    newObj.variation = variation > 0 ? variation : 0;
                    float3 targetPos = pos.ToFloat3();

                    pc.instantiatePrefabsSystem.PrespawnEntity(newObj, targetPos);
                    pc.playerCommandSystem.CreateEntity(data.objectID, targetPos, newObj.variation);
                }
            }
        }

        if (consumeAmount > 0)
        {
            pc.playerInventoryHandler.Consume(pc.equippedSlotIndex, consumeAmount, true);
            pc.SetEquipmentSlotToNonUsableIfEmptySlot(slot);
        }
    }

    internal static void PaintGrid(PaintToolSlot slot, PlacementHandlerPainting handler)
    {
        PlayerController pc = slot.slotOwner;
        ObjectDataCD item = slot.objectReference;

        slot.StartCooldownForItem(slot.SLOT_COOLDOWN);

        PaintToolCD paintTool = PugDatabase.GetComponent<PaintToolCD>(item);
        int tileset = (int) slot.PaintIndexToTileset(paintTool.paintIndex);

        Vector3Int initialPos = slot.placementHandler.bestPositionToPlaceAt;
        Vector3Int worldPos = new Vector3Int(pc.pugMapPosX, 0, pc.pugMapPosZ);
        handler.tilesChecked.Clear();

        bool anySuccess = false;
        int effectCount = 0;

        BrushRect extents = GetExtents(false);

        int width = extents.width + 1;
        int height = extents.height + 1;

        float effectChance = 5 / (float) (width * height);

        for (int x = extents.minX; x <= extents.maxX; x++)
        {
            for (int y = extents.minY; y <= extents.maxY; y++)
            {
                Vector3Int pos = worldPos + initialPos + new Vector3Int(x, 0, y);
                if (handler.CanPlaceObjectAtPosition(pos, 1, 1) <= 0) continue;

                TileInfo tileInfo = handler.tileToPaint;
                if (tileInfo.tileType == TileType.none) continue;

                ObjectInfo tileItem = PugDatabase.TryGetTileItemInfo(tileInfo.tileType, tileset);
                if (tileItem == null) continue;

                int2 position = new int2(pos.x, pos.z);

                pc.pugMapSystem.RemoveTileOverride(position, tileInfo.tileType);
                pc.pugMapSystem.AddTileOverride(position, tileset, tileInfo.tileType);
                pc.playerCommandSystem.AddTile(position, tileset, tileInfo.tileType);

                anySuccess = true;
                if (PugRandom.GetRng().NextFloat() < effectChance && effectCount < 5)
                {
                    slot.PlayEffect(paintTool.paintIndex, pos);
                    effectCount++;
                }
            }
        }

        if (anySuccess)
        {
            pc.PlaceObject(initialPos);
        }
    }

/*
    internal static void DigGrid(ShovelSlot slot, Vector3Int center, PlacementHandlerDigging placementHandler)
    {
        slot.StartCooldownForItem(ShovelSlot.DIG_COOLDOWN);
        PlayerController pc = slot.slotOwner;
        int addAmount = 0;

        pc.EnterState(pc.sDig);

        ObjectDataCD dt = new ObjectDataCD()
        {
            objectID = (ObjectID)33011
        };


        BrushRect extents = GetExtents(false);
        for (int x = extents.minX; x <= extents.maxX; x++)
        {
            for (int y = extents.minY; y <= extents.maxY; y++)
            {
                Vector3Int pos = center + new Vector3Int(x, 0, y);
                ObjectDataCD shovel = pc.GetHeldObject();

                if (placementHandler.CanPlaceObjectAtPosition(pos, 1, 1) <= 0) continue;
                if (placementHandler.diggableObjects.Count <= 0) continue;
                
                PlacementHandlerDigging.DiggableEntityAndInfo info = placementHandler.diggableObjects._items[0];
                TileType type = info.diggableObjectInfo.tileType;

                if (type == TileType.ground ||
                    type == TileType.dugUpGround ||
                    type == TileType.wateredGround)
                {
                    DigUpAtPosition(slot, pos, placementHandler);
                }
                else
                {
                    if (slot.world.EntityManager.Exists(info.diggableEntity))
                    {
                        if (EntityUtility.HasComponentData(info.diggableEntity, slot.world, ComponentType.ReadOnly<DestructibleObjectCD>()))
                        {
                            pc.playerCommandSystem.DropDestructible(info.diggableEntity, pc.entity);
                        }
                        else
                        {
                            pc.IncreaseSkillIfEntityIsPlant(info.diggableEntity, true);
                            pc.playerCommandSystem.DestroyEntity(info.diggableEntity, pc.entity);
                        }
                    }
                    else
                    {
                        pc.DigUpTile(info.diggableObjectInfo.tileType, info.diggableObjectInfo.tileset, pos);
                    }
                }
                
                pc.ReduceDurabilityOfHeldEquipment();
                pc.DealCritterDamageAtTile(pos, false, false);
            }
        }
    }

    internal static void DigUpAtPosition(ShovelSlot slot, Vector3Int position, PlacementHandlerDigging placementHandler)
    {
        PlayerController pc = slot.slotOwner;

        int digging = EntityUtility.GetConditionEffectValue(ConditionEffect.Digging, pc.entity, slot.world);

        /*
        CollisionWorld world = PhysicsManager.GetCollisionWorld();
        PhysicsManager physicsManager = Manager.physics;

        float3 pos = position.ToFloat3();

        PhysicsCollider collider = physicsManager.GetSphereCollider(pos, 1, 0);
        ColliderCastInput input = PhysicsManager.GetColliderCastInput(pos, pos, collider);
        
        
        
        world.CastCollider(input, )*/ /*


        foreach (PlacementHandlerDigging.DiggableEntityAndInfo info in placementHandler.diggableObjects)
        {
            ComponentType tileType = ComponentType.ReadWrite<TileCD>();


            if (EntityUtility.HasComponentData(info.diggableEntity, slot.world, tileType))
            {
                TileCD tileCd = EntityUtility.GetComponentData<TileCD>(info.diggableEntity, slot.world);

                if (tileCd.tileType == TileType.ground)
                {
                    var cond = new NativeArray<SummarizedConditionEffectsBuffer>(0, Allocator.Temp);
                    HealthCD healthCd = EntityUtility.GetComponentData<HealthCD>(info.diggableEntity, slot.world);
                    
                    pc.GetTileDamageValues(info.diggableEntity, cond, digging, out float normHealth, out int damageDone, out int damageDoneBeforeReduction, false, true);
                    
                    //Do something about pc.playerCommandSystem.DealDamageToEntity()
                    pc.playerCommandSystem.DealDamageToEntity(info.diggableEntity, 
                        new NativeArray<SummarizedConditionEffectsBuffer>(0, Allocator.Temp), 
                        digging, 
                        false, 
                        pc.entity,
                        info.pos.ToFloat3(), 
                        out int _, 
                        out int _, 
                        out bool _, 
                        out bool _, 
                        out bool _, 
                        out bool _);
                }
                
            }
        }

    }*/
    public static bool HandleDirectionLogic(PlaceObjectSlot slot, Vector3Int initialPos, Vector3Int worldPos)
    {
        PlayerController pc = slot.slotOwner;
        ObjectDataCD item = pc.GetHeldObject();

        DirectionBasedOnVariationCD variationCd = PugDatabase.GetComponent<DirectionBasedOnVariationCD>(item);
        if (!variationCd.alignWithNearbyAffectorsWhenPlaced)
        {
            PlayEffects(slot, initialPos, slot.placementHandler.infoAboutObjectToPlace);
            slot.StartCooldownForItem(slot.SLOT_COOLDOWN);

            Vector3Int pos = worldPos + initialPos;

            if (slot.placementHandler.CanPlaceObjectAtPosition(pos, 1, 1) <= 0) return false;
            if (!pc.CanConsumeEntityInSlot(slot, 1)) return false;

            ObjectDataCD newObj = item;
            newObj.variation = currentRotation;
            float3 targetPos = pos.ToFloat3();

            pc.instantiatePrefabsSystem.PrespawnEntity(newObj, targetPos);
            pc.playerCommandSystem.CreateEntity(newObj.objectID, targetPos, newObj.variation);

            pc.playerInventoryHandler.Consume(pc.equippedSlotIndex, 1, true);
            pc.SetEquipmentSlotToNonUsableIfEmptySlot(slot);

            return false;
        }

        return true;
    }

    public static bool HandleReplaceLogic(PlaceObjectSlot slot, Vector3Int initialPos, Vector3Int worldPos)
    {
        PlayerController pc = slot.slotOwner;
        ObjectDataCD item = pc.GetHeldObject();
        Vector3Int pos = worldPos + initialPos;

        TileCD itemTile = PugDatabase.GetComponent<TileCD>(item);

        if (databaseBlob == null)
        {
            EntityQuery query = slot.world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<PugDatabase.DatabaseBankCD>());
            databaseBlob = query.GetSingleton<PugDatabase.DatabaseBankCD>();
        }

        int2 position = pos.ToInt2();
        if (Manager.multiMap.GetTileTypeAt(position, itemTile.tileType, out TileInfo tile))
        {
            if (tile.tileset != itemTile.tileset)
            {
                ObjectID objectID = PugDatabase.GetObjectID(tile.tileset, tile.tileType, databaseBlob.databaseBankBlob);
                if (objectID != ObjectID.None)
                {
                    //ObjectInfo info = PugDatabase.GetObjectInfo(objectID);
                    /*DamageReductionCD damageReduction = PugDatabase.GetComponent<DamageReductionCD>(new ObjectDataCD()
                    {
                        objectID = objectID,
                        amount = 1,
                        variation = 0
                    });
                    if (damageReduction.reduction < 100000)
                    {*/
                        pc.pugMapSystem.RemoveTileOverride(position, tile.tileType);
                        pc.pugMapSystem.AddTileOverride(position, itemTile.tileset, itemTile.tileType);
                        pc.playerCommandSystem.AddTile(position, itemTile.tileset, itemTile.tileType);
                       
                        pc.playerInventoryHandler.CreateItem(0, objectID, 1, pc.WorldPosition, 0);
                        pc.playerInventoryHandler.Consume(pc.equippedSlotIndex, 1, true);
                        pc.SetEquipmentSlotToNonUsableIfEmptySlot(slot);
                        
                        return false;
                    //}
                }
            }
        }

        return true;
    }
}