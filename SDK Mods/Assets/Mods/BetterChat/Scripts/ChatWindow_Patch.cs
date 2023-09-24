using HarmonyLib;
using UnityEngine;

namespace BetterChat.Scripts
{
    [HarmonyPatch]
    public class ChatWindow_Patch
    {
        private const string PREFAB_PATH = "Assets/BetterChat/Prefab/UI/ChatBackground.prefab";
        private static ChatBackground background;
        
        [HarmonyPatch(typeof(ChatWindow), "AllocPugText")]
        [HarmonyPostfix]
        public static void AllocPugText(PugText __result)
        {
            __result.maxWidth = BetterChatMod.chatWindowWidth;
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