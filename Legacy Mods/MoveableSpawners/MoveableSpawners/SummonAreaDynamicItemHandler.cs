using CoreLib.Submodules.CustomEntity.Interfaces;

namespace MoveableSpawners
{
    public class SummonAreaDynamicItemHandler : IDynamicItemHandler
    {
        public bool ShouldApply(ObjectDataCD objectData)
        {
            return objectData.objectID == ObjectID.SummonArea;
        }

        public void ApplyText(ObjectDataCD objectData, TextAndFormatFields text)
        {
            if (objectData.variation != 0)
            {
                text.text = $"{text.text}{objectData.variation}";
            }
        }

        public bool ApplyColors(ObjectDataCD objectData, ColorReplacementData colorData)
        {
            return false;
        }
    }
}