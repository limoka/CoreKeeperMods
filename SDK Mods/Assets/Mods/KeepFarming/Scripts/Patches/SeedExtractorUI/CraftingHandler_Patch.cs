using System;
using CoreLib.Util.Extensions;
using HarmonyLib;
using KeepFarming.Components;
using Unity.Entities;

namespace KeepFarming
{
    [HarmonyPatch]
    public static class CraftingHandler_Patch
    {
        [HarmonyPatch(typeof(CraftingHandler), MethodType.Constructor, typeof(EntityMonoBehaviour), typeof(World))]
        [HarmonyPostfix]
        public static void OnNew(CraftingHandler __instance, EntityMonoBehaviour entityMonoBehaviour, World world)
        {
            var entity = entityMonoBehaviour.entity;
            if (world.EntityManager.HasComponent<SeedExtractorCD>(entity))
            {
                //__instance.outputInventoryHandler.Dispose();
                CraftingCD componentData = world.EntityManager.GetComponentData<CraftingCD>(entity);
                __instance.outputInventoryHandler = new InventoryHandler(entityMonoBehaviour, world, componentData.outputSlotIndex, 2, 2);
            }
        }
        
        [HarmonyPatch(typeof(CraftingHandler), nameof(CraftingHandler.GetNormalizedElapsedCraftingTime))]
        [HarmonyPrefix]
        public static bool OnGetNormalizedElapsedCraftingTime(CraftingHandler __instance, ref float __result)
        {
            var world = __instance.GetValue<World>("world");
            if (!world.EntityManager.HasComponent(__instance.craftingEntity, typeof(SeedExtractorCD))) return true;
            
            var seedExtractor = world.EntityManager.GetComponentData<SeedExtractorCD>(__instance.craftingEntity);
            var crafting = world.EntityManager.GetComponentData<CraftingCD>(__instance.craftingEntity);
            
            __result = (seedExtractor.processingTime - crafting.timeLeftToCraft) / seedExtractor.processingTime;
            
            return false;
        }
    }
}