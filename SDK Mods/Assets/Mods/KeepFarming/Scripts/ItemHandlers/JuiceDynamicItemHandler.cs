using System.Collections.Generic;
using CoreLib.Submodules.ModEntity.Interfaces;
using KeepFarming.Components;
using UnityEngine;

namespace Mods.KeepFarming.Scripts
{
    public class JuiceDynamicItemHandler : IDynamicItemHandler
    {
        private static List<Color> originalColors;

        static JuiceDynamicItemHandler()
        {
            Color[] _originalColors = new Color[4];
            
            UnityEngine.ColorUtility.TryParseHtmlString("#ffffff", out _originalColors[0]);
            UnityEngine.ColorUtility.TryParseHtmlString("#f7eec7", out _originalColors[1]);
            UnityEngine.ColorUtility.TryParseHtmlString("#ecc1bf", out _originalColors[2]);
            UnityEngine.ColorUtility.TryParseHtmlString("#e9d9a2", out _originalColors[3]);
            originalColors = new List<Color>(_originalColors);
        }


        public bool ShouldApply(ObjectDataCD objectData)
        {
            return PugDatabase.HasComponent<JuiceCD>(objectData);
        }

        public void ApplyText(ObjectDataCD objectData, TextAndFormatFields text)
        {
        }

        public bool ApplyColors(ObjectDataCD objectData, ColorReplacementData colorData)
        {
            JuiceCD juice = PugDatabase.GetComponent<JuiceCD>(objectData);

            colorData.srcColors = originalColors;
            List<Color> dstColors = new List<Color>(4)
            {
                juice.brightestColor,
                juice.brightColor,
                juice.darkColor,
                juice.darkestColor
            };

            colorData.replacementColors = new List<ColorList>()
            {
                new ColorList()
                {
                    colorList = dstColors
                }
            };

            return true;
        }
    }
}