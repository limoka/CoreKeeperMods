
using Mods.PlacementPlus.Scripts.Util;
using PlacementPlus.Components;
using PlayerEquipment;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Physics;

namespace PlacementPlus.Systems
{
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(EquipmentUpdateSystemGroup))]
    [UpdateBefore(typeof(EquipmentUpdateSystem))]
    public partial class PlacementPlusSystem : PugSimulationSystemBase
    {
        private int _tickRate;

        private EntityArchetype _achievementArchetype;

        private bool _toolsResized;

        protected override void OnCreate()
        {
            _tickRate = PlatformConfiguration.Instance.SessionConfiguration.SimulationTickRate;
            _achievementArchetype = AchievementSystem.GetRpcArchetype(EntityManager);

            RequireForUpdate<PhysicsWorldSingleton>();
            RequireForUpdate<WorldInfoCD>();
            RequireForUpdate<TileWithTilesetToObjectDataMapCD>();

            NeedDatabase();
            base.OnCreate();
        }

        // Shovels/hoes/roofing/watering can carry their working area in the object
        // database, and every size path (GetTileSizeFromVariation, GetCurrentSize,
        // the tool-tier cap) reads from there. Widening it once at startup lifts the
        // ceiling for all of them — same trick the Tool Resizer mod uses.
        // Called from the generated OnUpdate (the source generator replaces this
        // class's OnUpdate wholesale, so a call placed in the original body never
        // runs). The database is passed in because it is already resolved there.
        internal void ResizeToolsOnce(PugDatabase.DatabaseBankCD bank)
        {
            if (_toolsResized) return;
            _toolsResized = true;

            if (PlacementPlusMod.ignoreToolTierLimit?.Value != true) return;

            // prefabTileSize is the hard ceiling for these tools: GetCurrentSize
            // returns it directly, and GetTileSizeFromVariation clamps to it too.
            // So it must be as large as MaxBrushSize. The "brush shrinks the area"
            // problem is fixed in MyPlacementHandler instead, by treating brush
            // size 0 as 1x1 rather than falling back to the tool's own area.
            int size = PlacementPlusMod.maxSize.Value;
            int count = 0;
            ref var infos = ref bank.databaseBankBlob.Value.objectInfos;
            for (int i = 0; i < infos.Length; i++)
            {
                ref var info = ref infos[i];
                if (info.objectType == ObjectType.Shovel ||
                    info.objectType == ObjectType.Hoe ||
                    info.objectType == ObjectType.RoofingTool ||
                    info.objectType == ObjectType.WaterCan)
                {
                    info.prefabTileSize = new int2(size, size);
                    count++;
                }
            }

            PlacementPlusMod.Log.LogInfo($"Tool size ceiling raised to {size}x{size} for {count} tools");
        }

        protected override void OnUpdate()
        {
            var networkTime = SystemAPI.GetSingleton<NetworkTime>();
            var currentTick = networkTime.ServerTick;

            var databaseBank = SystemAPI.GetSingleton<PugDatabase.DatabaseBankCD>();

            var levelLookup = SystemAPI.GetComponentLookup<LevelCD>();
            var levelEntitiesLookup = SystemAPI.GetBufferLookup<LevelEntitiesBuffer>();
            
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
                    
                    var conditionsBuffer = HelperLogic.GetConditionsBuffer(objectData, ref entityObjectInfo, levelLookup, levelEntitiesLookup, givesConditionsLookup);
                    
                    int damage = HelperLogic.GetShovelDamage(objectData, ref entityObjectInfo, conditionsBuffer);
                    if (damage == 0)
                    {
                        state.currentMaxSize = maxSize;
                        state.CheckSize(currentTick);
                        return;
                    }

                    state.currentMaxSize = PlacementPlusMod.ignoreToolTierLimit.Value
                        ? maxSize
                        : HelperLogic.GetShovelLevel(damage);
                    state.CheckSize(currentTick);
                })
                .WithName("UpdateMaxSize")
                .WithoutBurst()
                .Schedule();

            base.OnUpdate();
        }
    }
}
