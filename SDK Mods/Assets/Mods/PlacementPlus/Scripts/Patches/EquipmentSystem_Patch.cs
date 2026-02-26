using HarmonyLib;
using Mods.PlacementPlus.Scripts.Util;
using PlayerEquipment;
using Unity.Entities;
using Unity.NetCode;

namespace PlacementPlus
{
    [HarmonyPatch]
    public static class EquipmentSystem_Patch
    {
        internal static PlacementPlusLookups plusLookupsClient;
        internal static PlacementPlusLookups plusLookupsServer;

        internal static PlacementPlusLookups GetLookups(bool isServer)
        {
            return isServer ? plusLookupsServer : plusLookupsClient;
        }
        
        [HarmonyPatch(typeof(EquipmentUpdateSystem), nameof(EquipmentUpdateSystem.OnUpdate))]
        [HarmonyPrefix]
        public static void OnUpdate(EquipmentUpdateSystem __instance, ref SystemState state)
        {
            bool isServer = state.WorldUnmanaged.IsServer();
            
            if (isServer)
                plusLookupsServer.Init(ref state);
            else 
                plusLookupsClient.Init(ref state);
        }
    }
}