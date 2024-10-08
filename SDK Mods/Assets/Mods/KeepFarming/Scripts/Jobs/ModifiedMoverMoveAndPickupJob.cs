﻿using System;
using KeepFarming.Components;
using PugAutomation;
using PugTilemap;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Internal;
using Unity.Mathematics;
using Random = Unity.Mathematics.Random;

namespace Mods.KeepFarming.Scripts.Jobs
{
   /* public struct ModifiedMoverMoveAndPickupJob : IJobChunk
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
        
        public void Execute(in ArchetypeChunk chunk, int batchIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            IntPtr intPtr = InternalCompilerInterface.UnsafeGetChunkEntityArrayIntPtr(chunk, __entityTypeHandle);
            IntPtr intPtr2 = InternalCompilerInterface.UnsafeGetChunkNativeArrayIntPtr(chunk, ref __moverTypeHandle);
            int count = chunk.Count;
            if (!useEnabledMask)
            {
                for (int i = 0; i < count; i++)
                {
                    OriginalLambdaBody(InternalCompilerInterface.UnsafeGetCopyOfNativeArrayPtrElement<Entity>(intPtr, i),
                        ref InternalCompilerInterface.UnsafeGetRefToNativeArrayPtrElement<MoverCD>(intPtr2, i));
                }

                return;
            }

            if (math.countbits(chunkEnabledMask.ULong0 ^ (chunkEnabledMask.ULong0 << 1)) +
                math.countbits(chunkEnabledMask.ULong1 ^ (chunkEnabledMask.ULong1 << 1)) - 1 <= 4)
            {
                int j = 0;
                int num = 0;
                while (EnabledBitUtility.TryGetNextRange(chunkEnabledMask, num, out j, out num))
                {
                    while (j < num)
                    {
                        OriginalLambdaBody(InternalCompilerInterface.UnsafeGetCopyOfNativeArrayPtrElement<Entity>(intPtr, j),
                            ref InternalCompilerInterface.UnsafeGetRefToNativeArrayPtrElement<MoverCD>(intPtr2, j));
                        j++;
                    }
                }

                return;
            }

            ulong num2 = chunkEnabledMask.ULong0;
            int num3 = math.min(64, count);
            for (int k = 0; k < num3; k++)
            {
                if ((num2 & 1UL) != 0UL)
                {
                    OriginalLambdaBody(InternalCompilerInterface.UnsafeGetCopyOfNativeArrayPtrElement<Entity>(intPtr, k),
                        ref InternalCompilerInterface.UnsafeGetRefToNativeArrayPtrElement<MoverCD>(intPtr2, k));
                }

                num2 >>= 1;
            }

            num2 = chunkEnabledMask.ULong1;
            for (int l = 64; l < count; l++)
            {
                if ((num2 & 1UL) != 0UL)
                {
                    OriginalLambdaBody(InternalCompilerInterface.UnsafeGetCopyOfNativeArrayPtrElement<Entity>(intPtr, l),
                        ref InternalCompilerInterface.UnsafeGetRefToNativeArrayPtrElement<MoverCD>(intPtr2, l));
                }

                num2 >>= 1;
            }
        }

        void IJobChunk.Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            this.Execute(chunk, unfilteredChunkIndex, useEnabledMask, chunkEnabledMask);
        }

        public uint seed;

        public NativeParallelMultiHashMap<int2, Entity> moveeAtPosition;

        public NativeParallelHashMap<int2, Entity> storageAtPosition;

        [ReadOnly] public ComponentLookup<CraftingCD> craftingLookup;

        [ReadOnly] public ComponentLookup<SeedExtractorCD> seedExtractorLookup;

        public BufferLookup<ContainedObjectsBuffer> containerLookup;

        [ReadOnly] public TileAccessor tileLookup;

        [ReadOnly] public EntityTypeHandle __entityTypeHandle;

        public ComponentTypeHandle<MoverCD> __moverTypeHandle;

        [ReadOnly] public ComponentLookup<BigEntityRefCD> __PugAutomation_BigEntityRefCD_FromEntity;

        [ReadOnly] public ComponentLookup<PickUpObjectCD> __PickUpObjectCD_FromEntity;

        public ComponentLookup<MoveeCD> __PugAutomation_MoveeCD_FromEntity;
    }*/
}