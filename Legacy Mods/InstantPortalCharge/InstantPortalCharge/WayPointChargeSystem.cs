using Unity.Entities;

namespace InstantPortalCharge
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial class PortalChargeServerSystem : PugSimulationSystemBase
    {
        protected override void OnUpdate()
        {
            Entities.ForEach((ref ObjectDataCD objectDataCd, in WayPointCD wayPoint, in DistanceToPlayerCD dis) =>
            {
                if (objectDataCd.amount < 600)
                {
                    float minDis = dis.minDistanceSq;
                    if (minDis > 0 && minDis <= wayPoint.distanceToActivateSQ)
                    {
                        objectDataCd.amount = 600;
                    }
                }
            })
                .WithName("WayPointCharge")
                .WithBurst()
                .WithEntityQueryOptions(EntityQueryOptions.IncludeDisabledEntities)
                .Schedule();

            base.OnUpdate();
        }
    }

}
