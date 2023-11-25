using System;
using KeepFarming.Components;
using PugAutomation;
using PugTilemap;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Random = Unity.Mathematics.Random;

namespace Mods.KeepFarming.Scripts.Jobs
{
    public struct ModifiedMoverMoveAndPickupJob : IJobEntityBatch
    {
        private void OriginalLambdaBody(Entity entity, ref MoverCD mover)
        {
            if (mover.timer >= 0) return;

            bool flag = false;
            bool flag2 = false;

            if (moveeAtPosition.TryGetFirstValue(mover.start, out Entity moveeEntity, out var iterator))
            {
                Random random = PugRandom.GetRngFromEntity(seed, entity);
                do
                {
                    Entity targetEntity = __PugAutomation_BigEntityRefCD_FromEntity.HasComponent(moveeEntity)
                        ? __PugAutomation_BigEntityRefCD_FromEntity[moveeEntity].Value
                        : moveeEntity;
                    
                    bool hasPickUpObject = __PickUpObjectCD_FromEntity.HasComponent(targetEntity);
                    if (mover.inventoryEntity == Entity.Null)
                    {
                        MoveeCD moveeCD = __PugAutomation_MoveeCD_FromEntity[moveeEntity];
                        if (!hasPickUpObject && !flag)
                        {
                            flag2 = tileLookup.GetBlockingTile(mover.stop).tileType > TileType.none;
                            flag = true;
                        }

                        if ((hasPickUpObject || !flag2) && (moveeCD.moveTimer <= 0 || random.NextBool()))
                        {
                            moveeCD.target = mover.stop;
                            moveeCD.moveTimer = (int)math.round(mover.moveTime * math.distancesq(moveeCD.position, mover.stop) /
                                                                math.distancesq(mover.start, mover.stop));
                            __PugAutomation_MoveeCD_FromEntity[moveeEntity] = moveeCD;
                            mover.timer = mover.cooldownTime;
                        }
                    }
                    else if (hasPickUpObject)
                    {
                        InventoryUtility.AutomatedPickup(containerLookup, craftingLookup, targetEntity, mover.inventoryEntity);
                        PickupFromSeedExtractor(mover, targetEntity);
                        
                        if (containerLookup[mover.inventoryEntity][0].objectData.objectID != ObjectID.None)
                        {
                            mover.timer = mover.moveTime;
                        }
                    }
                } while (mover.timer < 0 && moveeAtPosition.TryGetNextValue(out moveeEntity, ref iterator));
            }

            if (mover.inventoryEntity != Entity.Null && 
                mover.timer < 0 && 
                storageAtPosition.ContainsKey(mover.start))
            {
                InventoryUtility.AutomatedPickup(containerLookup, craftingLookup, storageAtPosition[mover.start], mover.inventoryEntity);
                PickupFromSeedExtractor(mover, storageAtPosition[mover.start]);
                
                if (containerLookup[mover.inventoryEntity][0].objectData.objectID != ObjectID.None)
                {
                    mover.timer = mover.moveTime;
                }
            }
        }

        private void PickupFromSeedExtractor(MoverCD mover, Entity targetEntity)
        {
            if (!seedExtractorLookup.HasComponent(targetEntity)) return;
            
            DynamicBuffer<ContainedObjectsBuffer> targetBuffer = containerLookup[targetEntity];
            DynamicBuffer<ContainedObjectsBuffer> moverBuffer = containerLookup[mover.inventoryEntity];
            if (moverBuffer[0].objectData.objectID != ObjectID.None) return;
            
            int outputSlotIndex = seedExtractorLookup[targetEntity].juiceOutputSlot;
            ContainedObjectsBuffer containedObjectsBuffer = targetBuffer[outputSlotIndex];
            
            if (containedObjectsBuffer.objectID != ObjectID.None)
            {
                targetBuffer[outputSlotIndex] = new ContainedObjectsBuffer();
                moverBuffer[0] = containedObjectsBuffer;
            }
        }

        public void Execute(ArchetypeChunk chunk, int batchIndex)
        {
            IntPtr entityPtr = InternalCompilerInterface.UnsafeGetChunkEntityArrayIntPtr(chunk, __entityTypeHandle);
            IntPtr moverPtr = InternalCompilerInterface.UnsafeGetChunkNativeArrayIntPtr(chunk, __moverTypeHandle);
            int count = chunk.Count;
            for (int i = 0; i != count; i++)
            {
                OriginalLambdaBody(InternalCompilerInterface.UnsafeGetCopyOfNativeArrayPtrElement<Entity>(entityPtr, i),
                    ref InternalCompilerInterface.UnsafeGetRefToNativeArrayPtrElement<MoverCD>(moverPtr, i));
            }
        }

        public uint seed;

        public NativeParallelMultiHashMap<int2, Entity> moveeAtPosition;

        public NativeParallelHashMap<int2, Entity> storageAtPosition;

        [ReadOnly] public ComponentDataFromEntity<CraftingCD> craftingLookup;
        
        [ReadOnly] public ComponentDataFromEntity<SeedExtractorCD> seedExtractorLookup;

        public BufferFromEntity<ContainedObjectsBuffer> containerLookup;

        [ReadOnly] public TileAccessor tileLookup;

        [ReadOnly] public EntityTypeHandle __entityTypeHandle;

        public ComponentTypeHandle<MoverCD> __moverTypeHandle;

        [ReadOnly] public ComponentDataFromEntity<BigEntityRefCD> __PugAutomation_BigEntityRefCD_FromEntity;

        [ReadOnly] public ComponentDataFromEntity<PickUpObjectCD> __PickUpObjectCD_FromEntity;

        public ComponentDataFromEntity<MoveeCD> __PugAutomation_MoveeCD_FromEntity;
    }
}