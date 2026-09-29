using PugTilemap;
using Unity.Entities;
using Unity.Mathematics;

namespace PlacementPlus.Components
{
    [InternalBufferCapacity(25)]
    public struct ShovelDigQueueBuffer : IBufferElementData
    {
        public int2 position;
        public Entity entity;
        public TileType tileType;
        public int tileset;
    }
}