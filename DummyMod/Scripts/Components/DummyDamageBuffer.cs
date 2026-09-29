using Unity.Entities;

namespace DummyMod
{
    [InternalBufferCapacity(60)]
    public struct DummyDamageBuffer : IBufferElementData
    {
        public int damage;
        public float deltaTime;
    }
}