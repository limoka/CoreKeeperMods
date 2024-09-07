using System.Collections.Generic;
using HarmonyLib;
using PlacementPlus.Util;
using UnityEngine;

namespace PlacementPlus
{
    [HarmonyPatch]
    public static class Emote_Patch
    {
        public const Emote.EmoteType MOD_EMOTE = (Emote.EmoteType)500;
        private static string lastMessage;

        private static List<Emote> lastEmotes = new List<Emote>();

        internal static void SpawnModEmoteText(
            Vector3 position, 
            string emoteString, 
            bool randomizePosition = true,
            bool replace = true)
        {
            if (replace && lastEmotes.Count > 0)
            {
                foreach (Emote lastEmote in lastEmotes)
                {
                    FadeQuickly(lastEmote);
                }
                lastEmotes.Clear();
            }
            
            lastMessage = emoteString;
            var emote = Emote.SpawnEmoteText(position, MOD_EMOTE, randomizePosition, false, false);
            lastEmotes.Add(emote);
        }

        private static void FadeQuickly(Emote emote)
        {
            emote.fadeEffect.fadeOutTime = 0.1f;
            emote.StartFadeOut();
        }

        [HarmonyPatch(typeof(Emote), nameof(Emote.OnOccupied))]
        [HarmonyPrefix]
        public static bool OnOccupied(Emote __instance)
        {
            __instance.fadeEffect.fadeOutTime = 0.5f;
            
            if (__instance.emoteTypeInput == MOD_EMOTE &&
                !string.IsNullOrEmpty(lastMessage))
            {
                Vector3 vector3_1 = new Vector3(0.0f, 3f, -3f);
                Vector2 vector2 = Random.insideUnitCircle * 0.33f;
                Vector3 vector3_2 = __instance.randomizePosition ? new Vector3(vector2.x, vector2.y, 0.0f) : Vector3.zero;
                __instance.transform.position = (__instance.emotePosition + vector3_1 + vector3_2).RoundToMultiple(16f);
                __instance.iconSkin.gameObject.SetActive(false);
                
                __instance.SetValue("textToPrint", lastMessage);
                __instance.text.localize = false;
                __instance.textOutline.localize = false;
                ApplyEmote(__instance);
                lastMessage = "";
                return false;
            }

            return true;
        }

        private static void ApplyEmote(Emote __instance)
        {
            string textToPrint = __instance.GetValue<string>("textToPrint");
            __instance.text.Render(textToPrint, true);
            __instance.textOutline.Render(textToPrint, true);
            __instance.textOutline.SetTempColor(Color.black);
        }
    }
}