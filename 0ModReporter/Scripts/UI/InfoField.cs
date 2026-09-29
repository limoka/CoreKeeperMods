using System;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;

namespace ModReporter.UI
{
    public class InfoField : MonoBehaviour
    {
        private static Regex pattern = new Regex("<\\/?color.*?>", RegexOptions.Compiled);

        public TMP_Text mainText;

        public int baseHeight = 30;
        public bool adjustSize;

        private void Awake()
        {
            SetText(mainText.text);
        }

        public void SetText(string text)
        {
            mainText.text = text;
            UpdateSize();
        }

        private void UpdateSize()
        {
            if (adjustSize)
            {
                var preferredHeight = mainText.preferredHeight;
                preferredHeight += baseHeight;

                var rectTransform = transform as RectTransform;
                
                Vector2 size = rectTransform.sizeDelta;
                size.y = preferredHeight;
                rectTransform.sizeDelta = size;
            }
        }

        public void OnCopy()
        {
            var text = pattern.Replace(mainText.text, "");
            GUIUtility.systemCopyBuffer = text;
        }

    }
}