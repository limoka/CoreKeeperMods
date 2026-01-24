using System;
using Pug.Conversion;
using Unity.Entities;
using UnityEngine;

namespace SecureAttachment
{
    public struct WrenchCD : IComponentData
    {
        public int wrenchTier;
    }

    public class WrenchCDAuthoring : MonoBehaviour
    {
        public int wrenchTier;
    }
    
    public class WrenchCDBaker : SingleAuthoringComponentConverter<WrenchCDAuthoring>
    {
        protected override void Convert(WrenchCDAuthoring authoring)
        {
            AddComponentData(new WrenchCD()
            {
                wrenchTier = authoring.wrenchTier,
            });
        }
    }
}