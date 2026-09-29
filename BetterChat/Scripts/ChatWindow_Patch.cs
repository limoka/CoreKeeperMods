using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BetterChat.Scripts
{
    [HarmonyPatch]
    internal static class ChatWindow_Patch
    {
        private const string PREFAB_PATH = "Assets/Mods/BetterChat/Prefab/UI/ChatBackground.prefab";
        private static ChatBackground background;
        
        [HarmonyPatch(typeof(ChatWindow), "AllocPugText")]
        [HarmonyPostfix]
        public static void AllocPugText(PugText __result)
        {
            __result.maxWidth = BetterChatMod.chatWindowWidth;
        }

        [HarmonyPatch(typeof(ChatWindow), "AddMessageHeight")]
        [HarmonyPrefix]
        public static bool AddMessageHeight(ChatWindow __instance, float h)
        {
            var activePools = __instance.GetField<Dictionary<ChatWindow.MessageTextType, Queue<PugText>>>("activePools");
            var freePools = __instance.GetField<Dictionary<ChatWindow.MessageTextType, Queue<PugText>>>("freePools");
            var fadeEffects = __instance.GetField<Queue<PugTextEffectMaxFade>>("fadeEffects");
            
            foreach (KeyValuePair<ChatWindow.MessageTextType, Queue<PugText>> keyValuePair in activePools)
            {
                ChatWindow.MessageTextType key = keyValuePair.Key;
                Queue<PugText> value = keyValuePair.Value;
                foreach (PugText pugText in value)
                {
                    Transform transform = pugText.transform;
                    Vector3 localPosition = transform.localPosition;
                    localPosition.y += h;
                    transform.localPosition = localPosition;
                }

                while (value.Count > 0 && 
                       value.Peek().transform.localPosition.y > BetterChatMod.chatWindowHeight)
                {
                    PugText pugText2 = value.Dequeue();
                    pugText2.gameObject.SetActive(false);
                    freePools[key].Enqueue(pugText2);
                    fadeEffects.Dequeue();
                }
            }

            return false;
        }

        [HarmonyPatch(typeof(ChatWindow), "Awake")]
        [HarmonyPostfix]
        public static void Awake(ChatWindow __instance)
        {
            var prefab = BetterChatMod.AssetBundle.LoadAsset<GameObject>(PREFAB_PATH);
            var backgroundGO = Object.Instantiate(prefab, __instance.transform);
            background = backgroundGO.GetComponent<ChatBackground>();

            float width = BetterChatMod.chatWindowWidth + 1;
            background.spriteRenderer.size = new Vector2(width, 14.6f);
            backgroundGO.transform.localPosition = new Vector3(-14.8f + width / 2f, 0.75f, 10f);
            
            background.Init(__instance);
        }
    }
}