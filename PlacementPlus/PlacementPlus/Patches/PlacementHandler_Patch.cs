using HarmonyLib;
using PugTilemap;
using UnityEngine;

namespace PlacementPlus;

[HarmonyPatch]
public static class PlacementHandler_Patch
{

    [HarmonyPatch(typeof(PlacementHandler), nameof(PlacementHandler.UpdatePlaceIcon))]
    [HarmonyPostfix]
    public static void UpdatePlaceIcon(PlacementHandler __instance, bool immediate)
    {
        ObjectDataCD item = __instance.slotOwner.GetHeldObject();
        Vector2Int size = __instance.GetCurrentSize();

        if (BrushExtension.size == 0 || size.x != 1 || size.y != 1)
        {
            PlacementHandler.SetAllowPlacingAnywhere(false);

            if (PugDatabase.HasComponent<TileCD>(item) && BrushExtension.replaceTiles)
            {
                if (HandleTileReplace(__instance, item)) return;
            }

            return;
        }

        ObjectInfo itemInfo = __instance.infoAboutObjectToPlace;
        PlacementHandlerPainting painting = __instance.TryCast<PlacementHandlerPainting>();
        PlacementHandlerDigging digging = __instance.TryCast<PlacementHandlerDigging>();

        if (painting != null || digging != null || BrushExtension.IsItemValid(itemInfo))
        {
            BrushRect extents = BrushExtension.GetExtents();
            __instance.placeableIcon.SetPosition(__instance.bestPositionToPlaceAt - extents.offset, immediate || BrushExtension.brushChanged);
            BrushExtension.brushChanged = false;

            int newWidth = extents.width + 1;
            int newHeight = extents.height + 1;

            __instance.placeableIcon.SetSize(newWidth, newHeight);

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

    /*[HarmonyPatch(typeof(PlacementHandler), nameof(PlacementHandler.GetVelocityAffectorAlignmentVariation))]
    [HarmonyPostfix]
    public static void GetObjectVariation(ref int __result)
    {
        if (BrushExtension.forceRotation)
        {
            __result = BrushExtension.currentRotation;
        }
    }*/
}