using Inventory;
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
            BufferLookup<GivesConditionsWhenEquippedBuffer> conditionsLookup,
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

            for (int i = 0; i < 10; i++)
            {
                ContainedObjectsBuffer objectsBuffer = dynamicBuffer[i];
                ref PugDatabase.EntityObjectInfo entityObjectInfo =
                    ref PugDatabase.GetEntityObjectInfo(
                        objectsBuffer.objectID,
                        sharedData.databaseBank.databaseBankBlob,
                        objectsBuffer.variation
                    );

                int shovelDamage = GetShovelDamage(objectsBuffer.objectData, ref entityObjectInfo, conditionsLookup);
                int pickaxeDamage = GetPickaxeDamage(objectsBuffer.objectData, ref entityObjectInfo, conditionsLookup);


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
        
        
        public static int GetShovelDamage(
            ObjectDataCD item,
            ref PugDatabase.EntityObjectInfo objectInfo,
            BufferLookup<GivesConditionsWhenEquippedBuffer> conditionsLookup)
        {
            if (item.objectID == ObjectID.None) return 0;
            if (item.amount == 0) return 0;

            if (objectInfo.objectType != ObjectType.Shovel) return 0;

            var entity = objectInfo.prefabEntities[0];
            if (!conditionsLookup.TryGetBuffer(entity, out var buffer)) return 0;

            foreach (GivesConditionsWhenEquippedBuffer condition in buffer)
            {
                if (condition.equipmentCondition.id != ConditionID.DiggingIncrease) continue;

                return condition.equipmentCondition.value;
            }

            return 0;
        }

        public static int GetPickaxeDamage(
            ObjectDataCD item,
            ref PugDatabase.EntityObjectInfo objectInfo,
            BufferLookup<GivesConditionsWhenEquippedBuffer> conditionsLookup
        )
        {
            if (item.objectID == ObjectID.None) return 0;
            if (item.amount == 0) return 0;

            if (objectInfo.objectType != ObjectType.MiningPick) return 0;

            var entity = objectInfo.prefabEntities[0];
            if (!conditionsLookup.TryGetBuffer(entity, out var buffer)) return 0;

            foreach (GivesConditionsWhenEquippedBuffer condition in buffer)
            {
                if (condition.equipmentCondition.id != ConditionID.MiningIncrease) continue;

                return condition.equipmentCondition.value;
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