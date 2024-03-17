using HarmonyLib;
using PlacementPlus.Access;
using PugTilemap;
using UnityEngine;

namespace PlacementPlus
{
    [HarmonyPatch]
    public static class PlacementHandler_Patch
    {
        [HarmonyPatch(typeof(PlacementHandler), nameof(PlacementHandler.UpdatePlaceIcon))]
        [HarmonyPostfix]
        public static void UpdatePlaceIcon(PlacementHandler __instance, bool immediate)
        {
            ObjectDataCD item = __instance.GetSlotOwner_Public().GetHeldObject();
            Vector2Int size = __instance.GetCurrentSize_Public();

            if (BrushExtension.size == 0 ||
                BrushExtension.mode == BrushMode.NONE ||
                size.x != 1 || 
                size.y != 1)
            {
                PlacementHandler.SetAllowPlacingAnywhere(false);

                if (PugDatabase.HasComponent<TileCD>(item) && BrushExtension.replaceTiles)
                {
                    if (HandleTileReplace(__instance, item)) return;
                }

                return;
            }

            ObjectInfo itemInfo = __instance.GetInfoAboutObjectToPlace_Public();
            
            if (__instance is PlacementHandlerPainting || 
                (__instance is PlacementHandlerDigging && itemInfo.objectType == ObjectType.Shovel) ||
                __instance is PlacementHandlerRoofingTool || 
                BrushExtension.IsItemValid(itemInfo))
            {
                BrushRect extents = BrushExtension.GetExtents();
                __instance.placeableIcon.SetPosition(__instance.bestPositionToPlaceAt, immediate || BrushExtension.brushChanged);
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
            var tileLookup = Manager.multiMap.GetTileLayerLookup();

            if (tileLookup.TryGetTileInfo(initialPos.ToInt2(), itemTileCD.tileType, out TileInfo tile))
            {
                if (tile.tileset != itemTileCD.tileset)
                {
                    PlacementHandler.SetAllowPlacingAnywhere(true);
                    return true;
                }
            }

            return false;
        }

        [HarmonyPatch(typeof(PlacementHandlerPainting), "CanPlaceObjectAtPosition")]
        [HarmonyPostfix]
        private static void FixPaintBrushGridPaint(PlacementHandlerPainting __instance, int width, int height, ref int __result)
        {
            if (AccessExtensions.GetAllowPlacingAnywhere_Public() && BrushExtension.size > 0 && __result > 0)
            {
                __result = width * height;
            }
        }

        [HarmonyPatch(typeof(PlacementHandler), "FindPlaceablePositionFromMouseOrJoystick")]
        [HarmonyPatch(typeof(PlacementHandler), "FindPlaceablePositionFromOwnerDirection")]
        [HarmonyPrefix]
        private static void UseExtendedRange(PlacementHandler __instance, ref int width, ref int height)
        {
            ObjectInfo itemInfo = __instance.GetInfoAboutObjectToPlace_Public();

            if (__instance is PlacementHandlerPainting || 
                (__instance is PlacementHandlerDigging && itemInfo.objectType == ObjectType.Shovel) || 
                __instance is PlacementHandlerRoofingTool || 
                BrushExtension.IsItemValid(itemInfo))
            {
                BrushRect extents = BrushExtension.GetExtents();

                width = extents.width + 1;
                height = extents.height + 1;
            }
        }
    }
}