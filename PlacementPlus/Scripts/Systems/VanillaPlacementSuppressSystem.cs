using Mods.PlacementPlus.Scripts.Util;
using PlacementPlus.Components;
using PlacementPlus.Util;
using PlayerEquipment;
using Pug.Properties;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace PlacementPlus
{
    // M2 Round 3.5 (plan-deharmony): The "slot" for suppressing vanilla server placement.
    // Because SelectedEquipmentChangeSystem (within EquipmentBeforeUpdateSystemGroup) 
    // reassigns the slotType "unconditionally every tick" (not just upon changes — a correction 
    // from a Round 3 misreading), the suppression write must happen strictly *between* that reassignment 
    // and the EquipmentUpdateSystem dispatch to be effective. Therefore, we insert it into the same group 
    // using UpdateBefore.
    // A hand-written plain ISystem — this serves as empirical proof that the serializer's 
    // RpcCommandRequestSystem compiles and operates in the exact same manner.
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(EquipmentUpdateSystemGroup))]
    [UpdateBefore(typeof(EquipmentUpdateSystem))]
    public partial struct VanillaPlacementSuppressSystem : ISystem, ISystemStartStop
    {
        private EntityArchetype _achievementArchetype;
        private BlobAssetReference<PugDatabase.PugDatabaseBank> _database;
        private TileAccessor _tileAccessor;

        public void OnCreate(ref SystemState state)
        {
            _achievementArchetype = state.EntityManager.CreateArchetype(typeof(AchievementSystem.AchievementRpc), typeof(SendRpcCommandRequest));
           
            state.RequireForUpdate<BeginSimulationEntityCommandBufferSystem.Singleton>();
            state.RequireForUpdate<PugDatabase.DatabaseBankCD>();
        }
        
        public void OnStartRunning(ref SystemState state)
        {
            _database = SystemAPI.GetSingleton<PugDatabase.DatabaseBankCD>().databaseBankBlob;
            _tileAccessor = new TileAccessor(ref state, true);
        }
        
        public void OnStopRunning(ref SystemState state)
        {
        }

        public void OnUpdate(ref SystemState state)
        {
            // In environments where the server prefix is active, the legacy path manages it — remains dormant.
            if (EquipmentSystem_Patch.serverPrefixAlive) return;

            _tileAccessor.Update(ref state);
            
            var ecbSingleton = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>();
            EntityCommandBuffer ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);
            
            var lookupData = Helpers.BuildLookupData(ref state);

            var canUpdatePlacement = Helpers.TryBuildSharedData(
                ref state,
                ecb,
                out var sharedData,
                _tileAccessor,
                _achievementArchetype
            );

            var objectProps = state.GetComponentLookup<ObjectPropertiesCD>();
            
            foreach (var (
                         aspect,
                         slot,
                         ppState,
                         held
                         ) in SystemAPI.Query<
                              EquipmentUpdateAspect,
                         RefRW<EquipmentSlotCD>,
                         RefRO<PlacementPlusState>,
                         RefRO<EquippedObjectCD>
                     >())
            {
                bool suppress = false;
                var objectData = held.ValueRO.containedObject.objectData;
                if (objectData.objectID != ObjectID.None && held.ValueRO.containedObject.auxDataIndex <= 0)
                {
                    ref var info = ref PugDatabase.GetEntityObjectInfo(objectData.objectID,
                        _database, objectData.variation);
                    if (info.objectID != ObjectID.None &&
                        held.ValueRO.equipmentPrefab != Entity.Null &&
                        objectProps.HasComponent(held.ValueRO.equipmentPrefab))
                    {
                        var properties = objectProps[held.ValueRO.equipmentPrefab];
                        suppress = ObjectPlacementLogic.IsItemValid(ref info, ref properties);
                    }
                }

                // M3: If the modification is enabled for the roof apparatus or paintbrush, we also disable 
                // the server's vanilla handling — if the vanilla roofing behavior (toggle) overlaps with our RPC 
                // execution, the same cell gets re-toggled and messed up, while for paint, it duplicates a single vanilla cell. 
                // The evaluation logic here is a mirror image of the client prefix's "defer to vanilla" condition 
                // (the state has already been synced to the server via the existing RPC). The slotType is reliable here 
                // since the reassignment system has already restored it to its normal value for this tick.
                if (!suppress)
                {
                    if (slot.ValueRO.slotType == EquipmentSlotType.RoofingToolSlot)
                        // "Always" suppress — If conditioned on mod states, the server placementCD calculation 
                        // authority would bounce back and forth (vanilla ↔ ours) whenever modes toggled, 
                        // causing the preview to get stuck on stale values (Observed on 08-25). The behavior 
                        // of the default mode is instead replicated by the executor via forceRun.
                        suppress = true;
                    else if (slot.ValueRO.slotType == EquipmentSlotType.PaintToolSlot)
                        suppress = ppState.ValueRO.size != 0 && ppState.ValueRO.mode != BrushMode.NONE;
                    else if (slot.ValueRO.slotType == EquipmentSlotType.ShovelSlot ||
                             slot.ValueRO.slotType == EquipmentSlotType.HoeSlot)
                        // Tools are "always" suppressed — If the server runs vanilla logic, ① it cannot read the shape, 
                        // causing it to dig in a true square (Observed on 08-27), ② its validation desyncs from the client, 
                        // causing the preview to flicker, and ③ tools that ignore size settings (wooden/copper shovels) 
                        // get stuck at 9x9 (Observed on 08-28). Handled identically to roofing.
                        suppress = true;
                }

                // Updating server placementCD every tick using our grid logic — placementCD handles 
                // ghost synchronization (Confirmed via PlacementCDGhostComponentSerializer), so if updates stop, 
                // the frozen server values will repeatedly roll back client predictions on every snapshot, 
                // leaving preview jitter and re-simulation lag behind. This calls the exact same logic that 
                // the server prefix handled back in the dll-off days (Server execution verified).
                // ⚠️ This must happen strictly *before* changing slotType to 100 — CanPlaceObjectAtPositionForSlotType 
                // internally branches based on slotType, and if it encounters 100, it throws a "Non implemented slot" 
                // error every single tick, breaking the server calculations (Observed 4,403 times on 08-25 — we missed this 
                // for blocks because the pinned icon masked it on screen, but for roofing, which lacks pins, the preview 
                // died entirely).
                if (suppress && canUpdatePlacement)
                {
                    var nativeList = new NativeList<PlacementHandler.EntityAndInfoFromPlacement>(Allocator.Temp);
                    MyPlacementHandler.UpdatePlaceablePosition(
                        held.ValueRO.equipmentPrefab,
                        ref nativeList,
                        aspect,
                        sharedData,
                        lookupData,
                        ppState.ValueRO);
                    nativeList.Dispose();
                }

                // ⚠️ Suppression (slotType=100) doesn't just disable "placement"—it shuts down the entire 
                // dispatch pipeline for that slot. Consequently, vanilla interactions handled via the 'R' key 
                // (such as rotation and size changes) break on the server side, causing the client's predicted 
                // modifications to be rolled back by incoming server snapshots (Reported by SirSephiroth1 on 08-27: 
                // changing shovel sizes with 'R' immediately reverted, and conveyor/drill rotations reverted too 
                // — the clue was that chests were completely fine since they were on the exclusion list). 
                // While suppression is active, we must manually handle those operations on behalf of the game.
                if (suppress && canUpdatePlacement)
                {
                    if (aspect.clientInput.ValueRO.IsButtonStateSet(CommandInputButtonStateNames.Rotate_Pressed))
                    {
                        if (slot.ValueRO.slotType.OwnsItsSize())
                        {
                            // Tools: Replicates the exact calculation of the game's EquipmentSlot.ChangeSize 
                            // (cannot be called directly since it is protected, so we reproduce it here). 
                            // The slot key must be the actual value rather than 100 to avoid creating ghost elements.
                            var sizeData = held.ValueRO.containedObject.objectData;
                            ref var sizeInfo = ref PugDatabase.GetEntityObjectInfo(
                                sizeData.objectID, _database, sizeData.variation);
                            int cap = sizeInfo.prefabTileSize.x;
                            if (cap > 1)
                            {
                                ref var sizeElement = ref aspect.placementSizeByEquipmentTypeBuffer
                                    .GetElementForEquipment(slot.ValueRO.slotType);
                                int current = Unity.Mathematics.math.min(sizeElement.sizeVariationToPlace, cap - 1);
                                sizeElement.sizeVariationToPlace = (byte)((current + 1) % cap);
                            }
                        }
                        else
                        {
                            // Placeables/Paint: Replicates the game's exact rotation logic (public static).
                            PlaceObjectSlot.Rotate(aspect, sharedData, lookupData);
                        }
                    }
                }

                if (suppress && slot.ValueRO.slotType != (EquipmentSlotType)100)
                {
                    slot.ValueRW.slotType = (EquipmentSlotType)100;
                }
                else if (!suppress && slot.ValueRO.slotType == (EquipmentSlotType)100)
                {
                    slot.ValueRW.slotType = EquipmentSlotType.PlaceObjectSlot;
                }
            }
        }
    }
}