using System;
using PlacementPlus.Util;
using PugTilemap;
using Unity.Mathematics;
using UnityEngine;

namespace PlacementPlus;

public static class BrushExtension
{
    public static int size = 0;
    public static int currentRotation = 0;
    public static bool forceRotation = true;

    public static BrushMode mode = BrushMode.SQUARE;

    public static bool brushChanged;

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
        int newMode = (int)mode + 1;
        if (newMode >= (int)BrushMode.MAX)
        {
            newMode = (int)BrushMode.HORIZONTAL;
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
        slot.StartCooldownForItem(slot.SLOT_COOLDOWN );
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
        int tileset = (int)slot.PaintIndexToTileset(paintTool.paintIndex);

        Vector3Int initialPos = slot.placementHandler.bestPositionToPlaceAt;
        Vector3Int worldPos = new Vector3Int(pc.pugMapPosX, 0, pc.pugMapPosZ);
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
}