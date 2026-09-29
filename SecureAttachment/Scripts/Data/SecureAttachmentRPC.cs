using Unity.Entities;
using Unity.NetCode;

namespace SecureAttachment
{
    public struct SecureAttachmentRPC : IRpcCommand
    {
        public MessageType command;
        public WrenchMode wrenchMode;
        
        public Entity player;
    }
}