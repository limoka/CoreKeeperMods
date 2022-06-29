using System;
using HarmonyLib;
using PugTilemap;
using Unity.Mathematics;
using UnityEngine;

namespace PlacementPlus;

[Flags]
public enum RangeMode
{
    NONE,
    HORIZONTAL = 1,
    VERTICAL = 2,
    SQUARE = HORIZONTAL | VERTICAL,
    MAX
}

[HarmonyPatch]
public static class PlaceObjectSlot_Patch
{
    public static int radius = 0;
    public static RangeMode mode = RangeMode.SQUARE;
    public static bool radiusChanged;

    public static void ChangeRadius(int polarity)
    {
        radius += polarity;
        radius = Math.Clamp(radius, 0, 3);
        radiusChanged = true;
    }

    public static void ToggleMode()
    {
        int newMode = (int)mode + 1;
        if (newMode >= (int)RangeMode.MAX)
        {
            newMode = (int)RangeMode.HORIZONTAL;
        }

        mode = (RangeMode)newMode;
        radiusChanged = true;
    }

    public static Vector3Int GetExtents()
    {
        int width = (mode & RangeMode.HORIZONTAL) == RangeMode.HORIZONTAL ? radius : 0;
        int height = (mode & RangeMode.VERTICAL) == RangeMode.VERTICAL ? radius : 0;
        return new Vector3Int(width, 0, height);
    }

    public static bool IsItemValid(ObjectDataCD item, ObjectInfo info)
    {
        if (info == null) return false;
        if (info.objectType != ObjectType.PlaceablePrefab) return false;
        if (info.tileType != TileType.floor &&
            info.tileType != TileType.wall &&
            info.tileType != TileType.bridge &&
            info.tileType != TileType.ground &&
            info.tileType != TileType.none) return false;

        if (PlacementPlusPlugin.defaultExclude.Contains(info.objectID)) return false;
        if (PlacementPlusPlugin.userExclude.Contains(info.objectID)) return false;

        return true;
    }

    private static bool CheckSlot(EquipmentSlot slot)
    {
        return slot.GetType() == typeof(PlaceObjectSlot) && slot.isActiveAndEnabled;
    }

    [HarmonyPatch(typeof(PlacementHandler), nameof(PlacementHandler.UpdatePlaceIcon))]
    [HarmonyPostfix]
    public static void UpdatePlaceIcon(PlacementHandler __instance, int width, int height, bool immediate)
    {
        if (radius == 0 || width != 1 || height != 1)
        {
            PlacementHandler.SetAllowPlacingAnywhere(false);
            return;
        }

        ObjectDataCD item = __instance.slotOwner.GetHeldObject();
        ObjectInfo itemInfo = PugDatabase.GetObjectInfo(item.objectID);
        PlacementHandlerPainting painting = __instance.TryCast<PlacementHandlerPainting>();

        if (painting != null || IsItemValid(item, itemInfo))
        {
            Vector3Int extents = GetExtents();
            __instance.placeableIcon.SetPosition(__instance.bestPositionToPlaceAt - extents, immediate || radiusChanged);

            int newWidth = 2 * extents.x + 1;
            int newHeight = 2 * extents.z + 1;

            __instance.placeableIcon.SetSize(newWidth, newHeight);
            radiusChanged = false;
            PlacementHandler.SetAllowPlacingAnywhere(true);
            return;
        }

        PlacementHandler.SetAllowPlacingAnywhere(false);
    }

    [HarmonyPatch(typeof(PlaceObjectSlot), nameof(PlaceObjectSlot.PlaceItem))]
    [HarmonyPrefix]
    public static bool OnPlace(PlaceObjectSlot __instance)
    {
        PlacementHandler.SetAllowPlacingAnywhere(false);
        if (radius == 0) return true;

        PlayerController pc = __instance.slotOwner;

        ObjectDataCD item = pc.GetHeldObject();
        ObjectInfo itemInfo = PugDatabase.GetObjectInfo(item.objectID);

        Vector3Int initialPos = __instance.placementHandler.bestPositionToPlaceAt;
        Vector3Int worldPos = new Vector3Int(pc.pugMapPosX, 0, pc.pugMapPosZ);

        if (!IsItemValid(item, itemInfo)) return true;

        pc.PlaceObject(initialPos);
        __instance.StartCooldownForItem(__instance.SLOT_COOLDOWN);
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

        int consumeAmount = 0;

        Vector3Int extents = GetExtents();
        int conditionValue = EntityUtility.GetConditionValue(ConditionID.ChanceToGainRarePlant, pc.entity, __instance.world);

        for (int x = -extents.x; x <= extents.x; x++)
        {
            for (int y = -extents.z; y <= extents.z; y++)
            {
                Vector3Int pos = worldPos + initialPos + new Vector3Int(x, 0, y);
                ObjectDataCD data = pc.GetHeldObject();

                if (__instance.placementHandler.CanPlaceObjectAtPosition(pos, 1, 1) <= 0) continue;
                if (!pc.CanConsumeEntityInSlot(__instance, consumeAmount + 1)) continue;

                consumeAmount++;
                int variation = -1;
                if (PugDatabase.HasComponent<TileCD>(item.objectID))
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
                        if (seedCd.rarePlantVariation > 0)
                        {
                            if (conditionValue > 0 && conditionValue / 100f > PugRandom.GetRng().NextFloat())
                            {
                                variation = seedCd.rarePlantVariation;
                            }
                        }
                    }

                    pc.playerCommandSystem.CreateEntity(data.objectID, (Vector3)pos, variation > 0 ? variation : 0);
                }
            }
        }

        if (consumeAmount > 0)
        {
            pc.playerInventoryHandler.Consume(pc.equippedSlotIndex, consumeAmount, true);
            pc.SetEquipmentSlotToNonUsableIfEmptySlot(__instance);
        }

        return false;
    }

    [HarmonyPatch(typeof(PaintToolSlot), nameof(PaintToolSlot.PlaceItem))]
    [HarmonyPrefix]
    public static bool OnPaint(PaintToolSlot __instance)
    {
        PlacementHandler.SetAllowPlacingAnywhere(false);
        if (radius == 0) return true;

        PlayerController pc = __instance.slotOwner;
        PlacementHandlerPainting handler = __instance.placementHandler.Cast<PlacementHandlerPainting>();
        bool entityExists = pc.world.EntityManager.Exists(handler.entityToPaint);
        if (entityExists) return true;

        ObjectDataCD item = __instance.objectReference;
        if (item.objectID <= 0) return true;
        if (!PugDatabase.HasComponent<PaintToolCD>(item)) return true;

        __instance.StartCooldownForItem(__instance.SLOT_COOLDOWN);

        PaintToolCD paintTool = PugDatabase.GetComponent<PaintToolCD>(item);
        int tileset = (int)__instance.PaintIndexToTileset(paintTool.paintIndex);

        Vector3Int initialPos = __instance.placementHandler.bestPositionToPlaceAt;
        Vector3Int worldPos = new Vector3Int(pc.pugMapPosX, 0, pc.pugMapPosZ);
        handler.tilesChecked.Clear();

        bool anySuccess = false;
        int effectCount = 0;
        int size = radius * 2 + 1;
        float effectChance = 5 / (float)(size * size);

        Vector3Int extents = GetExtents();

        for (int x = -extents.x; x <= extents.x; x++)
        {
            for (int y = -extents.z; y <= extents.z; y++)
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
                    __instance.PlayEffect(paintTool.paintIndex, pos);
                    effectCount++;
                }
            }
        }

        if (anySuccess)
        {
            pc.PlaceObject(initialPos);
        }

        return false;
    }
}