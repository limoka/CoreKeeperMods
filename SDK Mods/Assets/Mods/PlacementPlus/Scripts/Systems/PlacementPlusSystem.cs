
using Mods.PlacementPlus.Scripts.Util;
using PlacementPlus.Components;
using PlayerEquipment;
using Unity.Entities;
using Unity.NetCode;
using Unity.Physics;

namespace PlacementPlus.Systems
{
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(EquipmentUpdateSystemGroup))]
    [UpdateBefore(typeof(EquipmentUpdateSystem))]
    public partial class PlacementPlusSystem : PugSimulationSystemBase
    {
        private int _tickRate;

        private EntityArchetype _achievementArchetype;

        protected override void OnCreate()
        {
            _tickRate = PlatformConfiguration.Instance.SessionConfiguration.SimulationTickRate;
            _achievementArchetype = AchievementSystem.GetRpcArchetype(EntityManager);

            RequireForUpdate<PhysicsWorldSingleton>();
            RequireForUpdate<WorldInfoCD>();
            RequireForUpdate<TileWithTilesetToObjectDataMapCD>();

            NeedDatabase();
            base.OnCreate();
        }

        protected override void OnUpdate()
        {
            var networkTime = SystemAPI.GetSingleton<NetworkTime>();
            var currentTick = networkTime.ServerTick;

            var databaseBank = SystemAPI.GetSingleton<PugDatabase.DatabaseBankCD>();

            var givesConditionsLookup = SystemAPI.GetBufferLookup<GivesConditionsWhenEquippedBuffer>();

            Entities.ForEach((
                    ref PlacementPlusState state,
                    in EquippedObjectCD equippedObjectCD
                ) =>
                {
                    int maxSize = PlacementPlusMod.maxSize.Value - 1;

                    ObjectDataCD objectData = equippedObjectCD.containedObject.objectData;
                    ref PugDatabase.EntityObjectInfo entityObjectInfo = ref PugDatabase.GetEntityObjectInfo(objectData.objectID,
                        databaseBank.databaseBankBlob, objectData.variation);

                    int damage = HelperLogic.GetShovelDamage(objectData, ref entityObjectInfo, givesConditionsLookup);
                    if (damage == 0)
                    {
                        state.currentMaxSize = maxSize;
                        state.CheckSize(currentTick);
                        return;
                    }

                    state.currentMaxSize = HelperLogic.GetShovelLevel(damage);
                    state.CheckSize(currentTick);
                })
                .WithName("UpdateMaxSize")
                .WithoutBurst()
                .Schedule();

            base.OnUpdate();
        }
    }
}