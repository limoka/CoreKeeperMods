using System;
using System.Collections.Generic;
using ModReporter.Scripts;
using PugMod;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ModReporter.UI
{
    public class ModItem : MonoBehaviour
    {
        private Action<string> onClicked;
        private ModStatus status;
        private bool wasShowingTipLastFrame;
        
        public string modName;

        public TMP_Text modNameText;
        public Image statusImage;
        public Image backgroundImage;

        public Sprite[] statusSprites;
        
        public void SetState(string modName, ModStatus status, Action<string> onClicked)
        {
            this.modName = modName;
            this.status = status;
            this.onClicked = onClicked;
            modNameText.text = modName;
            int index = (int)status;
            if (index > 0 && index < statusSprites.Length)
            {
                statusImage.sprite = statusSprites[index];
            }
        }

        public void OnClick()
        {
            onClicked?.Invoke(modName);
        }

        public void SetSelected(bool selected)
        {
            backgroundImage.color = Color.HSVToRGB(0, 0, selected ? 0.2f : 0.31f);
        }

        private void Update()
        {
            bool isOverUI = IsPointerOverUIElement();
            
            if (wasShowingTipLastFrame == isOverUI) return;
            
            if (isOverUI)
            {
                var tipText = API.Localization.GetLocalizedTerm($"ModReporter/{status}");
                ModReporterMod.hoverTip.SetHoverTip(tipText);
            }
            else
            {
                ModReporterMod.hoverTip.HideTip();
            }

            wasShowingTipLastFrame = isOverUI;
        }
 
 
        //Returns 'true' if we touched or hovering on Unity UI element.
        public bool IsPointerOverUIElement()
        {
            return IsPointerOverUIElement(GetEventSystemRaycastResults());
        }
        
        private bool IsPointerOverUIElement(List<RaycastResult> eventSystemRaysastResults)
        {
            for (int index = 0; index < eventSystemRaysastResults.Count; index++)
            {
                RaycastResult curRaysastResult = eventSystemRaysastResults[index];
                if (curRaysastResult.gameObject == statusImage.gameObject)
                    return true;
            }
            return false;
        }
        
        //Gets all event system raycast results of current mouse or touch position.
        static List<RaycastResult> GetEventSystemRaycastResults()
        {
            PointerEventData eventData = new PointerEventData(EventSystem.current);
            eventData.position = Input.mousePosition;
            List<RaycastResult> raysastResults = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, raysastResults);
            return raysastResults;
        }
    }
}