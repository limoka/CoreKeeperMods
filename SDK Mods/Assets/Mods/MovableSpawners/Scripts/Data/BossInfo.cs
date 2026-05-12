using Unity.Transforms;

namespace Mods.MovableSpawners.Scripts.Data
{
    public struct BossInfo
    {
        public ObjectID bossID;
        public LocalTransform transform;

        public BossInfo(ObjectID bossID, LocalTransform transform)
        {
            this.bossID = bossID;
            this.transform = transform;
        }
    }
}