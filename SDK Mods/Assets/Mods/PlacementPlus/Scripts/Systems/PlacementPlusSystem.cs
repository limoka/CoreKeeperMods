using CoreLib.Util.Extensions;
using Inventory;
using Mods.PlacementPlus.Scripts.Util;
using PlacementPlus.Access;
using PlacementPlus.Components;
using PlayerCommand;
using PlayerEquipment;
using PlayerState;
using PugProperties;
using PugTilemap;
using PugTilemap.Quads;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using Random = Unity.Mathematics.Random;
using SphereCollider = Unity.Physics.SphereCollider;

namespace PlacementPlus.Systems
{
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(EquipmentUpdateSystemGroup))]
    [UpdateBefore(typeof(EquipmentUpdateSystem))]
    public partial class PlacementPlusSystem : PugSimulationSystemBase
    {
        private uint _tickRate;

        private EntityArchetype _achievementArchetype;

        protected override void OnCreate()
        {
            _tickRate = (uint)NetworkingManager.GetSimulationTickRateForPlatform();
            _achievementArchetype = AchievementSystem.GetRpcArchetype(EntityManager);

            RequireForUpdate<PhysicsWorldSingleton>();
            RequireForUpdate<WorldInfoCD>();
            RequireForUpdate<TileWithTilesetToObjectDataMapCD>();

            NeedDatabase();
            base.OnCreate();
        }

        protected override void OnUpdate()
        {
            var worldInfoCD = SystemAPI.GetSingleton<WorldInfoCD>();
            var networkTime = SystemAPI.GetSingleton<NetworkTime>();
            var currentTick = networkTime.ServerTick;

            var databaseBank = SystemAPI.GetSingleton<PugDatabase.DatabaseBankCD>();

            var cooldownLookup = GetComponentLookup<CooldownCD>(true);
            var ecb = CreateCommandBuffer();
            var tileAccessor = CreateTileAccessor();

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

            var equipmentShared = new EquipmentUpdateSharedData
            {
                currentTick = currentTick,
                databaseBank = databaseBank,
                worldInfoCD = worldInfoCD,
                tickRate = _tickRate,
                physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld,
                physicsWorldHistory = SystemAPI.GetSingleton<PhysicsWorldHistorySingleton>(),
                inventoryUpdateBufferEntity = SystemAPI.GetSingletonEntity<InventoryChangeBuffer>(),
                tileUpdateBufferEntity = SystemAPI.GetSingletonEntity<TileUpdateBuffer>(),
                tileAccessor = tileAccessor,
                tileWithTilesetToObjectDataMapCD = SystemAPI.GetSingleton<TileWithTilesetToObjectDataMapCD>(),
                colliderCacheCD = SystemAPI.GetSingleton<ColliderCacheCD>(),
                isServer = isServer,
                ecb = ecb,
                isFirstTimeFullyPredictingTick = networkTime.IsFirstTimeFullyPredictingTick,
                achievementArchetype = _achievementArchetype
            };

            var lookupData = new LookupEquipmentUpdateData
            {
                secondaryUseLookup = SystemAPI.GetComponentLookup<SecondaryUseCD>(),
                cooldownLookup = SystemAPI.GetComponentLookup<CooldownCD>(),
                consumeManaLookup = SystemAPI.GetComponentLookup<ConsumesManaCD>(),
                levelLookup = SystemAPI.GetComponentLookup<LevelCD>(),
                levelEntitiesLookup = SystemAPI.GetBufferLookup<LevelEntitiesBuffer>(),
                parchementRecipeLookup = SystemAPI.GetComponentLookup<ParchmentRecipeCD>(),
                objectDataLookup = SystemAPI.GetComponentLookup<ObjectDataCD>(),
                attackWithEquipmentLookup = SystemAPI.GetComponentLookup<AttackWithEquipmentTag>(),
                inventoryUpdateBuffer = SystemAPI.GetBufferLookup<InventoryChangeBuffer>(),
                cattleLookup = SystemAPI.GetComponentLookup<CattleCD>(),
                petCandyLookup = SystemAPI.GetComponentLookup<PetCandyCD>(),
                potionLookup = SystemAPI.GetComponentLookup<PotionCD>(),
                localTransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(),
                petLookup = SystemAPI.GetComponentLookup<PetCD>(),
                playAnimationStateLookup = SystemAPI.GetComponentLookup<PlayAnimationStateCD>(),
                simulateLookup = SystemAPI.GetComponentLookup<Simulate>(),
                waitingForEatableSlotConsumeResultLookup = SystemAPI.GetComponentLookup<WaitingForEatableSlotConsumeResultCD>(),
                tileUpdateBufferLookup = SystemAPI.GetBufferLookup<TileUpdateBuffer>(),
                tileLookup = SystemAPI.GetComponentLookup<TileCD>(),
                objectPropertiesLookup = SystemAPI.GetComponentLookup<ObjectPropertiesCD>(),
                adaptiveEntityBufferLookup = SystemAPI.GetBufferLookup<AdaptiveEntityBuffer>(),
                directionBasedOnVariationLookup = SystemAPI.GetComponentLookup<DirectionBasedOnVariationCD>(),
                directionLookup = SystemAPI.GetComponentLookup<DirectionCD>(),
                playerGhostLookup = SystemAPI.GetComponentLookup<PlayerGhost>(),
                minionLookup = SystemAPI.GetComponentLookup<MinionCD>(),
                indestructibleLookup = SystemAPI.GetComponentLookup<IndestructibleCD>(),
                plantLookup = SystemAPI.GetComponentLookup<PlantCD>(),
                critterLookup = SystemAPI.GetComponentLookup<CritterCD>(),
                fireflyLookup = SystemAPI.GetComponentLookup<FireflyCD>(),
                requiresDrillLookup = SystemAPI.GetComponentLookup<RequiresDrillCD>(),
                surfacePriorityLookup = SystemAPI.GetComponentLookup<SurfacePriorityCD>(),
                electricityLookup = SystemAPI.GetComponentLookup<ElectricityCD>(),
                eventTerminalLookup = SystemAPI.GetComponentLookup<EventTerminalCD>(),
                waterSourceLookup = SystemAPI.GetComponentLookup<WaterSourceCD>(),
                paintToolLookup = SystemAPI.GetComponentLookup<PaintToolCD>(),
                paintableObjectLookup = SystemAPI.GetComponentLookup<PaintableObjectCD>(),
                growingLookup = SystemAPI.GetComponentLookup<GrowingCD>(),
                healthLookup = SystemAPI.GetComponentLookup<HealthCD>(),
                summarizedConditionsBufferLookup = SystemAPI.GetBufferLookup<SummarizedConditionsBuffer>(),
                reduceDurabilityOfEquippedTagLookup = SystemAPI.GetComponentLookup<ReduceDurabilityOfEquippedTriggerCD>(),
                summarizedConditionEffectsBufferLookup = SystemAPI.GetBufferLookup<SummarizedConditionEffectsBuffer>(),
                entityDestroyedLookup = SystemAPI.GetComponentLookup<EntityDestroyedCD>(),
                dontDropSelfLookup = SystemAPI.GetComponentLookup<DontDropSelfCD>(),
                dontDropLootLookup = SystemAPI.GetComponentLookup<DontDropLootCD>(),
                killedByPlayerLookup = SystemAPI.GetComponentLookup<KilledByPlayerCD>(),
                destructibleLookup = SystemAPI.GetComponentLookup<DestructibleObjectCD>(),
                canBeRemovedByWaterLookup = SystemAPI.GetComponentLookup<CanBeRemovedByWaterCD>(),
                groundDecorationLookup = SystemAPI.GetComponentLookup<GroundDecorationCD>(),
                diggableLookup = SystemAPI.GetComponentLookup<DiggableCD>(),
                pseudoTileLookup = SystemAPI.GetComponentLookup<PseudoTileCD>(),
                dontBlockDiggingLookup = SystemAPI.GetComponentLookup<DontBlockDiggingCD>(),
                fullnessLookup = SystemAPI.GetComponentLookup<FullnessCD>(),
                godModeLookup = SystemAPI.GetComponentLookup<GodModeCD>(),
                containedObjectsBufferLookup = SystemAPI.GetBufferLookup<ContainedObjectsBuffer>(),
                anvilLookup = SystemAPI.GetComponentLookup<AnvilCD>(),
                waypointLookup = SystemAPI.GetComponentLookup<WayPointCD>(),
                craftingLookup = SystemAPI.GetComponentLookup<CraftingCD>(),
                triggerAnimationOnDeathLookup = SystemAPI.GetComponentLookup<TriggerAnimationOnDeathCD>(),
                moveToPredictedByEntityDestroyedLookup = SystemAPI.GetComponentLookup<MoveToPredictedByEntityDestroyedCD>(),
                hasExplodedLookup = SystemAPI.GetComponentLookup<HasExplodedCD>()
            };

            var damageReductionLookup = SystemAPI.GetComponentLookup<DamageReductionCD>();
            
            Entities.ForEach((
                    EquipmentUpdateAspect equipmentAspect,
                    DynamicBuffer<ShovelDigQueueBuffer> digQueue,
                    in ClientInput clientInput,
                    in PlacementPlusState state,
                    in CommandDataInterpolationDelay interpolationDelay
                ) =>
                {
                    if ((state.size == 0 || state.mode == BrushMode.NONE) && !state.replaceTiles) return;

                    bool interactHeldRaw = clientInput.IsButtonSet(CommandInputButtonNames.Interact_HeldDown);
                    bool secondInteractHeldRaw = clientInput.IsButtonSet(CommandInputButtonNames.SecondInteract_HeldDown);
                    if (!PlayerController.CurrentStateAllowInteractions(
                            worldInfoCD, equipmentAspect.playerGhost.ValueRO,
                            equipmentAspect.playerStateCD.ValueRO,
                            equipmentAspect.equipmentSlotCD.ValueRO, secondInteractHeldRaw && !interactHeldRaw, clientInput))
                    {
                        return;
                    }

                    bool interactHeld = interactHeldRaw | equipmentAspect.equipmentSlotCD.ValueRW.interactIsPendingToBeUsed;
                    bool secondInteractHeld = secondInteractHeldRaw | equipmentAspect.equipmentSlotCD.ValueRW.secondInteractIsPendingToBeUsed;

                    bool onCooldown = EquipmentSlot.IsItemOnCooldown(
                        equipmentAspect.equippedObjectCD.ValueRO,
                        databaseBank,
                        cooldownLookup,
                        equipmentAspect.syncedSharedCooldownTimers,
                        equipmentAspect.localPlayerSharedCooldownTimers, currentTick);
                    if (onCooldown) return;

                    var slotType = equipmentAspect.equipmentSlotCD.ValueRO.slotType;

                    if (slotType == EquipmentSlotType.PlaceObjectSlot ||
                        slotType == (EquipmentSlotType)100)
                    {
                        var success = ObjectPlacementLogic.UpdatePlaceObjectPlus(
                            equipmentAspect,
                            equipmentShared,
                            lookupData,
                            givesConditionsLookup,
                            damageReductionLookup,
                            state,
                            secondInteractHeld
                        );
                        if (!success) return;

                        equipmentAspect.equipmentSlotCD.ValueRW.secondInteractIsPendingToBeUsed = false;
                    }
                    else if (slotType == EquipmentSlotType.ShovelSlot ||
                             slotType == (EquipmentSlotType)101)
                    {
                        var success = ShovelLogic.UpdateShovelPlus(
                            equipmentAspect,
                            equipmentShared,
                            lookupData,
                            interpolationDelay,
                            digQueue,
                            state,
                            secondInteractHeld);
                        if (!success) return;

                        equipmentAspect.equipmentSlotCD.ValueRW.secondInteractIsPendingToBeUsed = false;
                    }
                })
                .WithoutBurst()
                .Schedule();

            base.OnUpdate();
        }
    }
}