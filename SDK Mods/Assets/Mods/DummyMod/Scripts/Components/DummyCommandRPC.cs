using Unity.Entities;
using Unity.NetCode;

namespace DummyMod
{
    public enum DummyCommandType : byte
    {
        RESET_DUMMY,
        DESTROY_DUMMY
    }
    
    public struct DummyCommandRPC : IRpcCommand
    {
        public DummyCommandType commandType;
        public Entity entity0;
    }
}