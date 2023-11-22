using System.Runtime.CompilerServices;
using PugTilemap;
using PugTilemap.Quads;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace Mods.CustomizeWaterPriority.Scripts
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateInWorld(TargetWorld.Server)]
    public partial class CustomWaterSpredingSystem : PugSimulationSystemBase
    {
        private bool hasRunAtLeastOnce;

        private EntityQuery query;
        private EntityQuery query_1;

        protected override void OnCreate()
        {
            NeedDatabase();
            NeedTileUpdateBuffer();
            RequireSingletonForUpdate<EffectEventBuffer>();
            RequireForUpdate(query);

            query_1 = GetEntityQuery(typeof(EffectEventBuffer));

            base.OnCreate();
        }

        protected override void OnUpdate()
        {
            if (WorldInfo.simulationDisabled && hasRunAtLeastOnce)
            {
                base.OnUpdate();
                return;
            }

            hasRunAtLeastOnce = true;

            var ecb = CreateCommandBuffer();
            
            var createdPositions = new NativeParallelHashSet<int2>(1024, World.UpdateAllocator.ToAllocator);
            TileAccessor tileLookup = CreateTileAccessor();
            
            Entity updatedTilesSingletonLocal = tileUpdateBufferSingletonEntity;
            Entity effectEventBufferEntity = query_1.GetSingletonEntity();
            double elapsedTime = Time.ElapsedTime;
            Random rng = PugRandom.GetRng();

            int highestPrio = (int)CustomizeWaterPriorityMod.highestPriorityTileset.Value;

            Entities.ForEach((Entity entity, ref WaterSpreaderCD waterSpreaderCD) =>
                {
                    if (!waterSpreaderCD.timer.isRunning)
                    {
                        waterSpreaderCD.timer.Start(elapsedTime, GetRandomSpreadDelay(ref rng));
                    }

                    if (!waterSpreaderCD.timer.IsTimerElapsed(elapsedTime))
                    {
                        return;
                    }

                    int2 position = waterSpreaderCD.position;
                    
                    bool type = tileLookup.GetType(position, TileType.water, out TileCD tileCD);
                    if (!type && !tileLookup.HasType(position, TileType.pit))
                    {
                        ecb.DestroyEntity(entity);
                        return;
                    }

                    int2 @int = position + AdjacentDir.GetInt2(64);
                    int2 int2 = position + AdjacentDir.GetInt2(4);
                    int2 int3 = position + AdjacentDir.GetInt2(16);
                    int2 int4 = position + AdjacentDir.GetInt2(1);
                    
                    bool type2 = tileLookup.GetType(@int, TileType.water, out TileCD tileCD2);
                    bool type3 = tileLookup.GetType(int2, TileType.water, out TileCD tileCD3);
                    bool type4 = tileLookup.GetType(int3, TileType.water, out TileCD tileCD4);
                    bool type5 = tileLookup.GetType(int4, TileType.water, out TileCD tileCD5);
                    
                    bool flag = type2 || (type3 || type4) || type5;
                    if (!tileLookup.HasType(position, TileType.greatWall))
                    {
                        int num = (type ? tileCD.tileset : (-1));
                        int num2 = num;
                        if (type2)
                        {
                            num2 = GetHighestPrioWater(num2, tileCD2.tileset, highestPrio);
                        }

                        if (type3)
                        {
                            num2 = GetHighestPrioWater(num2, tileCD3.tileset, highestPrio);
                        }

                        if (type4)
                        {
                            num2 = GetHighestPrioWater(num2, tileCD4.tileset, highestPrio);
                        }

                        if (type5)
                        {
                            num2 = GetHighestPrioWater(num2, tileCD5.tileset, highestPrio);
                        }

                        if (flag && (!type || Tileset1HasHigherPrio(num2, tileCD.tileset, highestPrio)))
                        {
                            if (!createdPositions.Contains(position))
                            {
                                createdPositions.Add(position);
                                SpreadToPosition(ecb, position, tileCD.tileType, tileCD.tileset, num2,
                                    updatedTilesSingletonLocal, effectEventBufferEntity);
                            }
                        }
                        else if (type)
                        {
                            if (!createdPositions.Contains(@int) && ((type2 && Tileset1HasHigherPrio(num, tileCD2.tileset, highestPrio)) ||
                                                                          (!type2 && tileLookup.HasType(@int, TileType.pit))))
                            {
                                createdPositions.Add(@int);
                                SpreadToPosition(ecb, @int, tileCD2.tileType, tileCD2.tileset, tileCD.tileset,
                                    updatedTilesSingletonLocal, effectEventBufferEntity);
                            }

                            if (!createdPositions.Contains(int2) && ((type3 && Tileset1HasHigherPrio(num, tileCD3.tileset, highestPrio)) ||
                                                                          (!type3 && tileLookup.HasType(int2, TileType.pit))))
                            {
                                createdPositions.Add(int2);
                                SpreadToPosition(ecb, int2, tileCD3.tileType, tileCD3.tileset, tileCD.tileset,
                                    updatedTilesSingletonLocal, effectEventBufferEntity);
                            }

                            if (!createdPositions.Contains(int3) && ((type4 && Tileset1HasHigherPrio(num, tileCD4.tileset, highestPrio)) ||
                                                                          (!type4 && tileLookup.HasType(int3, TileType.pit))))
                            {
                                createdPositions.Add(int3);
                                SpreadToPosition(ecb, int3, tileCD4.tileType, tileCD4.tileset, tileCD.tileset,
                                    updatedTilesSingletonLocal, effectEventBufferEntity);
                            }

                            if (!createdPositions.Contains(int4) && ((type5 && Tileset1HasHigherPrio(num, tileCD5.tileset, highestPrio)) ||
                                                                          (!type5 && tileLookup.HasType(int4, TileType.pit))))
                            {
                                createdPositions.Add(int4);
                                SpreadToPosition(ecb, int4, tileCD5.tileType, tileCD5.tileset, tileCD.tileset,
                                    updatedTilesSingletonLocal, effectEventBufferEntity);
                            }
                        }

                        ecb.DestroyEntity(entity);
                        return;
                    }

                    if (flag)
                    {
                        waterSpreaderCD.timer.Start(elapsedTime, GetRandomSpreadDelay(ref rng));
                        return;
                    }

                    ecb.DestroyEntity(entity);
                })
                .WithAll<WaterSpreaderCD>()
                .WithStoreEntityQueryInField(ref query)
                .WithoutBurst()
                .Schedule();

            base.OnUpdate();
        }

        private static float GetRandomSpreadDelay(ref Random rng)
        {
            return 1f + 0.25f * rng.NextInt(0, 5);
        }

        private static int GetHighestPrioWater(int tileset1, int tileset2, int highestPrio)
        {
            if (tileset1 == highestPrio)
            {
                return tileset1;
            }

            if (tileset2 == highestPrio)
            {
                return tileset2;
            }

            return math.max(tileset1, tileset2);
        }

        private static bool Tileset1HasHigherPrio(int tileset1, int tileset2, int highestPrio)
        {
            return (tileset1 == highestPrio && tileset2 != highestPrio) || tileset2 < tileset1;
        }

        private static void SpreadToPosition(EntityCommandBuffer ecb, int2 tilePos, TileType oldTileType, int oldTileset, int newTileset,
            Entity updatedTilesSingleton, Entity effectEventBuffer)
        {
            if (oldTileset == 3 || (oldTileType == TileType.water && newTileset == 3))
            {
                ClearAndAddLavaGround(ecb, tilePos, updatedTilesSingleton);
                EntityUtility.PlayEffectEventServer(ecb, effectEventBuffer, new EffectEventCD
                {
                    effectID = EffectID.BurnSmoke,
                    position1 = new float3(tilePos.x, -0.5f, tilePos.y)
                });
                return;
            }

            AddWaterAndRemovePit(ecb, tilePos, newTileset, updatedTilesSingleton);
        }

        private static void AddWaterAndRemovePit(EntityCommandBuffer ecb, int2 tilePos, int tileset, Entity updatedTilesSingleton)
        {
            ecb.AppendToBuffer(updatedTilesSingleton, new TileUpdateBuffer
            {
                command = TileUpdateBuffer.Command.Remove,
                position = tilePos,
                tile = new TileCD
                {
                    tileset = 0,
                    tileType = TileType.pit
                }
            });
            ecb.AppendToBuffer(updatedTilesSingleton, new TileUpdateBuffer
            {
                command = TileUpdateBuffer.Command.Add,
                position = tilePos,
                tile = new TileCD
                {
                    tileset = tileset,
                    tileType = TileType.water
                }
            });
        }

        private static void ClearAndAddLavaGround(EntityCommandBuffer ecb, int2 tilePos, Entity updatedTilesSingleton)
        {
            ecb.AppendToBuffer(updatedTilesSingleton, new TileUpdateBuffer
            {
                command = TileUpdateBuffer.Command.Clear,
                position = tilePos
            });
            ecb.AppendToBuffer(updatedTilesSingleton, new TileUpdateBuffer
            {
                command = TileUpdateBuffer.Command.Add,
                position = tilePos,
                tile = new TileCD
                {
                    tileset = 3,
                    tileType = TileType.ground
                }
            });
        }
    }
}