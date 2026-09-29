using HarmonyLib;

namespace MovableSpawners.Patches
{
    [HarmonyPatch]
    public class PlayerController_Patch
    {
        [HarmonyPatch(typeof(PlayerController), nameof(PlayerController.GetObjectName))]
        [HarmonyPostfix]
        public static void GetObjectName(ContainedObjectsBuffer containedObject, bool localize, TextAndFormatFields __result)
        {
            ObjectDataCD objectData = containedObject.objectData;
            if (objectData.objectID == ObjectID.SummonArea &&
                objectData.variation != 0)
            {
                __result.text = $"{__result.text}{objectData.variation}";
            }
        }
    }
}