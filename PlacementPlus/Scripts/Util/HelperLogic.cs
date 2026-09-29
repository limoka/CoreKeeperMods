using Inventory;
using PlacementPlus;
using PlayerEquipment;
using Unity.Entities;
using Unity.Mathematics;

namespace Mods.PlacementPlus.Scripts.Util
{
    internal static class HelperLogic
    {
        public static void ConsumeEquipmentInSlot(
            EquipmentUpdateAspect equipmentAspect, 
            EquipmentUpdateSharedData sharedData,
            DynamicBuffer<InventoryChangeBuffer> inventoryChangeBuffers,
            int slot,
            ObjectDataCD item,
            float3 position)
        {
            var newCount = math.max(item.amount - 1, 0);

            if (newCount > 0)
            {
                inventoryChangeBuffers.Add(new InventoryChangeBuffer()
                {
                    inventoryChangeData = Create.SetAmount(
                        equipmentAspect.entity,
                        slot,
                        item.objectID,
                        item.amount - 1
                    ),
                    playerEntity = equipmentAspect.entity,
                });
                return;
            }
            
            if (item.amount <= 0) return;
            
            DynamicBuffer<GhostEffectEventBuffer> ghostEffectEventBuffer = equipmentAspect.ghostEffectEventBuffer;
            ref GhostEffectEventBufferPointerCD valueRW = ref equipmentAspect.ghostEffectEventBufferPointerCD.ValueRW;
            GhostEffectEventBuffer ghostEffectEventBuffer2 = default(GhostEffectEventBuffer);
            ghostEffectEventBuffer2.Tick = sharedData.currentTick;
            ghostEffectEventBuffer2.value = new EffectEventCD
            {
                effectID = EffectID.DigGround,
                position1 = position
            };
            ghostEffectEventBuffer.AddToRingBuffer(ref valueRW, ghostEffectEventBuffer2);
            
            inventoryChangeBuffers.Add(new InventoryChangeBuffer()
            {
                inventoryChangeData = Create.SetAmount(
                    equipmentAspect.entity,
                    slot,
                    item.objectID,
                    0
                ),
                playerEntity = equipmentAspect.entity,
            });
            
            inventoryChangeBuffers.Add(new InventoryChangeBuffer()
            {
                inventoryChangeData = Create.TryReplaceBrokenObject(
                    equipmentAspect.entity,
                    slot
                ),
                playerEntity = equipmentAspect.entity
            });
        }

        public static int GetBestToolsSlots(
            in EquipmentUpdateAspect equipmentAspect,
            EquipmentUpdateSharedData sharedData,
            LookupEquipmentUpdateData lookupData,
            PlacementPlusLookups ppLookup,
            out int shovelSlot,
            out int pickaxeSlot,
            out ObjectDataCD shovel,
            out ObjectDataCD pickaxe
        )
        {
            int maxShovelDamage = 0;
            int maxPickaxeDamage = 0;
            shovelSlot = -1;
            pickaxeSlot = -1;
            shovel = default;
            pickaxe = default;

            lookupData.containedObjectsBufferLookup
                .TryGetBuffer(
                    equipmentAspect.entity,
                    out DynamicBuffer<ContainedObjectsBuffer> dynamicBuffer
                );

            for (int i = 0; i < dynamicBuffer.Length; i++)
            {
                ContainedObjectsBuffer objectsBuffer = dynamicBuffer[i];
                if (objectsBuffer.objectData.objectID == ObjectID.None) continue;
                if (objectsBuffer.objectData.amount == 0) continue;
                
                ref PugDatabase.EntityObjectInfo entityObjectInfo =
                    ref PugDatabase.GetEntityObjectInfo(
                        objectsBuffer.objectID,
                        sharedData.databaseBank.databaseBankBlob,
                        objectsBuffer.variation
                    );
                if (entityObjectInfo.prefabEntities.Length <= 0) continue;

                var conditionsBuffer = GetConditionsBuffer(
                    objectsBuffer.objectData, 
                    ref entityObjectInfo, 
                    lookupData.levelLookup,
                    lookupData.levelEntitiesLookup, 
                    ppLookup.conditionsLookup
                    );

                int shovelDamage = GetShovelDamage(objectsBuffer.objectData, ref entityObjectInfo, conditionsBuffer);
                int pickaxeDamage = GetPickaxeDamage(objectsBuffer.objectData, ref entityObjectInfo, conditionsBuffer);

                if (shovelDamage > maxShovelDamage)
                {
                    maxShovelDamage = shovelDamage;
                    shovelSlot = i;
                    shovel = objectsBuffer.objectData;
                }

                if (pickaxeDamage > maxPickaxeDamage)
                {
                    maxPickaxeDamage = pickaxeDamage;
                    pickaxeSlot = i;
                    pickaxe = objectsBuffer.objectData;
                }
            }

            return maxPickaxeDamage;
        }

