using Pug.Conversion;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;
using UnityEngine.Scripting;

namespace SecureAttachment
{
    [GhostComponent]
    public struct MountedCD : IComponentData
    {
        [GhostField]
        public MountType mountType;
        
        public MountType lastMountType;
    }

    public class CanBeMountedAuthoring : MonoBehaviour
    {
    }
    
    [Preserve]
    public class MountedConverter : SingleAuthoringComponentConverter<CanBeMountedAuthoring>
    {
        protected override void Convert(CanBeMountedAuthoring authoring)
        {
            AddComponentData(new MountedCD()
            {
                mountType = MountType.None,
                lastMountType = MountType.Unknown,
            });
            EnsureHasComponent<IndestructibleCD>(componentIsEnabled: false);
            EnsureHasComponent<MealsEatenCD>();
        }
    }
}