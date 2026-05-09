using CoreLib.Util.Extension;
using HarmonyLib;
using PlacementPlus.Components;
using PlacementPlus.Systems;
using PlayerEquipment;
using PugMod;
using Pug.Properties;
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
                return;
            }

            var equipmentSlot = world.EntityManager.GetComponentData<EquipmentSlotCD>(player.entity);

            var query = world.EntityManager.CreateEntityQuery(typeof(NetworkTime));
            var currentTick = query.GetSingleton<NetworkTime>().ServerTick;
            query.Dispose();
            
            if (!objectPropertiesLookup.TryGetComponent(placementPrefab, out ObjectPropertiesCD properties))
                return;

            if ((equipmentSlot.slotType != EquipmentSlotType.PlaceObjectSlot ||
                 !ObjectPlacementLogic.IsItemValid(ref info, ref properties)) &&
                equipmentSlot.slotType != EquipmentSlotType.ShovelSlot) return;
            
            BrushRect extents = state.GetExtents();
                
            Vector3Int vector3Int = new Vector3Int(placementCD.bestPositionToPlaceAt.x, placementCD.bestPositionToPlaceAt.y, placementCD.bestPositionToPlaceAt.z);
            vector3Int = EntityMonoBehaviour.ToRenderFromWorld(vector3Int);
            var diff = currentTick.TicksSince(state.changedOnTick);
                
            __instance.placeableIcon.SetPosition(vector3Int, immediate || Mathf.Abs(diff) < 5);

            int newWidth = extents.width + 1;
            int newHeight = extents.height + 1;

            __instance.placeableIcon.SetSize(newWidth, newHeight);
        }
    }
}