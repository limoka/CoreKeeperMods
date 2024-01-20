using System;
using PugConversion;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace DummyMod
{
    [GhostComponent]
    public struct DummyCD : IComponentData
    { 
        [GhostField]
        public int lastDamage;
        
        [GhostField]
        public int minDamage;
        [GhostField]
        public int averageDamage;
        [GhostField]
        public int maxDamage;

        public int oldAverageDamage;
        public int damageCount;

        public int damageSum;
        public float deltaTimeSum;
        public int ticksElapsed;

        [GhostField]
        public int damagePerSecond;
        [GhostField]
        public int maxDamagePerSecond;
    }

    public class DummyAuthoring : MonoBehaviour { }

    public class DummyConverter : SingleAuthoringComponentConverter<DummyAuthoring>
    {
        protected override void Convert(DummyAuthoring authoring)
        {
            AddComponentData(new DummyCD()
            {
                minDamage = int.MaxValue
            });
            EnsureHasBuffer<DummyDamageBuffer>();
        }
    }
}