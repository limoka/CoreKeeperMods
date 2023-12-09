using CoreLib.Util.Extensions;
using PugConversion;
using PugMod;
using Unity.Entities;
using UnityEngine;

namespace KeepFarming.Components
{
    public struct SeedExtractorCD : IComponentData
    {
        public float processingTime;
        public int juiceOutputSlot;
    }

    public struct SeedExtractorTimerTriggerCD : IComponentData
    {
    }

    public class SeedExtractorAuthoring : MonoBehaviour
    {
        public float processingTime;
        public string recipeItem;
    }

    public class SeedExtractorConverter : SingleAuthoringComponentConverter<SeedExtractorAuthoring>
    {
        protected override void Convert(SeedExtractorAuthoring authoring)
        {
            AddComponentData(new PugTimerUserCD
            {
                triggerType = typeof(SeedExtractorTimerTriggerCD)
            });
            EnsureHasComponent<PugTimerRefCD>();
                
            int seedOutputSlot = AddToBuffer(default(ContainedObjectsBuffer));
            int juiceOutputSlot = AddToBuffer(default(ContainedObjectsBuffer));
            AddComponentData(new CraftingCD
            {
                currentlyCraftingIndex = -1,
                craftingType = CraftingType.ProcessResources,
                outputSlotIndex = seedOutputSlot,
                showLoopEffectOnOutputSlot = false
            });
            EnsureHasBuffer<CanCraftObjectsBuffer>();
            AddToBuffer(new CanCraftObjectsBuffer()
            {
                objectID = API.Authoring.GetObjectID(authoring.recipeItem),
                amount = 1
            });
            
            AddComponentData(new SeedExtractorCD()
            {
                processingTime = authoring.processingTime,
                juiceOutputSlot = juiceOutputSlot
            });
        }
    }
}