using System;
using HarmonyLib;
using PugTilemap;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace PlacementPlus;

[HarmonyPatch]
public static class PlacementHandler_Patch
{
    public static void SetStateWithArrow(PlacementIcon icon, bool state, int rotation)
    {
        if (rotation < 0 || rotation >= 4) throw new ArgumentException($"Rotation {rotation} is out of valid range!");

        if (state)
        {
            icon.SR.sprite = PlacementPlusPlugin.GetSprite(rotation);
        }
        else
        {
            icon.SR.sprite = PlacementPlusPlugin.GetSprite(rotation + 4);
        }
    }

    [HarmonyPatch(typeof(PlacementHandler), nameof(PlacementHandler.UpdatePlaceIcon))]
    [HarmonyPostfix]
    public static void UpdatePlaceIcon(PlacementHandler __instance, int width, int height, bool immediate)
    {
        ObjectDataCD item = __instance.slotOwner.GetHeldObject();

        if (BrushExtension.size == 0 || width != 1 || height != 1)
        {
            PlacementHandler.SetAllowPlacingAnywhere(false);

            if (PugDatabase.HasComponent<DirectionBasedOnVariationCD>(item) && BrushExtension.forceRotation)
            {
                SetStateWithArrow(__instance.placeableIcon, __instance.canPlaceObject, BrushExtension.currentRotation);
            }

            if (PugDatabase.HasComponent<TileCD>(item) && BrushExtension.replaceTiles)
            {
                if (HandleTileReplace(__instance, item)) return;
            }

            return;
        }

        ObjectInfo itemInfo = __instance.infoAboutObjectToPlace;
        PlacementHandlerPainting painting = __instance.TryCast<PlacementHandlerPainting>();
        PlacementHandlerDigging digging = __instance.TryCast<PlacementHandlerDigging>();
        bool directionByVariation = PugDatabase.HasComponent<DirectionBasedOnVariationCD>(item);

        if (painting != null || digging != null || BrushExtension.IsItemValid(itemInfo))
        {
            BrushRect extents = BrushExtension.GetExtents(directionByVariation);
            __instance.placeableIcon.SetPosition(__instance.bestPositionToPlaceAt - extents.offset, immediate || BrushExtension.brushChanged);
            BrushExtension.brushChanged = false;

            int newWidth = extents.width + 1;
            int newHeight = extents.height + 1;

            __instance.placeableIcon.SetSize(newWidth, newHeight);

            if (directionByVariation)
            {
                SetStateWithArrow(__instance.placeableIcon, true, BrushExtension.currentRotation);
            }

            PlacementHandler.SetAllowPlacingAnywhere(true);
            return;
        }

        PlacementHandler.SetAllowPlacingAnywhere(false);
    }

    private static bool HandleTileReplace(PlacementHandler __instance, ObjectDataCD item)
    {
        TileCD itemTileCD = PugDatabase.GetComponent<TileCD>(item);
        Vector3Int initialPos = __instance.bestPositionToPlaceAt;

        if (Manager.multiMap.GetTileTypeAt(initialPos.ToInt2(), itemTileCD.tileType, out TileInfo tile))
        {
            if (tile.tileset != itemTileCD.tileset)
            {
                PlacementHandler.SetAllowPlacingAnywhere(true);
                return true;
            }
        }

        return false;
    }

    [HarmonyPatch(typeof(PlacementHandler), nameof(PlacementHandler.GetVelocityAffectorAlignmentVariation))]
    [HarmonyPostfix]
    public static void GetObjectVariation(ref int __result)
    {
        if (BrushExtension.forceRotation)
        {
            __result = BrushExtension.currentRotation;
        }
    }
}