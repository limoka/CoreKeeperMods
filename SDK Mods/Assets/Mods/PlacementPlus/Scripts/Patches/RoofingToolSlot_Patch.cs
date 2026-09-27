using HarmonyLib;
using PlacementPlus.Components;
using PlacementPlus.Util;
using PlayerEquipment;
using PlayerState;
using Pug.UnityExtensions;
using PugTilemap;
using Unity.Entities;
using Unity.Mathematics;

namespace PlacementPlus
{
    // The game's ToggleRoof decides "punch or fill" from the current tiles:
    // if every tile in range is already a hole (or wall) it fills, otherwise it
    // punches. That decision lives in a local, so honouring ADD_ROOF/CLEAR_ROOF
    // means replacing the method rather than nudging it.
    [HarmonyPatch]
    public static class RoofingToolSlot_Patch
    {
        // M3 (plan-deharmony): Client transmission dedup — Because the roof toggle is not idempotent 
        // (doing it twice on the same cell = reverts back to original state), duplicate transmissions 
        // caused by prediction re-simulations are filtered out using the wall-clock time.
        private static int3 s_lastSentAnchor;
        private static float s_lastSentReal = -10f;

        [HarmonyPatch(typeof(RoofingToolSlot), "ToggleRoof")]
        [HarmonyPrefix]
        public static bool OnToggleRoof(
            in EquipmentUpdateAspect equipmentUpdateAspect,
            EquipmentUpdateSharedData equipmentUpdateSharedData,
            LookupEquipmentUpdateData equipmentUpdateLookupData)
        {
            return Run(equipmentUpdateAspect, equipmentUpdateSharedData, equipmentUpdateLookupData,
                forceRun: false);
        }

