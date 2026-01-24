using System;
using Pug.Conversion;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace SecureAttachment
{
    public struct MountedCD : IComponentData
    {
        public int wrenchTier;
    }

    public class MountedCDAuthoring : MonoBehaviour
    {
        public int wrenchTier;
    }
    
    public class MountedCDBaker : SingleAuthoringComponentConverter<MountedCDAuthoring>
    {
        protected override void Convert(MountedCDAuthoring authoring)
        {
            AddComponentData(new MountedCD()
            {
                wrenchTier = authoring.wrenchTier,
            });
            EnsureHasComponent<IndestructibleCD>();
        }
    }
}