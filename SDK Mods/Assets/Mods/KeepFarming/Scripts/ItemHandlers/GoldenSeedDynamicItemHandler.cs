using System.Collections.Generic;
using CoreLib.Submodules.ModEntity.Interfaces;
using KeepFarming;
using KeepFarming.Components;
using PugMod;
using UnityEngine;

namespace Mods.KeepFarming.Scripts
{
    public class GoldenSeedDynamicItemHandler : IDynamicItemHandler
    {
        private static List<Color> srcColors;
        private static List<Color> dstColors;
        
        static GoldenSeedDynamicItemHandler()
        {
            Color[] _originalColors = new Color[5];
            
            UnityEngine.ColorUtility.TryParseHtmlString("#0f5630", out _originalColors[0]);
            UnityEngine.ColorUtility.TryParseHtmlString("#0a6d27", out _originalColors[1]);
            UnityEngine.ColorUtility.TryParseHtmlString("#239029", out _originalColors[2]);
            UnityEngine.ColorUtility.TryParseHtmlString("#5ca537", out _originalColors[3]);
            UnityEngine.ColorUtility.TryParseHtmlString("#a3ce4a", out _originalColors[4]);
            srcColors = new List<Color>(_originalColors);
            
            Color[] _newColors = new Color[5];
            
            UnityEngine.ColorUtility.TryParseHtmlString("#824223", out _newColors[0]);
            UnityEngine.ColorUtility.TryParseHtmlString("#da6210", out _newColors[1]);
            UnityEngine.ColorUtility.TryParseHtmlString("#e2a12c", out _newColors[2]);
            UnityEngine.ColorUtility.TryParseHtmlString("#f2df3a", out _newColors[3]);
            UnityEngine.ColorUtility.TryParseHtmlString("#f7eec7", out _newColors[4]);
            dstColors = new List<Color>(_newColors);
        }

        public bool ShouldApply(ObjectDataCD objectData)
        {
            return //PugDatabase.HasComponent<SeedCD>(objectData) &&
                   PugDatabase.HasComponent<GoldenSeedCD>(objectData);
        }

        public void ApplyText(ObjectDataCD objectData, TextAndFormatFields text)
        {
            var mainLabel = API.Localization.GetLocalizedTerm(text.text);
            var addendum = API.Localization.GetLocalizedTerm("KeepFarming/Golden");
            
            text.text = $"{addendum} {mainLabel}";
            text.dontLocalize = true;
        }

        public bool ApplyColors(ObjectDataCD objectData, ColorReplacementData colorData)
        {
            colorData.srcColors = srcColors;
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