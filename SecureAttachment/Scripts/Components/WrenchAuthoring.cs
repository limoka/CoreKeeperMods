using System;
using Pug.Conversion;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Scripting;

namespace SecureAttachment
{
    public struct WrenchCD : IComponentData
    {
        public MountType mountType;
    }

    public class WrenchAuthoring : MonoBehaviour
    {
        public MountType mountType;
    }
    
    [Preserve]
    public class WrenchConverter : SingleAuthoringComponentConverter<WrenchAuthoring>
    {
        protected override void Convert(WrenchAuthoring authoring)
        {
            AddComponentData(new WrenchCD()
            {
                mountType = authoring.mountType,
            });
        }
    }
}