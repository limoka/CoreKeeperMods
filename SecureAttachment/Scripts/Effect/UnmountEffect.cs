using CoreLib.Submodule.Audio;
using Unity.Entities;

namespace SecureAttachment
{
    public class UnmountEffect : IEffect
    {
        public void PlayEffect(EffectEventCD effectEvent, Entity callerEntity, World world)
        {
            AudioManager.Sfx(
                SecureAttachmentMod.wrenchSfx,
                effectEvent.position1,
                pitchDev: 0.1f,
                playOnGamepad: EffectEventExtensions.ShouldPlayAudioAndRumbleOnGamepad(effectEvent.position1)
            );
        }
    }
}