using CommandMinion;
using Inventory;
using PlacementPlus;
using PlayerEquipment;
using Pug.Automation;
using Pug.Properties;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Physics;
using Unity.Transforms;

namespace Mods.PlacementPlus.Scripts.Util
{
    internal static class Helpers
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

                return condition.equipmentCondition.value;
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

            bool isReinforced = PugDatabase.HasComponent<DurabilityCD>(item) &&
                                PugDatabase.GetComponent<DurabilityCD>(item).IsReinforced(item.amount);

            foreach (GivesConditionsWhenEquippedBuffer condition in conditionBuffer)
            {
                if (condition.equipmentCondition.id != ConditionID.MiningIncrease) continue;
                var value = condition.equipmentCondition.value;

                var bonus = 0;

                if (isReinforced)
                    bonus = ConditionExtensions.GetReinforcedBonusValue(value,
                        new ConditionInfo { Id = ConditionID.MiningIncrease });

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

        internal static bool TryBuildSharedData(
            ref SystemState state, 
            EntityCommandBuffer ecb,
            out EquipmentUpdateSharedData sharedData,
            TileAccessor tileAccessor,
            EntityArchetype achievementArchetype
            )
        {
            sharedData = default;
            var networkTimeQuery = state.GetEntityQuery(ComponentType.ReadOnly<NetworkTime>());
            if (!networkTimeQuery.TryGetSingleton<NetworkTime>(out var networkTime)) return false;

            sharedData = new EquipmentUpdateSharedData
            {
                currentTick = networkTime.ServerTick,
                databaseBank = state.GetEntityQuery(ComponentType.ReadOnly<PugDatabase.DatabaseBankCD>())
                    .GetSingleton<PugDatabase.DatabaseBankCD>(),
                worldInfoCD = state.GetEntityQuery(ComponentType.ReadOnly<WorldInfoCD>())
                    .GetSingleton<WorldInfoCD>(),
                tickRate = (uint)PlatformConfiguration.Instance.SessionConfiguration.SimulationTickRate,
                physicsWorld = state.GetEntityQuery(ComponentType.ReadOnly<PhysicsWorldSingleton>())
                    .GetSingleton<PhysicsWorldSingleton>().PhysicsWorld,
                physicsWorldHistory = state.GetEntityQuery(ComponentType.ReadOnly<PhysicsWorldHistorySingleton>())
                    .GetSingleton<PhysicsWorldHistorySingleton>(),
                // GetSingletonEntity throws an exception on failure — if PugMod's ISystem wrapper 
                // silently swallows the exception, the system will break every tick without any logs 
                // (Round 6: suspected as the primary cause for why suppression was neutralized without a sound).
                inventoryUpdateBufferEntity =
                    GetSingletonEntitySafe<InventoryChangeBuffer>(ref state, "InventoryChangeBuffer"),
                tileUpdateBufferEntity = GetSingletonEntitySafe<TileUpdateBuffer>(ref state, "TileUpdateBuffer"),
                tileAccessor = tileAccessor,
                tileWithTilesetToObjectDataMapCD = state
                    .GetEntityQuery(ComponentType.ReadOnly<TileWithTilesetToObjectDataMapCD>())
                    .GetSingleton<TileWithTilesetToObjectDataMapCD>(),
                colliderCacheCD = state.GetEntityQuery(ComponentType.ReadOnly<ColliderCacheCD>())
                    .GetSingleton<ColliderCacheCD>(),
                isServer = true,
                ecb = ecb,
                isFirstTimeFullyPredictingTick = networkTime.IsFirstTimeFullyPredictingTick,
                achievementArchetype = achievementArchetype
            };
            return true;
        }
        
        // ⚠️ Do not use typeof(T).Name — The safety validation check rejects System.Reflection references, 
        // causing the entire mod compilation to fail (Observed on 2026-08-23). 
        // The label must be provided as a string by the caller.
        private static Entity GetSingletonEntitySafe<T>(ref SystemState state, string label) where T : unmanaged, IBufferElementData
        {
            var query = state.GetEntityQuery(ComponentType.ReadWrite<T>());
            if (!query.TryGetSingletonEntity<T>(out Entity e))
            {
                PlacementPlusMod.Log.LogWarning($"Singleton entity not found: {label}");
                return Entity.Null;
            }
            return e;
        }


        internal static LookupEquipmentUpdateData BuildLookupData(ref SystemState state)
        {
            return new LookupEquipmentUpdateData
            {
                secondaryUseLookup = state.GetComponentLookup<SecondaryUseCD>(),
                cooldownLookup = state.GetComponentLookup<CooldownCD>(),
                warmupLookup = state.GetComponentLookup<WarmupCD>(),
                consumeManaLookup = state.GetComponentLookup<ConsumesManaCD>(),
                levelLookup = state.GetComponentLookup<LevelCD>(),
                levelEntitiesLookup = state.GetBufferLookup<LevelEntitiesBuffer>(),
                parchementRecipeLookup = state.GetComponentLookup<RecipeCD>(),
                objectDataLookup = state.GetComponentLookup<ObjectDataCD>(),
                attackWithEquipmentLookup = state.GetComponentLookup<AttackWithEquipmentTag>(),
                inventoryUpdateBuffer = state.GetBufferLookup<InventoryChangeBuffer>(),
                cattleLookup = state.GetComponentLookup<CattleCD>(),
                petCandyLookup = state.GetComponentLookup<PetCandyCD>(),
                potionLookup = state.GetComponentLookup<PotionCD>(),
                localTransformLookup = state.GetComponentLookup<LocalTransform>(),
                petLookup = state.GetComponentLookup<PetCD>(),
                playAnimationStateLookup = state.GetComponentLookup<PlayAnimationStateCD>(),
                simulateLookup = state.GetComponentLookup<Simulate>(),
                waitingForEatableSlotConsumeResultLookup =
                    state.GetComponentLookup<WaitingForEatableSlotConsumeResultCD>(),
                tileUpdateBufferLookup = state.GetBufferLookup<TileUpdateBuffer>(),
                tileLookup = state.GetComponentLookup<TileCD>(),
                objectPropertiesLookup = state.GetComponentLookup<ObjectPropertiesCD>(),
                adaptiveEntityBufferLookup = state.GetBufferLookup<AdaptiveEntityBuffer>(),
                directionBasedOnVariationLookup = state.GetComponentLookup<DirectionBasedOnVariationCD>(),
                directionLookup = state.GetComponentLookup<DirectionCD>(),
                sizeVariationLookup = state.GetComponentLookup<ResizableTileSizeCD>(),
                playerGhostLookup = state.GetComponentLookup<PlayerGhost>(),
                minionLookup = state.GetComponentLookup<MinionCD>(),
                indestructibleLookup = state.GetComponentLookup<IndestructibleCD>(),
                plantLookup = state.GetComponentLookup<PlantCD>(),
                critterLookup = state.GetComponentLookup<CritterCD>(),
                fireflyLookup = state.GetComponentLookup<FireflyCD>(),
                requiresDrillLookup = state.GetComponentLookup<RequiresDrillCD>(),
                surfacePriorityLookup = state.GetComponentLookup<SurfacePriorityCD>(),
                electricityLookup = state.GetComponentLookup<ElectricityCD>(),
                eventTerminalLookup = state.GetComponentLookup<EventTerminalCD>(),
                waterSourceLookup = state.GetComponentLookup<WaterSourceCD>(),
                paintToolLookup = state.GetComponentLookup<PaintToolCD>(),
                paintableObjectLookup = state.GetComponentLookup<PaintableObjectCD>(),
                growingLookup = state.GetComponentLookup<GrowingCD>(),
                healthLookup = state.GetComponentLookup<HealthCD>(),
                summarizedConditionsBufferLookup = state.GetBufferLookup<SummarizedConditionsBuffer>(),
                reduceDurabilityOfEquippedTagLookup = state.GetComponentLookup<ReduceDurabilityOfEquippedTriggerCD>(),
                summarizedConditionEffectsBufferLookup = state.GetBufferLookup<SummarizedConditionEffectsBuffer>(),
                entityDestroyedLookup = state.GetComponentLookup<EntityDestroyedCD>(),
                dontDropSelfLookup = state.GetComponentLookup<DontDropSelfCD>(),
                dontDropLootLookup = state.GetComponentLookup<DontDropLootCD>(),
                killedByPlayerLookup = state.GetComponentLookup<KilledByPlayerCD>(),
                destructibleLookup = state.GetComponentLookup<DestructibleObjectCD>(),
                canBeRemovedByWaterLookup = state.GetComponentLookup<CanBeRemovedByWaterCD>(),
                groundDecorationLookup = state.GetComponentLookup<GroundDecorationCD>(),
                diggableLookup = state.GetComponentLookup<DiggableCD>(),
                pseudoTileLookup = state.GetComponentLookup<PseudoTileCD>(),
                dontBlockDiggingLookup = state.GetComponentLookup<DontBlockDiggingCD>(),
                fullnessLookup = state.GetComponentLookup<FullnessCD>(),
                godModeLookup = state.GetComponentLookup<GodModeCD>(),
                containedObjectsBufferLookup = state.GetBufferLookup<ContainedObjectsBuffer>(),
                inventoryBufferLookup = state.GetBufferLookup<InventoryBuffer>(),
                anvilLookup = state.GetComponentLookup<AnvilCD>(),
                waypointLookup = state.GetComponentLookup<WayPointCD>(),
                craftingLookup = state.GetComponentLookup<CraftingCD>(),
                proximityTriggerLookup = state.GetComponentLookup<ProximityTriggerCD>(),
                commandMinionLookup = state.GetComponentLookup<CommandMinionWeaponCD>(),
                rootPlantLookup = state.GetComponentLookup<RootPlantCD>(),
                triggerSelectNewEnemyToAttackCommandLookup =
                    state.GetComponentLookup<TriggerSelectEnemyToAttackForMinionCommandCD>(),
                triggerAnimationOnDeathLookup = state.GetComponentLookup<TriggerAnimationOnDeathCD>(),
                moveToPredictedByEntityDestroyedLookup = state.GetComponentLookup<MoveToPredictedByEntityDestroyedCD>(),
                hasExplodedLookup = state.GetComponentLookup<HasExplodedCD>()
            };
        }
    }
}