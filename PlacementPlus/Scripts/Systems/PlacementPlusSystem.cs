
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
                    info.objectType == ObjectType.WaterCan || 
                    info.objectType == ObjectType.PaintTool)
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
            ResizeToolsOnce(databaseBank);

            foreach (var state in SystemAPI.Query<RefRW<PlacementPlusState>>())
            {
                int maxSize = PlacementPlusMod.maxSize.Value - 1;
                
                state.ValueRW.currentMaxSize = maxSize;
                state.ValueRW.CheckSize(currentTick);
            }

            base.OnUpdate();
        }
    }
}
