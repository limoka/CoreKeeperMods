using KeepFarming.Components;
using Unity.Entities;
using Unity.NetCode;

namespace KeepFarming
{
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(BeginSimulationSystemGroup))]
    [UpdateAfter(typeof(SummarizeConditionsSystem))]
    public partial class ModConditionsSystem : PugSimulationSystemBase
    {
        protected override void OnCreate()
        {
            NeedDatabase();
            base.OnCreate();
        }

        protected override void OnUpdate()
        {
            var isClient = !isServer;

            PlayerController player = Manager.main.player;
            Entity localPlayerEntity = player != null ? player.entity : Entity.Null;
            int localPlayerEquippedIndex = localPlayerEntity != Entity.Null ? Manager.main.player.equippedSlotIndex : 0;
            ObjectDataCD localPlayerEquippedItem = player != null && player.GetEquippedSlot() != null ? player.GetEquippedSlot().objectData : default;
            var databaseLocal = database;

            Entities.ForEach((
                    Entity playerEntity,
                    ref DynamicBuffer<SummarizedConditionsBuffer> sumConditionsBuffer,
                    in DynamicBuffer<ContainedObjectsBuffer> container,
                    in EquippedObjectCD equippedObjectCd) =>
                {
                    ObjectDataCD objectDataCD;
                    int equippedSlotIndex;
                    if (isClient && playerEntity == localPlayerEntity)
                    {
                        equippedSlotIndex = localPlayerEquippedIndex;
                        objectDataCD = localPlayerEquippedItem;
                    }
                    else
                    {
                        equippedSlotIndex = equippedObjectCd.equippedSlotIndex;
                        objectDataCD = container[equippedSlotIndex].objectData;
                    }

                    Entity itemEntity = PugDatabase.GetPrimaryPrefabEntity(objectDataCD.objectID, databaseLocal, objectDataCD.variation);
                    if (itemEntity == Entity.Null) return;

                    if (equippedSlotIndex >= 0 &&
                        equippedSlotIndex < container.Length &&
                        SystemAPI.HasComponent<GoldenSeedCD>(itemEntity))
                    {
                        int id = (int)ConditionID.ChanceToGainRarePlant;

                        sumConditionsBuffer[id] = new SummarizedConditionsBuffer
                        {
                            value = sumConditionsBuffer[id].value + 100
                        };
                    }
                })
                .WithNone<EntityDestroyedCD>()
                .Schedule();

            base.OnUpdate();
        }
    }
}