using HarmonyLib;
using UnityEngine;

namespace MovableSpawners.Patches
{
    [HarmonyPatch]
    public class ColorReplacer_Patch
    {
        public static Color oldColor;
        public static Color[] spawnerColors = new Color[7];

        static ColorReplacer_Patch()
        {
            ColorUtility.TryParseHtmlString("#ffffff", out oldColor);
            
            ColorUtility.TryParseHtmlString("#da6210", out spawnerColors[0]);
            ColorUtility.TryParseHtmlString("#c6723b", out spawnerColors[1]);
            ColorUtility.TryParseHtmlString("#ae5f3f", out spawnerColors[2]);
            ColorUtility.TryParseHtmlString("#954d94", out spawnerColors[3]);
            ColorUtility.TryParseHtmlString("#4f4b48", out spawnerColors[4]);
            ColorUtility.TryParseHtmlString("#165dd9", out spawnerColors[5]);
            ColorUtility.TryParseHtmlString("#7c391f", out spawnerColors[6]);
        }
        
        [HarmonyPatch(typeof(ColorReplacer), nameof(ColorReplacer.UpdateColorReplacerFromObjectData))]
        [HarmonyPostfix]
        public static void UpdateReplacer(ColorReplacer __instance, ContainedObjectsBuffer containedObject)
        {
            ObjectDataCD objectData = containedObject.objectData;
            if (objectData.objectID == ObjectID.SummonArea &&
                objectData.variation < spawnerColors.Length)
            {
                var colorData = __instance.colorReplacementData;
                
                colorData.srcColors.Add(oldColor);
                ColorList list = new ColorList();
                colorData.replacementColors.Add(list);

                list.colorList.Add(spawnerColors[objectData.variation]);

                __instance.SetActiveColorReplacement(1);
            }
        }
    }
}