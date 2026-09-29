using CoreLib.Util.Extension;
using HarmonyLib;
using PlacementPlus.Components;
using PlacementPlus.Systems;
using PlacementPlus.Util;
using PlayerEquipment;
using Unity.Mathematics;
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

            // Size 0 is not a way out: tools keep state.size at 0 and carry their
            // area in the game's size variation instead. The prefabTileSize check
            // that used to sit here rejected them too (a shovel is 15x15) — for
            // placeables IsItemValid below already does that check.
            var equipmentSlot = world.EntityManager.GetComponentData<EquipmentSlotCD>(player.entity);

            // Even when the brush is turned off, we handle drawing the tools — because some tools 
            // use the upper bound as-is for vanilla size calculation (see comments in ShovelSlot_Patch), 
            // the preview gets stuck at 9x9. Placeables are left to vanilla handling as before.
            if (state.mode == BrushMode.NONE && !equipmentSlot.slotType.OwnsItsSize()) return;

            var query = world.EntityManager.CreateEntityQuery(typeof(NetworkTime));
            var currentTick = query.GetSingleton<NetworkTime>().ServerTick;
            query.Dispose();
            
            if (!objectPropertiesLookup.TryGetComponent(placementPrefab, out ObjectPropertiesCD properties))
                return;

            // The paint tool never passes IsItemValid (the brush itself is not a
            // placeable), so it is allowed through by slot type like the shovel.
            if ((equipmentSlot.slotType != EquipmentSlotType.PlaceObjectSlot ||
                 !ObjectPlacementLogic.IsItemValid(ref info, ref properties)) &&
                equipmentSlot.slotType != EquipmentSlotType.ShovelSlot &&
                equipmentSlot.slotType != EquipmentSlotType.HoeSlot &&
                equipmentSlot.slotType != EquipmentSlotType.RoofingToolSlot &&
                equipmentSlot.slotType != EquipmentSlotType.PaintToolSlot) return;
            
            // Mirror what MyPlacementHandler hands to the placement, or the icon
            // shows the tool's square while a line gets dug.
            int2 size;
            if (equipmentSlot.slotType.OwnsItsSize())
            {
                int2 toolSize = EquipmentSlot.GetTileSizeFromVariation(
                    equipmentSlot,
                    world.EntityManager.GetBuffer<PlacementSizeByEquipmentTypeBuffer>(player.entity, true),
                    info.prefabTileSize);
                // ⚠️ CutToBrush returns a 1x1 if the shape is set to "Off" — if used as-is, 
                // the preview will only draw a single cell, and right on the corner of the actual 
                // working area (Observed on 08-28: when holding the wooden shovel for the first time). 
                // We should only clip when a shape exists — following the same rule as MyPlacementHandler's 
                // actual calculations.
                size = state.mode == BrushMode.NONE ? toolSize : state.mode.CutToBrush(toolSize);
            }
            else
            {
                BrushRect extents = state.GetExtents();
                size = new int2(extents.width + 1, extents.height + 1);
            }

            // M2 (plan-deharmony): The placementCD argument handles prediction synchronization, 
            // so it bounces back and forth between the server's vanilla (1x1) anchor and the client patch's 
            // grid anchor (Round 1 observation: severe jittering of the preview grid). The screen trusts 
            // the cache just calculated by the client patch — if it is stale (not a placement slot or 
            // early-returned by the prefix), it falls back to the argument value.
            // 10-frame gap: The client prediction tick rate is coarser than the rendering frame rate 
            // (Observed via diagnostic logs: the pin drops out in 10-frame cycles, exposing the server-calculated 
            // anchor in between and causing judder — for conveyors, the server and client yield different anchors 
            // due to discrepancies in aim data). A narrow gap (2 frames) exposes this fallback periodically.
            int3 anchor = placementCD.bestPositionToPlaceAt;
            bool pinned = false;
            if (equipmentSlot.slotType == EquipmentSlotType.PlaceObjectSlot &&
                Mathf.Abs(Time.frameCount - PlaceObjectSlot_Patch.lastClientAnchorFrame) <= 10)
            {
                anchor = PlaceObjectSlot_Patch.lastClientAnchor;
                pinned = true;
            }

            Vector3Int vector3Int = new Vector3Int(anchor.x, anchor.y, anchor.z);
            vector3Int = EntityMonoBehaviour.ToRenderFromWorld(vector3Int);
            var diff = currentTick.TicksSince(state.changedOnTick);
                
            // Snap immediately when pinned — The vanilla body has already called SetPosition 
            // with a different target on the exact same frame. If the lerp target changes twice per frame, 
            // the icon will jitter as it constantly chases between the two points (Round 2 observation).
            __instance.placeableIcon.SetPosition(vector3Int, pinned || immediate || Mathf.Abs(diff) < 5);

            __instance.placeableIcon.SetSize(size.x, size.y);
        }
    }
}