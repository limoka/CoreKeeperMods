using Unity.Entities;

namespace Mods.PlacementPlus.Scripts.Util
{
    public struct PlacementPlusLookups
    {
        public ComponentLookup<RandomCD> randomLookup;
        public ComponentLookup<OwnerReferenceCD> ownerLookup;
        public ComponentLookup<IsExplosiveCD> isExplosiveLookup;
        public ComponentLookup<FactionCD> factionLookup;
        public ComponentLookup<DamageReductionCD> damageReductionLookup;


        public void Init(ref SystemState state)
        {
            randomLookup = state.GetComponentLookup<RandomCD>();
            ownerLookup = state.GetComponentLookup<OwnerReferenceCD>();
            isExplosiveLookup = state.GetComponentLookup<IsExplosiveCD>();
            factionLookup = state.GetComponentLookup<FactionCD>();
            damageReductionLookup = state.GetComponentLookup<DamageReductionCD>();
        }
    }
}