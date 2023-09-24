using CoreLib.Submodules.ModEntity.Interfaces;
using PugTilemap;
using UnityEngine;

namespace BucketMod
{
    public class BucketDynamicItemHandler : IDynamicItemHandler
    {
        public static Color oldColor;

        public static Color[] oldCanColors = new Color[5];
            
        public static Color waterColor;
        public static Color larvaWaterColor;
        public static Color moldWaterColor;
        public static Color seaWaterColor;
        public static Color lavaColor;

        static BucketDynamicItemHandler()
        {
            ColorUtility.TryParseHtmlString("#47677E", out oldColor);
            
            ColorUtility.TryParseHtmlString("#97281c", out oldCanColors[0]);
            ColorUtility.TryParseHtmlString("#812218", out oldCanColors[1]);
            ColorUtility.TryParseHtmlString("#6c2220", out oldCanColors[2]);
            ColorUtility.TryParseHtmlString("#5b2321", out oldCanColors[3]);
            ColorUtility.TryParseHtmlString("#522120", out oldCanColors[4]);
            
            ColorUtility.TryParseHtmlString("#1E3D81", out waterColor);
            ColorUtility.TryParseHtmlString("#756730", out larvaWaterColor);
            ColorUtility.TryParseHtmlString("#3D5587", out moldWaterColor);
            ColorUtility.TryParseHtmlString("#34D0FF", out seaWaterColor);
            ColorUtility.TryParseHtmlString("#DE3501", out lavaColor);
        }

        public bool ShouldApply(ObjectDataCD objectData)
        {
            return objectData.objectID == BucketSlot.bucketObjectID || objectData.objectID == BucketSlot.canObjectID;
        }

        public void ApplyText(ObjectDataCD objectData, TextAndFormatFields text)
        {
            if (objectData.variation != 0)
            {
                Tileset tileset = (Tileset)BucketSlot.ParseVariation(objectData.variation, out int _);
                
                switch (tileset)
                {
                    case Tileset.Dirt:
                        text.text = $"{text.text}Dirt";
                        break;
                    case Tileset.LarvaHive:
                        text.text = $"{text.text}LarvaHive";
                        break;
                    case Tileset.Mold:
                        text.text = $"{text.text}Mold";
                        break;
                    case Tileset.Sea:
                        text.text = $"{text.text}Sea";
                        break;
                    case Tileset.Lava:
                        text.text = $"{text.text}Lava";
                        break;
                }
            }
        }

        public bool ApplyColors(ObjectDataCD objectData, ColorReplacementData colorData)
        {
            if (objectData.variation != 0)
            {
                if (objectData.objectID == BucketSlot.bucketObjectID)
                {
                    ApplyBucketColors(objectData, colorData);
                    return true;
                } 
                if (objectData.objectID == BucketSlot.canObjectID)
                {
                    ApplyCanColors(objectData, colorData);
                    return true;
                }
            }

            return false;
        }

        private static void ApplyCanColors(ObjectDataCD objectData, ColorReplacementData colorData)
        {
            Tileset tileset = (Tileset)BucketSlot.ParseVariation(objectData.variation, out int count);
            ColorList list = new ColorList();

            for (int i = 0; i < count; i++)
            {
                Color orgColor = oldCanColors[i];
                colorData.srcColors.Add(orgColor);
                list.colorList.Add(GetLiquidColor(tileset, orgColor));
            }
            
            colorData.replacementColors.Add(list);
        }

        private static void ApplyBucketColors(ObjectDataCD objectData, ColorReplacementData colorData)
        {
            colorData.srcColors.Add(oldColor);
            ColorList list = new ColorList();
            colorData.replacementColors.Add(list);

            Tileset tileset = (Tileset)BucketSlot.ParseVariation(objectData.variation, out int _);

            list.colorList.Add(GetLiquidColor(tileset, oldColor));
        }

        private static Color GetLiquidColor(Tileset tileset, Color orgColor)
        {
            switch (tileset)
            {
                case Tileset.Dirt:
                    return waterColor;
                case Tileset.LarvaHive:
                    return larvaWaterColor;
                case Tileset.Mold:
                    return moldWaterColor;
                case Tileset.Sea:
                    return seaWaterColor;
                case Tileset.Lava:
                    return lavaColor;
                default:
                    return orgColor;
            }
        }
    }
}