using System;
using UnityEngine;

namespace BetterChat.Scripts
{
    public class ChatBackground : MonoBehaviour
    {
        [SerializeField] internal SpriteRenderer spriteRenderer;
        
        private ChatWindow window;
        private PlayerHungerBarUI hungerBarUI;
        private ConditionsContainerUI conditionsUI;
        private bool lastState;

        public void Init(ChatWindow window)
        {
            this.window = window;
            var parent = Manager.ui.playerHealthBarUI.transform.parent;
            hungerBarUI = parent.GetComponentInChildren<PlayerHungerBarUI>();
            conditionsUI = parent.GetComponentInChildren<ConditionsContainerUI>();
        }
        
        private void Update()
        {
            if (window == null) return;
            
            var active = window.inputFieldPrompt.gameObject.activeSelf;
            spriteRenderer.enabled = active;
            if (lastState != active)
            {
                OnChangedState(active);
                lastState = active;
            }
        }

        private void OnChangedState(bool active)
        {
            Manager.ui.playerHealthBarUI.gameObject.SetActive(!active);
            hungerBarUI.gameObject.SetActive(!active);
            conditionsUI.gameObject.SetActive(!active);
        }
    }
}