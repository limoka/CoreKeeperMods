using HarmonyLib;
using MovableSpawners.Util;
using NaughtyAttributes.Test;
using PugConversion;
using PugMod;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace MovableSpawners.Patches
{
    [HarmonyPatch]
    public static class GhostConverter_Patch
    {

        [HarmonyPatch("PugConversion.GhostConverter", nameof(PugPostConverter.PostConvert))]
        [HarmonyPrefix]
        public static void OnPostConvert(PugPostConverter __instance, GameObject authoring)
        {
            var entityData = authoring.GetComponent<EntityMonoBehaviourData>();
            if (entityData == null ||
                entityData.objectInfo.objectID != ObjectID.SummonArea) return;
            
            var health = authoring.GetComponent<HealthAuthoring>();
            if (health != null) return;
            
            health = authoring.AddComponent<HealthAuthoring>();
            health.normalizedOverrideStartHealth = 1;
            health.startHealth = 2;
            health.maxHealth = 2;
            health.maxHealthMultiplier = 1;

            var entitymanager = __instance.GetValue<EntityManager>("EntityManager");
            var entity =  __instance.Invoke<Entity>("GetEntity", authoring);
            
            entitymanager.AddComponentData(entity, new HealthCD()
            {
                health = 2,
                maxHealth = 2
            });
            entitymanager.AddComponent<HealthChangeBuffer>(entity);

        }
    }
}