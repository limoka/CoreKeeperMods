using System.Collections.Generic;
using DummyMod;
using HarmonyLib;
using Interaction;
using UnityEngine;

namespace DefaultNamespace
{
    [HarmonyPatch]
    public static class PugDatabase_Patch
    {
        [HarmonyPatch(typeof(PugDatabase), nameof(PugDatabase.BuildAuthoringListFromEntityDataBlocks))]
        [HarmonyPostfix]
        public static void OnBuildAuthoringListFromEntityDataBlocks(List<MonoBehaviour> __result)
        {
            foreach (var prefab in __result)
            {
                var monoBehaviour = prefab.GetComponent<EntityMonoBehaviourData>();
                if (monoBehaviour == null) continue;

                var objectId = monoBehaviour.objectInfo.objectID;
                if (objectId != ObjectID.TrainingDummy) continue;

                var localInteractable = prefab.GetComponent<LocalInteractableAuthoring>();
                if (localInteractable != null) return;
                
                TheDummyMod.Log.LogInfo("Added LocalInteractableAuthoring ");
                prefab.gameObject.AddComponent<LocalInteractableAuthoring>();
                break;
            }
            
        }
        
    }
}