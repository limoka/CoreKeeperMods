using CoreLib.Util.Extensions;
using PugConversion;
using Unity.Entities;
using UnityEngine;

namespace KeepFarming.Components
{
    public struct GoldenSeedCD : IComponentData
    {
        
    }

    public class GoldenSeedAuthoring : MonoBehaviour { }

    public class GoldenSeedConverter : SingleAuthoringComponentConverter<GoldenSeedAuthoring>
    {
        protected override void Convert(GoldenSeedAuthoring authoring)
        {
            AddComponentData(new GoldenSeedCD());
        }
    }
}