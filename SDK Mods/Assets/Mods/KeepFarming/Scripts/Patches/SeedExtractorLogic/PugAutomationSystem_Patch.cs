using HarmonyLib;
using KeepFarming.Access;
using KeepFarming.Components;
using Mods.KeepFarming.Scripts.Jobs;
using PugAutomation;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace KeepFarming
{
    [HarmonyPatch]
    public class PugAutomationSystem_Patch
    {
        [HarmonyPatch(typeof(PugAutomationSystem), "mover_move_and_pickup_Execute")]
        [HarmonyPrefix]
        public static bool OnMoverMoveAndPickupExecute(
            PugAutomationSystem __instance, 
            uint seed, 
            NativeParallelMultiHashMap<int2, Entity> moveeAtPosition, 
            NativeParallelHashMap<int2, Entity> storageAtPosition, 
            ComponentDataFromEntity<CraftingCD> craftingLookup, 
            BufferFromEntity<ContainedObjectsBuffer> containerLookup, 
            TileAccessor tileLookup)
        {
            __instance.GetMoverCDTypeHandle_Public().Update(__instance);
            ModifiedMoverMoveAndPickupJob mover_move_and_pickup_Job = default;
            mover_move_and_pickup_Job.seed = seed;
            mover_move_and_pickup_Job.moveeAtPosition = moveeAtPosition;
            mover_move_and_pickup_Job.storageAtPosition = storageAtPosition;
            mover_move_and_pickup_Job.craftingLookup = craftingLookup;
            mover_move_and_pickup_Job.containerLookup = containerLookup;
            mover_move_and_pickup_Job.tileLookup = tileLookup;
            mover_move_and_pickup_Job.seedExtractorLookup = __instance.GetComponentDataFromEntity<SeedExtractorCD>(true);
            mover_move_and_pickup_Job.__entityTypeHandle = __instance.GetEntityTypeHandle();
            mover_move_and_pickup_Job.__moverTypeHandle = __instance.GetMoverCDTypeHandle_Public();
            mover_move_and_pickup_Job.__PugAutomation_BigEntityRefCD_FromEntity = __instance.GetComponentDataFromEntity<BigEntityRefCD>(true);
            mover_move_and_pickup_Job.__PickUpObjectCD_FromEntity = __instance.GetComponentDataFromEntity<PickUpObjectCD>(true);
            mover_move_and_pickup_Job.__PugAutomation_MoveeCD_FromEntity = __instance.GetComponentDataFromEntity<MoveeCD>();

            __instance.SetDependency_Public(mover_move_and_pickup_Job.Schedule(__instance.GetMoverMoveAndPickupQuery_Public(), __instance.GetDependency_Public()));

            return false; // Do not run original!
        }
    }
}