using System;
using CoreLib.Components;
using CoreLib.Submodules.ModComponent;
using Il2CppInterop.Runtime.Attributes;
using Il2CppInterop.Runtime.InteropTypes.Fields;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace SecureAttachment
{
    [Il2CppImplements(typeof(IComponentData))]
    public struct MountedCD
    {
        public int wrenchTier;
    }
    
    [Il2CppImplements(typeof(IConvertGameObjectToEntity))]
    public class MountedCDAuthoring : ModCDAuthoringBase
    {
        public Il2CppValueField<int> wrenchTier;
        
        public MountedCDAuthoring(IntPtr ptr) : base(ptr) { }
    
        public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
        {
            dstManager.AddModComponentData(entity, new MountedCD()
            {
                wrenchTier = wrenchTier,
            });
            dstManager.AddModComponent<IndestructibleCD>(entity);
        }
        
    }
}