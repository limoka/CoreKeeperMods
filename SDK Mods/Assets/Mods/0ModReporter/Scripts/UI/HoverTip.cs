using System;
using ModReporter.Scripts;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ModReporter.UI
{
    public class HoverTip : MonoBehaviour
    {
        public TMP_Text infoText;

        public void SetHoverTip(string text)
        {
            infoText.text = text;
            UpdateSize();
            UpdatePosition();
            gameObject.SetActive(true);
        }

        public void HideTip()
        {
            gameObject.SetActive(false);
        }

        private void Update()
        {
            UpdatePosition();
        }

        private void UpdatePosition()
        {
            var rectTransform = transform as RectTransform;
            var parentRect = rectTransform.parent as RectTransform;

            Vector2 rectOffset = new Vector2(0, parentRect.rect.height);
            rectTransform.anchoredPosition = (Vector2)Input.mousePosition - rectOffset;
        }

        private void UpdateSize()
        {
            var preferredHeight = infoText.preferredHeight;

            var rectTransform = transform as RectTransform;
            
            Vector2 size = rectTransform.sizeDelta;
            size.y = preferredHeight;
            rectTransform.sizeDelta = size;
        }
    }
}