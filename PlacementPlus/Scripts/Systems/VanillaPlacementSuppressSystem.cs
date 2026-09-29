using PlayerEquipment;
using Unity.Entities;

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
    public partial struct VanillaPlacementSuppressSystem : ISystem
    {
        public void OnCreate(ref SystemState state) { }

        public void OnUpdate(ref SystemState state)
        {
            // In environments where the server prefix is active, the legacy path manages it — remains dormant.
            if (EquipmentSystem_Patch.serverPrefixAlive) return;

            ServerBrushExecutor.SuppressVanillaPlacement(ref state);
        }
    }
}
