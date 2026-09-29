using HarmonyLib;

namespace EquipmentKeeper
{
    [HarmonyPatch]
    public class PlayerController_Patch
    {
        [HarmonyPatch(typeof(PlayerController),"ReduceDurabilityOfAllEquipment")]
        [HarmonyPatch(typeof(PlayerController),"ReduceDurabilityOfEquipment")]
        [HarmonyPatch(typeof(PlayerController),"ReduceDurabilityOfHeldEquipment")]
        [HarmonyPatch(typeof(PlayerController),"ReducePercentageDurabilityOfAllEquipment")]
        [HarmonyPatch(typeof(PlayerController),"ReducePercentageDurabilityOfEquipment")]
        [HarmonyPrefix]
        static void DontReduceDurability(ref bool __runOriginal)
        {
            __runOriginal = false;
        }
    }
}