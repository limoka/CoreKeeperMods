using Unity.Entities;
using Unity.Mathematics;

namespace PlacementPlus
{
    public struct TileData
    {
        public Entity entity;
        public TileCD tileCd;

        public TileData(Entity entity, TileCD tileCd)
        {
            this.entity = entity;
            this.tileCd = tileCd;
        }
    }
}