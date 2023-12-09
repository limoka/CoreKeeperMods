﻿using System.Collections.Generic;
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
        [HarmonyPostfix]
        public static void OnOccupied(Emote __instance)
        {
            __instance.fadeEffect.fadeOutTime = 0.5f;
            
            if (__instance.emoteTypeInput == MOD_EMOTE &&
                !string.IsNullOrEmpty(lastMessage))
            {
                __instance.SetValue("textToPrint", lastMessage);
                ApplyEmote(__instance);
                lastMessage = "";
            }
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