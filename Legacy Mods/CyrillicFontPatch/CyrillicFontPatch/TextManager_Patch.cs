using System.Collections.Generic;
using System.Runtime.InteropServices;
using CoreLib.Submodules.ModResources;
using HarmonyLib;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Unity.Collections;
using UnityEngine;

namespace RussianFontPatch
{
    [HarmonyPatch]
    public static class TextManager_Patch
    {
        private static PugFont cyrThinMeduium;

        [HarmonyPatch(typeof(TextManager), nameof(TextManager.Init))]
        [HarmonyPrefix]
        public static void OnInit(TextManager __instance)
        {
            CyrillicFontPatchPlugin.logger.LogInfo("Patching!");

            Texture2D thinTexture = CyrillicFontPatchPlugin.LoadTexture("rrs_rus_thin8.png");
            Texture2D thickTexture = CyrillicFontPatchPlugin.LoadTexture("rrs8_rus.png");

            PugFont thin = __instance.thinSmallR;
            thin.texture = thinTexture;
            thin.charDims = new Vector2Int(8, 12);
            thin.charSpacing = 1;
            thin.spaceWidth = 2;
            thin.lineSpacing = -2;
            thin._customCharset =
                """♥♡ⒶⒷⓍⓎ⊕⊖   „“–                  """ +
                """☻!"#$%&'()*+,-./0123456789:;<=>?""" +
                """АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮ""" +
                """абвгдеёжзийклмнопрстуфхцчшщъыьэю""" +
                """`@¿[\]^_ЯЇІ                     """ +
                """´©™{|}~…яїі                     """;
            FillGhyphData(thin);

            PugFont thick = __instance.boldSmallR;
            thick.texture = thickTexture;
            thick.charDims = new Vector2Int(8, 12);
            thick.charSpacing = 1;
            thick.spaceWidth = 4;
            thick.lineSpacing = -2;
            thick._customCharset =
                """♥♡ⒶⒷⓍⓎ⊕⊖   „“–                  """ +
                """☻!"#$%&'()*+,-./0123456789:;<=>?""" +
                """АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮ""" +
                """абвгдеёжзийклмнопрстуфхцчшщъыьэю""" +
                """`@¿[\]^_ЯЇІ                     """ +
                """´©™{|}~…яїі                     """;
            FillGhyphData(thick);

            Texture2D thimMeduimTexture = CyrillicFontPatchPlugin.LoadTexture("rrs_rus_10thin.png");

            IL2CPP.il2cpp_gc_disable();
            cyrThinMeduium = ScriptableObject.CreateInstance<PugFont>();
            ResourcesModule.Retain(cyrThinMeduium);
            GCHandle.Alloc(cyrThinMeduium);
            cyrThinMeduium.charDims = new Vector2Int(8, 16);
            cyrThinMeduium.texture = thimMeduimTexture;
            cyrThinMeduium.pixelsPerUnit = 16;
            cyrThinMeduium.charSpacing = 2;
            cyrThinMeduium.spaceWidth = 4;
            cyrThinMeduium.lineSpacing = 0;
            cyrThinMeduium.emptyLineSpacing = 5;
            cyrThinMeduium.proportionalFont = true;
            cyrThinMeduium._customCharset =
                """!„“'(),-./0123456789:           """ +
                """АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮ""" +
                """абвгдеёжзийклмнопрстуфхцчшщъыьэю""" +
                """ЯЇІяїі                          """;
            FillGhyphData(cyrThinMeduium);
            cyrThinMeduium.InitCodePoints();
            IL2CPP.il2cpp_gc_enable();
        }

        [HarmonyPatch(typeof(TextManager), nameof(TextManager.GetCyrillicFont))]
        [HarmonyPostfix]
        public static void GetCyrillicFont(TextManager.FontFace fontFace, ref PugFont __result)
        {
            if (fontFace == TextManager.FontFace.thinMedium) __result = cyrThinMeduium;
        }

        [HarmonyPatch(typeof(TextManager), nameof(TextManager.GetFont))]
        [HarmonyPostfix]
        public static void GetFont(TextManager __instance, TextManager.FontFace fontFace, bool forceLatin, ref PugFont __result)
        {
            string language = Manager.prefs.language;
            if (language != "ru" && language != "uk") return;

            if (__result == __instance.chineseFont ||
                __result == __instance.japaneseFont ||
                __result == __instance.koreanFont)
            {
                __result = GetNormalFont(__instance, fontFace);
                return;
            }

            if (fontFace == TextManager.FontFace.thinMedium &&
                __result == __instance.boldSmallR)
                __result = cyrThinMeduium;
        }

