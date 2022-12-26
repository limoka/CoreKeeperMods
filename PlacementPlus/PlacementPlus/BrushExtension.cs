﻿using System;
using System.Collections.Generic;
using CoreLib.Util.Data;
using PlacementPlus.Util;
using PugTilemap;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using Math = System.Math;

namespace PlacementPlus;

public static class BrushExtension
{
    public static int size;
    public static int currentRotation;
    public static bool forceRotation = true;
    public static bool replaceTiles = false;

    public static BrushMode mode = BrushMode.SQUARE;

    public static bool brushChanged;

    #region GridSize

    internal static int GetMaxSize()
    {
        int maxSize = PlacementPlusPlugin.maxSize.Value - 1;
        PlayerController player = Manager.main.player;
        if (player == null) return maxSize;

        ObjectDataCD item = player.GetHeldObject();
        int damage = Extensions.GetShovelDamage(item);
        if (damage == 0) return maxSize;

        return GetShovelLevel(damage);
    }

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
        int maxSize = GetMaxSize();

        size += polarity;
        size = Math.Clamp(size, 0, maxSize);

        brushChanged = true;
    }

    public static void CheckSize()
    {
        int maxSize = GetMaxSize();
        if (size > maxSize)
        {
            size = maxSize;
            brushChanged = true;
        }
    }

    public static void ToggleMode()
    {
        int newMode = (int)mode + 1;
        if (newMode >= (int)BrushMode.MAX)
        {
            newMode = (int)BrushMode.NONE;
        }

        mode = (BrushMode)newMode;
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

            width = (int)MathF.Abs(MathF.Cos(angle)) * size;
            height = (int)MathF.Abs(MathF.Sin(angle)) * size;
        }
        else
        {
            width = mode.IsHorizontal() ? size : 0;
            height = mode.IsVertical() ? size : 0;
        }


        int xOffset = (int)MathF.Floor(width / 2f);
        int yOffset = (int)MathF.Floor(height / 2f);

        return new BrushRect(xOffset, yOffset, width, height);
    }

    #endregion

    #region Validation

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

    internal static bool IsItemValid(ObjectInfo info)
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

    internal static int GetBestShovelSlot(PlayerController pc)
    {
        int maxDamage = 0;
        int slot = 0;

        for (int i = 0; i < 10; i++)
        {
            ObjectDataCD objectDataCd = pc.GetInventorySlot(i);
            int damage = Extensions.GetShovelDamage(objectDataCd);

            if (damage > maxDamage)
            {
                maxDamage = damage;
                slot = i;
            }
        }

        return slot;
    }

    #endregion

    #region GridPlace

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
        int shovelSlot = GetBestShovelSlot(pc);
        bool usedShovel = false;

        BrushRect extents = GetExtents(directionByVariation);

        for (int x = extents.minX; x <= extents.maxX; x++)
        {
            for (int y = extents.minY; y <= extents.maxY; y++)
            {
                Vector3Int pos = center + new Vector3Int(x, 0, y);
                if (PlaceAt(slot, itemInfo, pos, consumeAmount, directionByVariation, ref usedShovel))
                {
                    consumeAmount++;
                }
            }
        }

        if (consumeAmount > 0)
        {
            pc.playerInventoryHandler.Consume(pc.equippedSlotIndex, consumeAmount, true);
            pc.SetEquipmentSlotToNonUsableIfEmptySlot(slot);
        }

        if (usedShovel)
        {
            pc.ReduceDurabilityOfEquipmentInSlot(shovelSlot);
        }
    }

    private static bool PlaceAt(PlaceObjectSlot slot, ObjectInfo itemInfo, Vector3Int pos, int consumeAmount, bool directionByVariation, ref bool usedShovel)
    {
        PlayerController pc = slot.slotOwner;
        ObjectDataCD item = pc.GetHeldObject();
        bool isTile = PugDatabase.HasComponent<TileCD>(itemInfo.objectID);
        int2 position = new int2(pos.x, pos.z);

        if (isTile && replaceTiles)
        {
            if (!pc.CanConsumeEntityInSlot(slot, consumeAmount + 1)) return false;

            bool success = !HandleReplaceLogic(slot, position, false);
            if (success) usedShovel = true;
            return success;
        }

        if (slot.placementHandler.CanPlaceObjectAtPosition(pos, 1, 1) <= 0) return false;
        if (!pc.CanConsumeEntityInSlot(slot, consumeAmount + 1)) return false;

        int variation = -1;

        if (isTile)
        {
            pc.pugMapSystem.RemoveTileOverride(position, TileType.debris);
            pc.pugMapSystem.RemoveTileOverride(position, TileType.debris2);
            pc.pugMapSystem.RemoveTileOverride(position, TileType.smallGrass);
            pc.pugMapSystem.RemoveTileOverride(position, TileType.smallStones);

            pc.pugMapSystem.AddTileOverride(position, itemInfo.tileset, itemInfo.tileType);
            pc.playerCommandSystem.AddTile(position, itemInfo.tileset, itemInfo.tileType);
            return true;
        }

        if (PugDatabase.HasComponent<SeedCD>(item))
        {
            int conditionValue = EntityUtility.GetConditionValue(ConditionID.ChanceToGainRarePlant, pc.entity, slot.world);
            SeedCD seedCd = PugDatabase.GetComponent<SeedCD>(item);

            if (seedCd.rarePlantVariation > 0 && conditionValue > 0 &&
                conditionValue / 100f > PugRandom.GetRng().NextFloat())
            {
                variation = seedCd.rareSeedVariation;
            }
        }
        else if (directionByVariation)
        {
            variation = currentRotation;
        }

        ObjectDataCD newObj = item;
        newObj.variation = variation > 0 ? variation : 0;
        float3 targetPos = pos.ToFloat3();

        pc.instantiatePrefabsSystem.PrespawnEntity(newObj, targetPos);
        pc.playerCommandSystem.CreateEntity(item.objectID, targetPos, newObj.variation);

        return true;
    }

    public static bool HandleDirectionLogic(PlaceObjectSlot slot, Vector3Int pos)
    {
        PlayerController pc = slot.slotOwner;
        ObjectDataCD item = pc.GetHeldObject();

        DirectionBasedOnVariationCD variationCd = PugDatabase.GetComponent<DirectionBasedOnVariationCD>(item);
        if (!variationCd.alignWithNearbyAffectorsWhenPlaced)
        {
            PlayEffects(slot, pos, slot.placementHandler.infoAboutObjectToPlace);
            slot.StartCooldownForItem(slot.SLOT_COOLDOWN);


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

    #endregion

    #region PaintGrid

    internal static void PaintGrid(PaintToolSlot slot, PlacementHandlerPainting handler)
    {
        PlayerController pc = slot.slotOwner;
        ObjectDataCD item = slot.objectReference;

        slot.StartCooldownForItem(slot.SLOT_COOLDOWN);

        PaintToolCD paintTool = PugDatabase.GetComponent<PaintToolCD>(item);
        int tileset = (int)slot.PaintIndexToTileset(paintTool.paintIndex);

        Vector3Int initialPos = slot.placementHandler.bestPositionToPlaceAt;
        handler.tilesChecked.Clear();

        bool anySuccess = false;
        int effectCount = 0;

        BrushRect extents = GetExtents(false);

        int width = extents.width + 1;
        int height = extents.height + 1;

        float effectChance = 5 / (float)(width * height);

        for (int x = extents.minX; x <= extents.maxX; x++)
        {
            for (int y = extents.minY; y <= extents.maxY; y++)
            {
                Vector3Int pos = initialPos + new Vector3Int(x, 0, y);
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

    #endregion

    #region DigGrid

    internal static void DigGrid(ShovelSlot slot, Vector3Int center, PlacementHandlerDigging placementHandler)
    {
        slot.StartCooldownForItem(ShovelSlot.DIG_COOLDOWN);
        PlayerController pc = slot.slotOwner;
        pc.EnterState(pc.sDig);

        BrushRect extents = GetExtents(false);
        var tileLookup = FindDamagedTiles(pc, center);
        NativeArray<SummarizedConditionEffectsBuffer> conditions = EntityUtility.GetConditionEffectValues(pc.entity, pc.world).ToNativeArray(Allocator.Temp);

        for (int x = extents.minX; x <= extents.maxX; x++)
        {
            for (int y = extents.minY; y <= extents.maxY; y++)
            {
                Vector3Int pos1 = center + new Vector3Int(x, 0, y);

                if (placementHandler.CanPlaceObjectAtPosition(pos1, 1, 1) <= 0) continue;
                if (placementHandler.diggableObjects.Count <= 0) continue;

                PlacementHandlerDigging.DiggableEntityAndInfo info = placementHandler.diggableObjects._items[0];
                TileType type = info.diggableObjectInfo.tileType;

                if (type == TileType.ground ||
                    type == TileType.dugUpGround ||
                    type == TileType.wateredGround)
                {
                    DigUpAtPosition(slot, pos1, tileLookup, conditions);
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
                        pc.DigUpTile(info.diggableObjectInfo.tileType, info.diggableObjectInfo.tileset, pos1);
                    }
                }

                pc.DealCritterDamageAtTile(pos1, false, false);
            }
        }

        conditions.Dispose();

        pc.ReduceDurabilityOfHeldEquipment();
    }

    private static Dictionary<int2, TileData> FindDamagedTiles(PlayerController pc, Vector3Int center)
    {
        World world = pc.world;
        CollisionWorld collisionWorld = PhysicsManager.GetCollisionWorld();
        PhysicsManager physicsManager = Manager.physics;
        BrushRect extents = GetExtents(false);

        float offset = size % 2 == 0 ? 0 : 0.5f;
        float3 worldPos = EntityMonoBehaviour.ToWorldFromRender(center).ToFloat3() + new float3(offset, 0, offset);

        float3 rectSize = new float3(extents.width + 1, 0.2f, extents.height + 1);

        PhysicsCollider collider = physicsManager.GetBoxCollider(new float3(0, -0.5f, 0), rectSize, PhysicsLayerID.Everything);
        ColliderCastInput input = PhysicsManager.GetColliderCastInput(worldPos, worldPos, collider);

        NativeList_Unboxed<ColliderCastHit> results = new NativeList_Unboxed<ColliderCastHit>(Allocator.Temp);

        bool res = collisionWorld.CastColliderB(input, ref results);
        Dictionary<int2, TileData> tileLookup = new Dictionary<int2, TileData>(results.Length);
        if (!res) return tileLookup;

        // ReSharper disable once ForCanBeConvertedToForeach
        for (int i = 0; i < results.Length; i++)
        {
            ColliderCastHit castHit = results[i];
            Entity entity = castHit._Entity_k__BackingField;

            bool hasComponent = world.EntityManager.HasComponent<TileCD>(entity);
            if (!hasComponent) continue;

            TileCD tileCd = world.EntityManager.GetComponentData<TileCD>(entity);

            Translation translation = world.EntityManager.GetComponentData<Translation>(entity);

            if (tileCd.tileType == TileType.ground)
            {
                int2 pos = translation.Value.xz.RoundToInt2();
                if (!tileLookup.ContainsKey(pos))
                    tileLookup.Add(pos, new TileData(entity, tileCd));
            }
        }

        return tileLookup;
    }

    internal static void DigUpAtPosition(ShovelSlot slot, Vector3Int position,
        Dictionary<int2, TileData> tileLookup,
        NativeArray<SummarizedConditionEffectsBuffer> conditions)
    {
        PlayerController pc = slot.slotOwner;
        int digging = conditions[(int)ConditionEffect.Digging].value;

        float normHealth;
        int damageDone;

        SinglePugMap multimap = Manager.multiMap;
        int2 origo = multimap.Origo;
        var posWorldSpace = position.ToInt2() + origo;

        if (tileLookup.ContainsKey(posWorldSpace))
        {
            TileData tileData = tileLookup[posWorldSpace];

            pc.GetTileDamageValues(tileData.entity, conditions, digging,
                out normHealth,
                out damageDone,
                out _,
                false, true);


            pc.playerCommandSystem.DealDamageToEntity(tileData.entity,
                conditions,
                digging,
                false,
                pc.entity,
                position.ToFloat3(),
                out int _,
                out int _,
                out bool _,
                out bool _,
                out bool _,
                out bool _,
                out bool _,
                true, false, true);

            pc.DoImmediateTileDamageEffects(
                tileData.tileCd.tileset,
                tileData.tileCd.tileType,
                position,
                normHealth,
                damageDone,
                tileData.entity);
            return;
        }


        int counts = multimap.tileLookup.CountValuesForKey(posWorldSpace);
        if (counts == 0) return;

        UnsafeParallelMultiHashMap<int2, TileInfo>.Enumerator results = multimap.tileLookup.GetValuesForKey(posWorldSpace);
        TileInfo targetTile = default;
        bool found = false;

        foreach (TileInfo tileInfo in results)
        {
            if (tileInfo.tileType == TileType.ground)
            {
                targetTile = tileInfo;
                found = true;
                break;
            }
        }

        if (!found) return;

        ObjectInfo objectInfo = PugDatabase.TryGetTileItemInfo(targetTile.tileType, targetTile.tileset);
        results.Dispose();

        var primaryEntity = PugDatabase.GetPrimaryPrefabEntity(objectInfo.objectID, pc.pugDatabase, objectInfo.variation);

        pc.GetTileDamageValues(primaryEntity,
            conditions,
            digging,
            out normHealth,
            out damageDone,
            out _,
            false, true);

        pc.playerCommandSystem.CreateTileDamage(position.ToInt2(), damageDone, pc.entity, true, true);
        pc.DoImmediateTileDamageEffects(targetTile.tileset, targetTile.tileType, position, normHealth, damageDone, primaryEntity);
    }

    #endregion

    #region TileReplace

    public static void ReduceDurabilityOfEquipmentInSlot(this PlayerController pc, int slotIndex)
    {
        EquipmentSlot slot = pc.GetEquipmentSlot(slotIndex);
        int toolConditionValue = EntityUtility.GetConditionValue(ConditionID.ToolDurabilityLastsLonger, pc.entity, pc.world);

        if (toolConditionValue / 100f > PugRandom.GetRng().NextFloat()) return;

        ObjectDataCD objectRef = slot.objectReference;

        if (!PugDatabase.HasComponent<DurabilityCD>(objectRef)) return;

        int newAmount = objectRef.amount - 1;
        if (newAmount < 0) newAmount = 0;

        if (newAmount > 0)
        {
            pc.playerInventoryHandler.SetAmount(slotIndex, objectRef.objectID, newAmount);
        }
        else if (objectRef.amount > 0)
        {
            pc.PlayEquipmentBreakSound();
            pc.playerInventoryHandler.SetAmount(slotIndex, objectRef.objectID, 0);
            pc.playerInventoryHandler.TryReplaceBrokenObject(slotIndex);
        }
    }

    public static bool HandleReplaceLogic(PlaceObjectSlot slot, int2 position, bool doConsumeItem)
    {
        PlayerController pc = slot.slotOwner;
        ObjectDataCD item = pc.GetHeldObject();

        int shovelSlot = GetBestShovelSlot(pc);

        TileCD itemTile = PugDatabase.GetComponent<TileCD>(item);

        if (Manager.multiMap.GetTileTypeAt(position, itemTile.tileType, out TileInfo tile))
        {
            if (tile.tileset == itemTile.tileset) return true;

            ObjectID objectID = PugDatabase.GetObjectID(tile.tileset, tile.tileType, pc.pugDatabase);
            if (objectID == ObjectID.None) return true;
            
            slot.StartCooldownForItem(slot.SLOT_COOLDOWN);

            pc.pugMapSystem.RemoveTileOverride(position, TileType.dugUpGround);
            pc.pugMapSystem.RemoveTileOverride(position, tile.tileType);
            pc.pugMapSystem.AddTileOverride(position, itemTile.tileset, itemTile.tileType);
            pc.playerCommandSystem.RemoveTile(position, tile.tileset, TileType.dugUpGround);
            pc.playerCommandSystem.AddTile(position, itemTile.tileset, itemTile.tileType);

            pc.playerInventoryHandler.CreateItem(0, objectID, 1, pc.WorldPosition);
            if (doConsumeItem)
            {
                pc.playerInventoryHandler.Consume(pc.equippedSlotIndex, 1, true);
                pc.SetEquipmentSlotToNonUsableIfEmptySlot(slot);
                pc.ReduceDurabilityOfEquipmentInSlot(shovelSlot);
            }

            return false;
        }

        return true;
    }

    #endregion
}