using System;
using CoreLib.Components;
using CoreLib.Submodules.ModComponent;
using Il2CppInterop.Runtime.Attributes;
using Il2CppInterop.Runtime.InteropTypes.Fields;
using Unity.Entities;

namespace SecureAttachment
{
    [Il2CppImplements(typeof(IComponentData))]
    public struct WrenchCD
    {
        public int wrenchTier;
    }
    
    [Il2CppImplements(typeof(IConvertGameObjectToEntity))]
    public class WrenchCDAuthoring : ModCDAuthoringBase
    {
        public Il2CppValueField<int> wrenchTier;
        
        public WrenchCDAuthoring(IntPtr ptr) : base(ptr) { }
    
        public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
        {
            dstManager.AddModComponentData(entity, new WrenchCD()
            {
                wrenchTier = wrenchTier,
            });
        }
        
    }
}