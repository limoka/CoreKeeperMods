using PugConversion;
using Unity.Entities;
using UnityEngine;

namespace KeepFarming.Components
{
    public class CopiedPrefabAuthoring : MonoBehaviour { }

    public class CopiedPrefabConverter : SingleAuthoringComponentConverter<CopiedPrefabAuthoring>
    {
        protected override void Convert(CopiedPrefabAuthoring authoring)
        {
            EnsureHasComponent<Prefab>();
            int count = EnsureHasBuffer<LinkedEntityGroup>();
            if (count != 0) return;
            
            AddToBuffer(new LinkedEntityGroup()
            {
                Value = PrimaryEntity
            });
        }
    }
}