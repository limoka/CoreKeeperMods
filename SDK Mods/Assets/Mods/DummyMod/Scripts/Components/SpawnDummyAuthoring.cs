using PugConversion;
using Unity.Entities;
using UnityEngine;

namespace DummyMod
{
    public struct SpawnDummyCD : IComponentData
    {
    }

    public class SpawnDummyAuthoring : MonoBehaviour { }

    public class SpawnDummyConverter : SingleAuthoringComponentConverter<SpawnDummyAuthoring>
    {
        protected override void Convert(SpawnDummyAuthoring authoring)
        {
            AddComponentData(new SpawnDummyCD());
        }
    }
}