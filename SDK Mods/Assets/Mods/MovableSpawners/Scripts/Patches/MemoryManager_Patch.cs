using HarmonyLib;
using UnityEngine;

namespace MovableSpawners.Patches
{
    [HarmonyPatch]
    public static class MemoryManager_Patch
    {
        private const string ControllerPath = "Assets/Mods/MovableSpawners/Animation/ModSummonArea.controller";
        
        [HarmonyPatch(typeof(MemoryManager), nameof(MemoryManager.Init))]
        [HarmonyPrefix]
        public static void OnInit(MemoryManager __instance)
        {
            if (__instance.poolablePrefabBanks == null)
            {
                return;
            }

            foreach (PoolablePrefabBank bank in __instance.poolablePrefabBanks)
            {
                if (bank is not PooledGraphicalObjectBank graphicalObjectBank) continue;
                
                foreach (var pool in graphicalObjectBank.poolInitializers)
                {
                    var summonArea = pool.prefab.gameObject.GetComponent<SummonArea>();
                    if (summonArea == null) continue;

                    var animator = summonArea.GetComponent<Animator>();
                    var animatorController = MovableSpawnersMod.AssetBundle.LoadAsset<RuntimeAnimatorController>(ControllerPath);
                    animator.runtimeAnimatorController = animatorController;
                    break;
                }
            }
        }
    }
}