        [HarmonyPatch(typeof(PugFont), nameof(PugFont.GetGlyphData))]
        [HarmonyPostfix]
        public static void GetGlyphData(PugFont __instance, char c, ref PugFont.GlyphData gd, PugTextStyle style, ref bool usedCharacterFromOtherLanguage, ref int cp)
        {
            
        }

        private static PugFont GetNormalFont(TextManager __instance, TextManager.FontFace fontFace)
        {
            switch (fontFace)
            {
                case TextManager.FontFace.thinTiny:
                    return __instance.thinTiny;
                case TextManager.FontFace.thinSmall:
                    return __instance.thinSmall;
                case TextManager.FontFace.boldSmall:
                    return __instance.boldSmall;
                case TextManager.FontFace.thinMedium:
                    return __instance.thinMedium;
                case TextManager.FontFace.boldMedium:
                    return __instance.boldMedium;
                case TextManager.FontFace.boldLarge:
                    return __instance.boldLarge;
                case TextManager.FontFace.boldHuge:
                    return __instance.boldHuge;
                case TextManager.FontFace.score:
                    return __instance.specialBoldLarge;
            }
            return __instance.thinSmall;
        }

        [HarmonyPatch(typeof(TextManager), nameof(TextManager.GetFontToUseForString))]
        [HarmonyPostfix]
        public static void GetFontToUseForString(TextManager __instance, string value, TextManager.FontFace fontFace, ref TextManager.FontInfo __result)
        {
            if (fontFace == TextManager.FontFace.thinMedium &&
                __result.pugFont == __instance.boldSmallR)
                __result.pugFont = cyrThinMeduium;
        }

        public static void FillGhyphData(PugFont font)
        {
            Texture2D texture = font.texture;
            var pixelData = texture.GetRawTextureData<Color32>();

            int xChar = texture.width / font.charDims.x;
            int yChar = texture.height / font.charDims.y;

            var glyphs = new List<PugFont.GlyphData>(font.charset.Length);

            for (int y = 0; y < yChar; y++)
            for (int x = 0; x < xChar; x++)
            {
                int charIndex = x + y * xChar;
                if (charIndex >= font.charset.Length) continue;

                char c = font.charset[charIndex];
                if (c == ' ')
                {
                    glyphs.Add(new PugFont.GlyphData());
                    continue;
                }

                int tx = x * font.charDims.x;
                int ty = texture.height - (y + 1) * font.charDims.y;
                int width = ComputeWidth(font, tx, ty, pixelData);

                if (width > 0)
                    glyphs.Add(new PugFont.GlyphData
                    {
                        rect = new RectInt
                        {
                            x = tx,
                            y = ty,
                            height = font.charDims.y,
                            width = width
                        },
                        kerning = new Il2CppStructArray<byte>(0)
                    });
            }

            font.glyphData = glyphs.ToArray();
        }

        private static int ComputeWidth(PugFont font, int tx, int ty, NativeArray<Color32> pixelData)
        {
            Texture2D texture = font.texture;
            int width = font.charDims.x;
            for (int cx = tx + font.charDims.x - 1; cx >= tx; cx--)
            {
                bool found = false;
                for (int cy = ty; cy < ty + font.charDims.y; cy++)
                {
                    Color32 pixel = pixelData[cx + cy * texture.width];
                    // The texture is ARGB, but this method returns RGBA.
                    // We need the alpha, so using r
                    if (pixel.r != 0)
                        found = true;
                }

                if (found) break;
                width--;
            }

            return width;
        }

        //	public static string latinCharset = "♥♡       ♦♢„“–  ⁰¹²³⁴⁵⁶⁷⁸⁹     ⚑
        //☻!\"#$%&'()*+,-./0123456789:;<=>?
        //@ABCDEFGHIJKLMNOPQRSTUVWXYZ
        //[\\]^_`
        //abcdefghijklmnopqrstuvwxyz
        //{|}~…
        //ÀÁÂÃÄÅÇÈÉÊËÍÌÎÏÑÒÓÔÕÖØÙÚÛÜÆŒß
        //¿¡\u00a0
        //àáâãäåçèéêëíìîïñòóôõöøùúûüæœ
        //©™  \u00b4ĞğıİŞşĄąŚśŻżźŁłćęńšČčŠ
        //ŮůŽžŘřĚě ŇňÝýŤťĎď×ĆŹĘŃ’";
    }
}