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
    // Brush placement commands require an anchor payload, so they use a separate type — BrushPlaceRPC.cs.


    public struct PlacementPlusRPC : IRpcCommand
    {
        public ModCommandType commandType;
        public Entity player;
        public int valueChange;
    }

    public enum ModMessageType : byte
    {
        UNDEFINED,
        MODE_MESSAGE,
        ROOFING_MODE_MESSAGE,
        BLOCK_MODE_MESSAGE,
        // messageData: new sledge step (0..3), or -1 for "admins only" denial.
        // Broadcast to every connection so all clients stay in sync.
        SLEDGE_SIZE_MESSAGE,
        // messageData: 1 = square swing (arc360), 0 = stock arc, -1 = denial.
        SLEDGE_SHAPE_MESSAGE,
        
        // Silent synchronization upon connection (08-25): messageData = step * 2 + (square ? 1 : 0).
        // If a new client doesn't know the server's hammer settings, it will draw the preview 
        // using its own local cfg, resulting in a "9x9 preview but 3x3 strike" desync. 
        // This updates 'synced' without displaying a speech bubble.
        SLEDGE_SYNC_MESSAGE
    }
    
    public struct PlacementMessageRPC : IRpcCommand
    {
        public ModMessageType messageType;
        public int messageData;
    }
}