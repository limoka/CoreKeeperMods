using Unity.Entities;
using Unity.NetCode;

namespace InstantPortalCharge
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial class PortalChargeSystem : PugSimulationSystemBase
    {
        protected override void OnUpdate()
        {
            Entities.ForEach((
                    ref ObjectDataCD objectDataCd
                ) =>
                {
                    if (objectDataCd.amount < 1200)
                    {
                        objectDataCd.amount = 1200;
                    }
                })
                .WithName("PortalCharge")
                .WithBurst()
                .WithAll<PortalCD>()
                .WithNone<WayPointCD>()
                .WithNone<EntityDestroyedCD>()
                .WithEntityQueryOptions(EntityQueryOptions.IncludeDisabledEntities)
                .Schedule();

            Entities.ForEach((
                    ref ObjectDataCD objectDataCd,
                    in WayPointCD wayPoint,
                    in DistanceToPlayerCD distance
                ) =>
                {
                    if (objectDataCd.amount >= 600) return;
                    
                    float minDis = distance.minDistanceSq;
                    if (!(minDis > 0) || !(minDis <= wayPoint.distanceToActivateSQ)) return;
                    
                    objectDataCd.amount = 600;
                })
                .WithName("WayPointCharge")
                .WithBurst()
                .Schedule();

            base.OnUpdate();
        }
    }
}