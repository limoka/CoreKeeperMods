using CoreLib.Submodule.Audio;
using CoreLib.Submodule.EquipmentSlot;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace SecureAttachment
{
    public class UnmountFailedEffect : IEffect
    {
        public void PlayEffect(EffectEventCD effectEvent, Entity callerEntity, World world)
        {
            float3 effectPos = effectEvent.position1;

            if (effectEvent.entity != Entity.Null && 
                EntityUtility.EntityExists(effectEvent.entity, Manager.ecs.ClientWorld) && 
                GetEntityMonoRenderPosition(effectEvent.entity, out Vector3 vector))
            {
                effectPos = vector;
            }
            
            bool flag = EffectEventExtensions.ShouldPlayAudioAndRumbleOnGamepad(effectPos);
            AudioManager.Sfx(SfxID.clunk, effectPos, pitchDev: 0.1f, playOnGamepad: flag);
            
            var pc = Manager.main.player;
            if (pc == null) return;
            
            EquipmentSlotModule.SpawnModEmoteText(pc.center, SecureAttachmentMod.emoteWrenchNotUsable);
        }
        
        private static bool GetEntityMonoRenderPosition(Entity entity, out Vector3 position)
        {
            position = Vector3.zero;
            EntityMonoBehaviour entityMono = Manager.memory.GetEntityMono(entity);
            if (entityMono ==null)
                return false;
            
            position = entityMono.RenderPosition;
            return true;
        }
    }
}