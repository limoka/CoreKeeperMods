using KeepFarming.Components;
using PlayerEquipment;
using Unity.Entities;

namespace KeepFarming
{
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(BeforePredictedFixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(SummarizeConditionsSystem))]
    [UpdateBefore(typeof(EquipmentSystemGroup))]
    public partial class ModConditionsSystem : PugSimulationSystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<ConditionsTableCD>();
            NeedDatabase();
            base.OnCreate();
        }

        protected override void OnUpdate()
        {
            var databaseLocal = database;
            var conditionsTable = SystemAPI.GetSingleton<ConditionsTableCD>().Value;

            Entities.ForEach((
                    ref DynamicBuffer<SummarizedConditionsBuffer> sumConditionsBuffer,
                    ref DynamicBuffer<SummarizedConditionEffectsBuffer> sunConditionEffectsBuffer,
                    in DynamicBuffer<ContainedObjectsBuffer> container,
                    in EquippedObjectCD equippedObjectCd) =>
                {
                    var equippedSlotIndex = equippedObjectCd.equippedSlotIndex;
                    var objectDataCD = equippedObjectCd.containedObject.objectData;
                    
                    Entity itemEntity = PugDatabase.GetPrimaryPrefabEntity(objectDataCD.objectID, databaseLocal, objectDataCD.variation);
                    if (itemEntity == Entity.Null) return;

                    if (equippedSlotIndex < 0 ||
                        equippedSlotIndex >= container.Length ||
                        !SystemAPI.HasComponent<GoldenSeedCD>(itemEntity)) return;
                    
                    int id = (int)ConditionID.ChanceToGainRarePlant;
                    int effect = (int)conditionsTable.Value.infos[id].effect;
                    
                    sumConditionsBuffer.ElementAt(id) = new SummarizedConditionsBuffer
                    {
                        value = sumConditionsBuffer[id].value + 100
                    };
                    sunConditionEffectsBuffer.ElementAt(effect) = new SummarizedConditionEffectsBuffer
                    {
                        value = sunConditionEffectsBuffer[effect].value + 100
                    };
                })
                .WithNone<EntityDestroyedCD>()
                .Schedule();

            base.OnUpdate();
        }
    }
}