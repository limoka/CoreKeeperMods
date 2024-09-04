using Unity.Entities;
using Unity.NetCode;

namespace PlacementPlus.Commands
{
    public enum ModCommandType : byte
    {
        UNDEFINED,
        CHANGE_SIZE,
        
        CHANGE_TOOL_MODE,
        CHANGE_ORIENTATION,
        
        SET_REPLACE
    }

    public struct PlacementPlusRPC : IRpcCommand
    {
        public ModCommandType commandType;
        public Entity player;
        public int valueChange;
    }
}