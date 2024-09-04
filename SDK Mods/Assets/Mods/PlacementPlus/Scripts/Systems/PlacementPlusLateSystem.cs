using PlayerEquipment;
using Unity.Entities;

namespace PlacementPlus.Systems
{
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(EquipmentUpdateSystemGroup))]
    [UpdateAfter(typeof(EquipmentUpdateSystem))]
    public partial class PlacementPlusLateSystem  : PugSimulationSystemBase
    {
        protected override void OnUpdate()
        {

            Entities.ForEach((ref EquipmentSlotCD slot) =>
                {
                    if (slot.slotType == (EquipmentSlotType)100)
                    {
                        slot.slotType = EquipmentSlotType.PlaceObjectSlot;
                    }
                })
                .WithoutBurst()
                .Schedule();
            
            base.OnUpdate();
        }
    }
}