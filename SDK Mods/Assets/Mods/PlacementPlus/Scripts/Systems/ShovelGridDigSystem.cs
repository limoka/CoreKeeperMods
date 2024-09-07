using PlacementPlus.Components;
using PlayerCommand;
using PlayerState;
using PugTilemap;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.NetCode;

namespace PlacementPlus.Systems
{
    [UpdateBefore(typeof(TileDamageSystem))]
    [UpdateBefore(typeof(UpdateHealthSystemGroup))]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation)]
    public partial class ShovelGridDigSystem : PugSimulationSystemBase
    {
        private DigUpdateJob _digJob;

        protected override void OnCreate()
        {
            RequireForUpdate<BeginSimulationEntityCommandBufferSystem.Singleton>();
            RequireForUpdate<TileWithTilesetToObjectDataMapCD>();
            RequireForUpdate<SubMapRegistry>();
            RequireForUpdate<TilePriorityLookupSingleton>();

            DigUpdateJob digUpdateJob = default;
            digUpdateJob.shared = new PlayerAttackShared(ref CheckedStateRef);
            digUpdateJob.lookups = new PlayerAttackLookups(ref CheckedStateRef);
            _digJob = digUpdateJob;

            base.OnCreate();
        }


        protected override void OnStartRunning()
        {
            _digJob.shared.Init(ref CheckedStateRef);
            _digJob.tileWithTilesetToObjectDataMap = SystemAPI.GetSingleton<TileWithTilesetToObjectDataMapCD>();
            base.OnStartRunning();
        }

        protected override void OnUpdate()
        {
            _digJob.lookups.Update(ref CheckedStateRef);
            EntityCommandBuffer entityCommandBuffer = CreateCommandBuffer();

            var players =
                SystemAPI
                    .QueryBuilder()
                    .WithAll<PlayerGhost>()
                    .Build()
                    .ToEntityListAsync(CheckedStateRef.WorldUpdateAllocator, out JobHandle jobHandle);
            _digJob.shared.Update(ref CheckedStateRef, entityCommandBuffer, players);

            _digJob.Schedule(jobHandle);
            base.OnUpdate();
        }

        public partial struct DigUpdateJob : IJobEntity
        {
            public PlayerAttackLookups lookups;
            public PlayerAttackShared shared;

            [ReadOnly] public TileWithTilesetToObjectDataMapCD tileWithTilesetToObjectDataMap;

            public void Execute(
                PlayerAttackAspect attackAspect,
                DynamicBuffer<ShovelDigQueueBuffer> digQueue
            )
            {
                if (lookups.playerStateLookup[attackAspect.entity].HasAnyState(PlayerStateEnum.Death)) return;
                if (digQueue.Length == 0) return;

                var targets = digQueue.ToNativeArray(Allocator.Temp);
                digQueue.Clear();
                
                int diggingDamage = EntityUtility.GetConditionEffectValue(ConditionEffect.Digging, attackAspect.entity, lookups.summarizeConiditionsEffectsLookup);

                for (int i = 0; i < targets.Length; i++)
                {
                    var target = targets[i];
                    DigUpAtPosition(attackAspect, target, diggingDamage);
                }
            }

            private void DigUpAtPosition(
                PlayerAttackAspect attackAspect,
                ShovelDigQueueBuffer target,
                int diggingDamage
            )
            {
                bool isGodMode = lookups.godModeLookup.IsComponentEnabled(attackAspect.entity);

                if (target.entity != Entity.Null)
                {
                    PlayerController.GetTileDamageValues(
                        attackAspect,
                        shared,
                        lookups,
                        target.entity,
                        target.position,
                        default,
                        default,
                        diggingDamage,
                        out float num,
                        out int _,
                        out int _,
                        false,
                        true);
                    
                    float3 positionFloat = target.position.ToFloat3();
                    ClientSystem.DealDamageToEntity(
                        attackAspect,
                        shared,
                        lookups,
                        target.entity,
                        positionFloat,
                        false,
                        Entity.Null,
                        default,
                        default,
                        diggingDamage,
                        false,
                        false,
                        attackAspect.entity,
                        positionFloat,
                        out int _,
                        out int _,
                        out int num4,
                        out bool _,
                        out bool _,
                        out bool _,
                        out bool _,
                        out bool _,
                        true,
                        false,
                        true,
                        false,
                        false,
                        isGodMode
                    );

                    PlayerController.DoImmediateTileDamageEffects(
                        attackAspect,
                        shared,
                        lookups,
                        target.tileset,
                        target.tileType,
                        target.position.ToInt3(),
                        num,
                        num4,
                        target.entity
                    );
                    return;
                }

                ObjectDataCD objectDataCD = default;

                if (shared.tileAccessor.GetType(target.position, TileType.ground, out TileCD resTile))
                    objectDataCD = PugDatabase.TryGetTileItemInfo(TileType.ground, (Tileset)resTile.tileset, tileWithTilesetToObjectDataMap);

                if (objectDataCD.objectID == ObjectID.None) return;

                Entity primaryPrefabEntity = PugDatabase.GetPrimaryPrefabEntity(
                    objectDataCD.objectID,
                    shared.databaseBank.databaseBankBlob,
                    objectDataCD.variation
                );

                PlayerController.GetTileDamageValues(
                    attackAspect,
                    shared,
                    lookups,
                    primaryPrefabEntity,
                    target.position,
                    default,
                    default,
                    diggingDamage,
                    out float _,
                    out int damageDone,
                    out int _,
                    false,
                    true
                );

                if (!shared.worldInfo.IsWorldModeEnabled(WorldMode.Creative) && damageDone == int.MaxValue) return;

                lookups.tileDamageBufferLookup[shared.tileDamageBufferSingleton].Add(new TileDamageBuffer
                {
                    position = target.position,
                    damage = damageDone,
                    causedByEntity = attackAspect.entity,
                    canHitGround = true,
                    canHitLowColliders = true,
                    pullAnyLootToPlayer = true,
                    damagedByExplosion = false,
                    bypassMaxDamagePerHit = isGodMode,
                    skipWallAndRootsLootDropOnDestroy = isGodMode,
                    dontPlayDamageTileEffect = true
                });

                ref GhostEffectEventBufferPointerCD valueRW =
                    ref lookups.ghostEffectEventBufferPointerLookup.GetRefRW(attackAspect.entity).ValueRW;
                DynamicBuffer<GhostEffectEventBuffer> dynamicBuffer = lookups.ghostEffectEventBufferLookup[attackAspect.entity];

                var ghostEffectEventBuffer = new GhostEffectEventBuffer
                {
                    Tick = shared.currentTick,
                    value = new EffectEventCD
                    {
                        effectID = EffectID.DamageTile,
                        position1 = new float3(target.position.x, 1f, target.position.y),
                        value1 = resTile.tileset,
                        tileInfo = new TileInfo
                        {
                            tileset = resTile.tileset,
                            tileType = TileType.ground
                        }
                    }
                };

                dynamicBuffer.AddToRingBuffer(ref valueRW, ghostEffectEventBuffer);
            }
        }
    }
}