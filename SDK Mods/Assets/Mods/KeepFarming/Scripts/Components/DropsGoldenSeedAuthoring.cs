using CoreLib.Util.Extensions;
using PugConversion;
using Unity.Entities;
using UnityEngine;

namespace KeepFarming.Components
{
    public struct DropsGoldenSeedCD : IComponentData
    {
        public ObjectID seedId;
        public int amount;
        public float chance;
    }

    public class DropsGoldenSeedAuthoring : MonoBehaviour
    {
        public ObjectID seedId;
        public int amount;
        public float chance;
    }

    public class DropsGoldenSeedConverter : SingleAuthoringComponentConverter<DropsGoldenSeedAuthoring>
    {
        protected override void Convert(DropsGoldenSeedAuthoring authoring)
        {
            AddComponentData(new DropsGoldenSeedCD()
            {
                seedId = authoring.seedId,
                amount = authoring.amount,
                chance = authoring.chance
            });
        }
    }
}