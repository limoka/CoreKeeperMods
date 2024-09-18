using CoreLib.Util.Extensions;
using HarmonyLib;
using PlacementPlus.Components;
using PlacementPlus.Systems;
using PlayerEquipment;
using PugMod;
using PugProperties;
using PugTilemap;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace PlacementPlus
{
    [HarmonyPatch]
    public static class PlacementHandler_Patch
    {
        [HarmonyPatch(typeof(PlacementHandler), nameof(PlacementHandler.UpdatePlaceIcon))]
        [HarmonyPostfix]
        public static void UpdatePlaceIcon(
            PlacementHandler __instance,
            bool immediate,
            in PlacementCD placementCD,
            ObjectDataCD infoAboutObjectToPlace,
            Entity placementPrefab,
            ComponentLookup<DirectionCD> directionLookup,
            ComponentLookup<DirectionBasedOnVariationCD> directionBasedOnVariationLookup,
            ComponentLookup<ObjectPropertiesCD> objectPropertiesLookup,
            PugDatabase.DatabaseBankCD databaseBankCD
        )
        {
            var world = API.Client.World;
            var player = Manager.main.player;
            var state = world.EntityManager.GetComponentData<PlacementPlusState>(player.entity);

            ref var info = ref PugDatabase.GetEntityObjectInfo(infoAboutObjectToPlace.objectID, databaseBankCD.databaseBankBlob, infoAboutObjectToPlace.variation);

            if (state.size == 0 ||
                state.mode == BrushMode.NONE ||
                info.prefabTileSize.x != 1 ||
                info.prefabTileSize.y != 1)
            {

                /*if (PugDatabase.HasComponent<TileCD>(item) && BrushExtension.replaceTiles)
                {
                    //if (HandleTileReplace(__instance, item)) return;
                }*/

                return;
            }

            var equipmentSlot = world.EntityManager.GetComponentData<EquipmentSlotCD>(player.entity);

            var query = world.EntityManager.CreateEntityQuery(typeof(NetworkTime));
            var currentTick = query.GetSingleton<NetworkTime>().ServerTick;
            query.Dispose();
            
           /* if (__instance is PlacementHandlerPainting ||
                (__instance is PlacementHandlerDigging && itemInfo.objectType == ObjectType.Shovel) ||
                __instance is PlacementHandlerRoofingTool ||
                BrushExtension.IsItemValid(itemInfo))*/
           if ((equipmentSlot.slotType == EquipmentSlotType.PlaceObjectSlot && ObjectPlacementLogic.IsItemValid(ref info)) ||
               equipmentSlot.slotType == EquipmentSlotType.ShovelSlot)
            {
                BrushRect extents = state.GetExtents();
                
                Vector3Int vector3Int = new Vector3Int(placementCD.bestPositionToPlaceAt.x, placementCD.bestPositionToPlaceAt.y, placementCD.bestPositionToPlaceAt.z);
                vector3Int = EntityMonoBehaviour.ToRenderFromWorld(vector3Int);
                var diff = currentTick.TicksSince(state.changedOnTick);
                
                __instance.placeableIcon.SetPosition(vector3Int, immediate || Mathf.Abs(diff) < 5);

                int newWidth = extents.width + 1;
                int newHeight = extents.height + 1;

                __instance.placeableIcon.SetSize(newWidth, newHeight);

                return;
            }
        }

        /*private static bool HandleTileReplace(PlacementHandler __instance, ObjectDataCD item)
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
        }*/

        /* [HarmonyPatch(typeof(PlacementHandlerPainting), "CanPlaceObjectAtPosition")]
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
         }*/
    }
}