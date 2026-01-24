using System.Collections.Generic;
using CoreLib.Submodule.Entity.Interface;
using KeepFarming;
using KeepFarming.Components;
using PugMod;
using UnityEngine;

namespace Mods.KeepFarming.Scripts
{
    public class GoldenSeedDynamicItemHandler : IDynamicItemHandler
    {
        private static readonly List<Color> _srcColors;
        private static readonly List<Color> _dstColors;
        
        static GoldenSeedDynamicItemHandler()
        {
            Color[] originalColors = new Color[5];
            
            ColorUtility.TryParseHtmlString("#0f5630", out originalColors[0]);
            ColorUtility.TryParseHtmlString("#0a6d27", out originalColors[1]);
            ColorUtility.TryParseHtmlString("#239029", out originalColors[2]);
            ColorUtility.TryParseHtmlString("#5ca537", out originalColors[3]);
            ColorUtility.TryParseHtmlString("#a3ce4a", out originalColors[4]);
            _srcColors = new List<Color>(originalColors);
            
            Color[] newColors = new Color[5];
            
            ColorUtility.TryParseHtmlString("#824223", out newColors[0]);
            ColorUtility.TryParseHtmlString("#da6210", out newColors[1]);
            ColorUtility.TryParseHtmlString("#e2a12c", out newColors[2]);
            ColorUtility.TryParseHtmlString("#f2df3a", out newColors[3]);
            ColorUtility.TryParseHtmlString("#f7eec7", out newColors[4]);
            _dstColors = new List<Color>(newColors);
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

        public void ApplyColors(ObjectDataCD objectData, ColorReplacer replacer)
        {
            replacer.SetColorReplacement(
                _srcColors,
                _dstColors
            );
        }
    }
}