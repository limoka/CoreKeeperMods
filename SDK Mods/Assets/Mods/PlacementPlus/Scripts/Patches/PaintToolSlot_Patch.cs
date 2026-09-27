using HarmonyLib;
using PlacementPlus.Components;
using PlayerEquipment;
using PlayerState;
using Pug.UnityExtensions;
using PugTilemap;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Entity = Unity.Entities.Entity;

namespace PlacementPlus
{
    // PaintToolSlot hides PlaceObjectSlot.UpdateEquipment with `new` instead of
    // overriding it, so the PlaceObjectSlot patch never runs for the paint tool.
    // That is why the brush had no effect on painting — this patches the paint
    // path directly.
    [HarmonyPatch]
    public static class PaintToolSlot_Patch
    {
        [HarmonyPatch(typeof(PaintToolSlot), nameof(PaintToolSlot.UpdateEquipment))]
        [HarmonyPrefix]
        public static bool OnUpdateEquipment(
            bool interactHeld,
            bool secondInteractHeld,
            in ClientInput clientInput,
            in EquipmentUpdateAspect equipmentUpdateAspect,
            in EquipmentUpdateSharedData equipmentUpdateSharedData,
            in LookupEquipmentUpdateData equipmentUpdateLookupData,
            bool hasItemInMouse,
            ref bool __result
        )
        {
            var ppLookups = EquipmentSystem_Patch.GetLookups(equipmentUpdateSharedData.isServer);
            if (!ppLookups.stateLookup.HasComponent(equipmentUpdateAspect.entity)) return true;

            PlacementPlusState state = ppLookups.stateLookup[equipmentUpdateAspect.entity];

            // Brush off -> let the game paint a single tile as usual.
            if (state.size == 0 || state.mode == BrushMode.NONE) return true;

            if (clientInput.IsButtonStateSet(CommandInputButtonStateNames.Rotate_Pressed))
            {
                PlaceObjectSlot.Rotate(equipmentUpdateAspect, equipmentUpdateSharedData, equipmentUpdateLookupData);
            }

            var entityAndInfos = new NativeList<PlacementHandler.EntityAndInfoFromPlacement>(Allocator.Temp);
            MyPlacementHandler.UpdatePlaceablePosition(
                equipmentUpdateAspect.equippedObjectCD.ValueRO.equipmentPrefab,
                ref entityAndInfos,
                equipmentUpdateAspect,
                equipmentUpdateSharedData,
                equipmentUpdateLookupData,
                state);
            entityAndInfos.Dispose();

            __result = false;
            if (!secondInteractHeld) return false;

            PaintGrid(equipmentUpdateAspect, equipmentUpdateSharedData, equipmentUpdateLookupData, state);

            __result = true;
            return false;
        }

        // M3 (plan-deharmony): Client transmission dedup (Same mechanism as the roof patch).
        private static int3 s_lastSentAnchor;
        private static float s_lastSentReal = -10f;

        // internal + ignoreGates: Called by ServerBrushExecutor via RPC — The server's 
        // canPlaceObject and tileToPaint hold values based on the vanilla anchor, meaning they 
        // cannot be used as validation gates (in the same spirit as forcing canPlaceObject=true for blocks). 
        // Safety is instead guaranteed by per-cell validation checks (paintable status and tileset existence).
        internal static void PaintGrid(
            in EquipmentUpdateAspect equipmentAspect,
            EquipmentUpdateSharedData sharedData,
            LookupEquipmentUpdateData lookupData,
            PlacementPlusState state,
            bool ignoreGates = false)
        {
            PlacementCD placement = equipmentAspect.placementCD.ValueRO;
            if (!ignoreGates)
            {
                if (!placement.canPlaceObject) return;

                // Objects are painted one at a time by the base game: each tile
                // would need its own entity lookup, so only tile painting is
                // spread here.
                if (placement.tileToPaint.tileType == TileType.none) return;
            }

            // M3: On Burst-enabled servers, the server-side part of this logic completely breaks, 
            // causing only a single cell to be painted (Observed on 08-25). Therefore, we command 
            // the server to execute the placement on the exact frame the client paints.
            if (!sharedData.isServer)
            {
                int3 sendAnchor = placement.bestPositionToPlaceAt;
                // 0.2s window: Legitimate repainting that respects the paint cooldown (0.25s) is allowed through.
                bool duplicate = math.all(sendAnchor == s_lastSentAnchor) &&
                                 UnityEngine.Time.realtimeSinceStartup - s_lastSentReal < 0.2f;
                if (!duplicate)
                {
                    s_lastSentAnchor = sendAnchor;
                    s_lastSentReal = UnityEngine.Time.realtimeSinceStartup;
                    PlacementPlusMod.commandSystem?.SendBrushPlace(
                        equipmentAspect.entity, sendAnchor, Commands.BrushKind.Paint);
                }
            }

            Entity equipmentPrefab = equipmentAspect.equippedObjectCD.ValueRO.equipmentPrefab;
            if (!lookupData.paintToolLookup.TryGetComponent(equipmentPrefab, out PaintToolCD paintTool)) return;

            int paintIndex = paintTool.paintIndex;

            // One cooldown for the whole stroke, not per tile.
            float cooldown = lookupData.godModeLookup.IsComponentEnabled(equipmentAspect.entity) ? 0.15f : 0.25f;
            EquipmentSlot.StartCooldownForItem(equipmentAspect, sharedData, lookupData, cooldown);

            TileAccessor tileAccessor = sharedData.tileAccessor;
            var tileUpdateBuffer = lookupData.tileUpdateBufferLookup[sharedData.tileUpdateBufferEntity];
            bool isCreative = sharedData.worldInfoCD.IsWorldModeEnabled(WorldMode.Creative);

            foreach (int3 pos in state.GetExtents().WithPos(placement.bestPositionToPlaceAt.ToInt2()))
            {
                int2 tilePos = new int2(pos.x, pos.z);

                TileCD tile = tileAccessor.GetTop(tilePos);
                if (tile.tileType == TileType.none) continue;

                // Vanilla only paints tiles whose prefab carries PaintableObjectCD
                // (PlacementHandlerPainting checks the same thing). Without this a
                // mud wall inside the stroke turns into a painted wall — free
                // material conversion.
                ObjectDataCD sourceItem = PugDatabase.TryGetTileItemInfo(
                    tile.tileType, (Tileset)tile.tileset, in sharedData.tileWithTilesetToObjectDataMapCD);
                Entity sourcePrefab = PugDatabase.GetPrimaryPrefabEntity(
                    sourceItem.objectID, sharedData.databaseBank.databaseBankBlob, sourceItem.variation);
                if (!lookupData.paintableObjectLookup.HasComponent(sourcePrefab)) continue;

                int tileset = (int)VanillaTools.PaintIndexToTileset(paintIndex, tile);
                if (PugDatabase.TryGetTileItemInfo(
                        tile.tileType,
                        (Tileset)tileset,
                        in sharedData.tileWithTilesetToObjectDataMapCD).objectID == ObjectID.None) continue;

                EntityUtility.AddTile(tileset, tile.tileType, tilePos, isCreative, tileUpdateBuffer);
            }

            equipmentAspect.placeObjectStateCD.ValueRW.positionToPlaceAt = placement.bestPositionToPlaceAt;
            equipmentAspect.playerStateCD.ValueRW.PushState(PlayerStateEnum.PlaceObject);
        }
    }
}
