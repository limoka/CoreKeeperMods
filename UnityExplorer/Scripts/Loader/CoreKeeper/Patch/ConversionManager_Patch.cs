using HarmonyLib;
using Pug.Conversion;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Mods.UnityExplorer.Scripts.Loader.CoreKeeper.Patch
{
    [HarmonyPatch]
    public static class ConversionManager_Patch
    {

        [HarmonyPatch(typeof(ConversionManager), "RunConverters")]
        [HarmonyPostfix]
        public static void OnRunConverters(ConversionManager __instance, GameObject gameObject, Entity entity)
        {
            var entityManager = __instance.EntityManager;
            var name = new FixedString64Bytes();
            name.CopyFromTruncated(gameObject.name);
            
            entityManager.SetName(entity, name);
        }
    }
}