        // forceRun: ServerBrushExecutor-exclusive — On Burst-enabled servers, vanilla roofing 
        // is "always" suppressed. Therefore, even the default mode + brush turned off combination 
        // (a combination originally left to vanilla handling) runs through this logic on the server instead. 
        // Under that specific combination, the logic below does not clip the bounds, and the TOGGLE 
        // evaluation strictly follows the native game rules, matching vanilla behavior perfectly.
        internal static bool Run(
            in EquipmentUpdateAspect equipmentUpdateAspect,
            EquipmentUpdateSharedData equipmentUpdateSharedData,
            LookupEquipmentUpdateData equipmentUpdateLookupData,
            bool forceRun)
        {
            var ppLookups = EquipmentSystem_Patch.GetLookups(equipmentUpdateSharedData.isServer);
            if (!ppLookups.stateLookup.HasComponent(equipmentUpdateAspect.entity)) return true;

            PlacementPlusState state = ppLookups.stateLookup[equipmentUpdateAspect.entity];
            RoofingToolMode mode = state.roofingMode;
            // Default Mode + Brush Off = Client-local handling defers to vanilla prediction. 
            // Server execution is requested by the RPC below, and the executor runs via forceRun.
            bool vanillaPath = mode != RoofingToolMode.ADD_ROOF &&
                               mode != RoofingToolMode.CLEAR_ROOF &&
                               state.mode == BrushMode.NONE;

            ref PlacementCD placement = ref equipmentUpdateAspect.placementCD.ValueRW;
            ObjectDataCD objectData = equipmentUpdateAspect.equippedObjectCD.ValueRO.containedObject.objectData;
            ref PugDatabase.EntityObjectInfo info = ref PugDatabase.GetEntityObjectInfo(
                objectData.objectID, equipmentUpdateSharedData.databaseBank.databaseBankBlob, objectData.variation);

            if (!placement.canPlaceObject || info.objectID == ObjectID.None) return vanillaPath;

            // M3 (plan-deharmony): On Burst-enabled servers, the server-side part of this prefix 
            // completely breaks, leaving roofing as vanilla (Observed on 08-25: all 3 modes defaulted 
            // to vanilla behavior). Therefore, we command the server to execute the placement on the exact 
            // frame the client finalizes execution — we send this even if it is a vanillaPath (since vanilla 
            // is always suppressed on the server, the executor is the sole execution authority). 
            // In environments where the server patch is fully functional, the executor remains dormant 
            // (serverPrefixAlive), making this harmless.
            // 0.3s dedup window: This only filters out re-simulation duplicates (which take tens of ms) — 
            // a 0.5s window previously swallowed legitimate rapid clicking that respected the 0.4s cooldown, 
            // which looked like a "malfunction" (08-25).
            if (!equipmentUpdateSharedData.isServer)
            {
                int3 sendAnchor = placement.bestPositionToPlaceAt;
                bool duplicate = math.all(sendAnchor == s_lastSentAnchor) &&
                                 UnityEngine.Time.realtimeSinceStartup - s_lastSentReal < 0.3f;
                if (!duplicate)
                {
                    s_lastSentAnchor = sendAnchor;
                    s_lastSentReal = UnityEngine.Time.realtimeSinceStartup;
                    PlacementPlusMod.commandSystem?.SendBrushPlace(
                        equipmentUpdateAspect.entity, sendAnchor, Commands.BrushKind.Roof);
                }
            }

            if (vanillaPath && !forceRun) return true;

            EquipmentSlot.StartCooldownForItem(
                equipmentUpdateAspect, equipmentUpdateSharedData, equipmentUpdateLookupData, 0.4f);

            int2 size = EquipmentSlot.GetTileSizeFromVariation(
                equipmentUpdateAspect.equipmentSlotCD.ValueRO,
                equipmentUpdateAspect.placementSizeByEquipmentTypeBuffer,
                info.prefabTileSize);

            // The gadget owns its size (Extensions.OwnsItsSize) — state.size holds
            // whatever the last placeable was set to, so the brush only picks the
            // shape here. Must match what MyPlacementHandler fed the placement,
            // or the tiles and the position drift apart.
            if (state.mode != BrushMode.NONE)
                size = state.mode.CutToBrush(size);

            float2 half = (float2)size / 2f;
            int3 basePos = placement.bestPositionToPlaceAt;
            equipmentUpdateAspect.flattenStateCD.ValueRW.positionToPlaceAt =
                basePos + new float3(half.x - 0.5f, 0f, half.y - 0.5f);
            equipmentUpdateAspect.playerStateCD.ValueRW.PushState(PlayerStateEnum.Flatten);

            TileAccessor tileAccessor = equipmentUpdateSharedData.tileAccessor;

            bool punch;
            if (mode == RoofingToolMode.ADD_ROOF) punch = true;
            else if (mode == RoofingToolMode.CLEAR_ROOF) punch = false;
            else
            {
                // TOGGLE: same rule as the game — fill only when the whole area
                // is already hole or wall, otherwise punch.
                bool allOpen = true;
                for (int x = 0; x < size.x && allOpen; x++)
                {
                    for (int y = 0; y < size.y; y++)
                    {
                        int2 p = (basePos + new int3(x, 0, y)).ToInt2();
                        if (!tileAccessor.HasType(p, TileType.roofHole) &&
                            !tileAccessor.GetTop(p).tileType.IsWallTile())
                        {
                            allOpen = false;
                            break;
                        }
                    }
                }
                punch = !allOpen;
            }

            bool isCreative = equipmentUpdateSharedData.worldInfoCD.IsWorldModeEnabled(WorldMode.Creative);
            var tileUpdateBuffer =
                equipmentUpdateLookupData.tileUpdateBufferLookup[equipmentUpdateSharedData.tileUpdateBufferEntity];

            for (int x = 0; x < size.x; x++)
            {
                for (int y = 0; y < size.y; y++)
                {
                    int2 pos = (basePos + new int3(x, 0, y)).ToInt2();
                    if (tileAccessor.GetTop(pos).tileType.IsWallTile()) continue;

                    bool hasHole = tileAccessor.HasType(pos, TileType.roofHole);

                    if (punch && !hasHole)
                    {
                        EntityUtility.AddTile(0, TileType.roofHole, pos, isCreative, tileUpdateBuffer);
                        SpawnEffect(equipmentUpdateAspect, equipmentUpdateSharedData, pos, 1);
                    }
                    else if (!punch && hasHole)
                    {
                        EntityUtility.RemoveTile(0, TileType.roofHole, pos, tileUpdateBuffer, tileAccessor);
                        SpawnEffect(equipmentUpdateAspect, equipmentUpdateSharedData, pos, 0);
                    }
                }
            }

            equipmentUpdateLookupData.reduceDurabilityOfEquippedTagLookup
                .SetComponentEnabled(equipmentUpdateAspect.entity, true);
            equipmentUpdateLookupData.reduceDurabilityOfEquippedTagLookup
                .GetRefRW(equipmentUpdateAspect.entity).ValueRW.triggerCounter++;

            return false;
        }

        private static void SpawnEffect(
            in EquipmentUpdateAspect aspect,
            EquipmentUpdateSharedData sharedData,
            int2 pos,
            int value)
        {
            var buffer = aspect.ghostEffectEventBuffer;
            ref GhostEffectEventBufferPointerCD pointer = ref aspect.ghostEffectEventBufferPointerCD.ValueRW;

            var evt = new GhostEffectEventBuffer
            {
                Tick = sharedData.currentTick,
                value = new EffectEventCD
                {
                    effectID = EffectID.RoofingToolEffect,
                    position1 = pos.ToFloat3(),
                    value1 = value
                }
            };

            buffer.AddToRingBuffer(ref pointer, in evt);
        }
    }
}
