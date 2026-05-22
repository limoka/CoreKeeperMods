using Unity.Entities;
using Unity.NetCode;

namespace SecureAttachment
{
    [GhostComponent]
    public struct WrenchStateCD : IComponentData
    {
        [GhostField]
        public WrenchMode mode;
    }
}