        public static DynamicBuffer<GivesConditionsWhenEquippedBuffer> GetConditionsBuffer(
            ObjectDataCD objectData,
            ref PugDatabase.EntityObjectInfo entityObjectInfo,
            ComponentLookup<LevelCD> levelLookup,
            BufferLookup<LevelEntitiesBuffer> levelEntitiesLookup,
            BufferLookup<GivesConditionsWhenEquippedBuffer> conditionsLookup
        )
        {
            if (entityObjectInfo.prefabEntities.Length <= 0) return default;
            
            var prefab = entityObjectInfo.prefabEntities[0];
            var levelEntity = EntityUtility.GetLevelEntity(prefab, objectData,
                levelEntitiesLookup,
                levelLookup);
                
            DynamicBuffer<GivesConditionsWhenEquippedBuffer> conditionsBuffer;
            if (levelEntity != Entity.Null)
            {
                if (!conditionsLookup.TryGetBuffer(levelEntity, out conditionsBuffer)) return default;
            }
            else
            {
                if (!conditionsLookup.TryGetBuffer(prefab, out conditionsBuffer)) return default;
            }

            return conditionsBuffer;
        }


        public static int GetShovelDamage(
            ObjectDataCD item,
            ref PugDatabase.EntityObjectInfo objectInfo,
            DynamicBuffer<GivesConditionsWhenEquippedBuffer> conditionBuffer)
        {
            if (!conditionBuffer.IsCreated) return 0;
            if (objectInfo.objectType != ObjectType.Shovel) return 0;
            
            foreach (GivesConditionsWhenEquippedBuffer condition in conditionBuffer)
            {
                if (condition.equipmentCondition.id != ConditionID.DiggingIncrease) continue;
                
                return  condition.equipmentCondition.value;
            }

            return 0;
        }

        public static int GetPickaxeDamage(
            ObjectDataCD item,
            ref PugDatabase.EntityObjectInfo objectInfo,
            DynamicBuffer<GivesConditionsWhenEquippedBuffer> conditionBuffer
        )
        {
            if (!conditionBuffer.IsCreated) return 0;
            if (objectInfo.objectType != ObjectType.MiningPick) return 0;
            
            bool isReinforced = PugDatabase.HasComponent<DurabilityCD>(item) && PugDatabase.GetComponent<DurabilityCD>(item).IsReinforced(item.amount);
            
            foreach (GivesConditionsWhenEquippedBuffer condition in conditionBuffer)
            {
                if (condition.equipmentCondition.id != ConditionID.MiningIncrease) continue;
                var value = condition.equipmentCondition.value;
                
                var bonus = 0;

                if (isReinforced)
                    bonus = ConditionExtensions.GetReinforcedBonusValue(value, new ConditionInfo { Id = ConditionID.MiningIncrease});
                
                return value + bonus;
            }

            return 0;
        }

        public static int GetShovelLevel(int diggingDamage)
        {
            return diggingDamage switch
            {
                < 30 => 0,
                < 40 => 1,
                < 60 => 2,
                < 80 => 3,
                < 160 => 4,
                < 210 => 5,
                _ => 6
            };
        }
    }
}