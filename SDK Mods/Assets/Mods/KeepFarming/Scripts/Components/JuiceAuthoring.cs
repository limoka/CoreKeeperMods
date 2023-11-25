using PugConversion;
using Unity.Entities;
using UnityEngine;

namespace KeepFarming.Components
{
    public struct JuiceCD : IComponentData
    {
        public Color brightestColor;
        public Color brightColor;
        public Color darkColor;
        public Color darkestColor;
    }

    public class JuiceAuthoring : MonoBehaviour
    {
        public Color brightestColor;
        public Color brightColor;
        public Color darkColor;
        public Color darkestColor;
    }

    public class JuiceCDConverter : SingleAuthoringComponentConverter<JuiceAuthoring>
    {
        protected override void Convert(JuiceAuthoring authoring)
        {
            AddComponentData(new JuiceCD()
            {
                brightestColor = authoring.brightestColor,
                brightColor = authoring.brightColor,
                darkColor = authoring.darkColor,
                darkestColor = authoring.darkestColor
            });
        }
    }
}