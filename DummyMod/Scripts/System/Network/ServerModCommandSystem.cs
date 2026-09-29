using Unity.Entities;
using Unity.NetCode;

namespace DummyMod
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial class ServerModCommandSystem : PugSimulationSystemBase
    {
        protected override void OnUpdate()
        {
            bool guestMode = WorldInfo.guestMode;
            var ecb = CreateCommandBuffer();

            Entities.ForEach((Entity rpcEntity, in DummyCommandRPC rpc, in ReceiveRpcCommandRequest req) =>
                {
                    int adminLevel = 0;
                    if (SystemAPI.HasComponent<ConnectionAdminLevelCD>(req.SourceConnection))
                        adminLevel = SystemAPI.GetComponent<ConnectionAdminLevelCD>(req.SourceConnection).adminPrivileges;

                    if (!guestMode || adminLevel > 0)
                    {
                        switch (rpc.commandType)
                        {
                            case DummyCommandType.RESET_DUMMY:

                                DummyCD dummy = SystemAPI.GetComponent<DummyCD>(rpc.entity0);
                                dummy.lastDamage = 0;
                                dummy.minDamage = int.MaxValue;
                                dummy.maxDamage = 0;
                                dummy.averageDamage = 0;
                                dummy.damagePerSecond = 0;
                                dummy.maxDamagePerSecond = 0;
                                dummy.oldAverageDamage = 0;
                                dummy.damageCount = 0;
                                ecb.SetComponent(rpc.entity0, dummy);

                                DynamicBuffer<DummyDamageBuffer> damageBuffer = SystemAPI.GetBuffer<DummyDamageBuffer>(rpc.entity0);
                                damageBuffer.Clear();
                                break;
                            
                            case DummyCommandType.DESTROY_DUMMY:
                                var target = rpc.entity0;
                                ecb.SetComponentEnabled<EntityDestroyedCD>(target, true);
                                break;
                        }
                    }

                    ecb.DestroyEntity(rpcEntity);
                })
                .WithoutBurst()
                .Schedule();

            base.OnUpdate();
        }
    }
}