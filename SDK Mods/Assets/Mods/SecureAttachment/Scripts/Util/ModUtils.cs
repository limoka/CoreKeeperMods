using Unity.Entities;
using Unity.NetCode;

namespace SecureAttachment
{
    public static class ModUtils
    {
        public static void SendCommand<T>(this EntityManager em, T message) where T : unmanaged, IRpcCommand
        {
            Entity req = em.CreateEntity();
            em.AddComponentData(req, message);
            em.AddComponent<SendRpcCommandRequest>(req);
        }
        
        public static void SendResponse<T>(this EntityManager em, T message, Entity connection) where T : unmanaged, IRpcCommand
        {
            Entity req = em.CreateEntity();
            em.AddComponentData(req, message);
            em.AddComponentData(req, new SendRpcCommandRequest { TargetConnection = connection });
        }
    }
}