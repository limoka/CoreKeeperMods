using System;
using UnityEngine;
using UnityEngine.UI;
using UnityExplorer.UI.Widgets.AutoComplete;
using UniverseLib.UI;
using UniverseLib.UI.Models;
using UniverseLib.UI.Widgets.ScrollView;

namespace ECSExtension.Panels
{
    public class QueryComponentCell : ICell
    {
        public GameObject UIRoot { get; set; }
        public float DefaultHeight => 25;
        
        public bool Enabled => UIRoot.activeSelf;
        public void Enable() => UIRoot.SetActive(true);
        public void Disable() => UIRoot.SetActive(false);
        
        public RectTransform Rect { get; set; }
        private TypeCompleter typeCompleter;
        private InputFieldRef componentInputField;
        private Dropdown searchTypeDropdown;
        private int cellIndex;

        private static readonly String[] options =
        {
            "Include",
            "Exclude"
        };
        
        public Action<int, string> OnTextChanged;
        public Action<int, int> OnSearchTypeChanged;

        public void ConfigureCell(int index, QueryComponentList.SearchData currentValue)
        {
            cellIndex = index;
            componentInputField.Text = currentValue.componentName;
            searchTypeDropdown.value = (int)currentValue.searchType;
        }
        
        public void SetCellToDefault(int index)
        {
            cellIndex = index;
            componentInputField.Text = "";
            searchTypeDropdown.value = 0;
        }

        private void OnInputChanged(string text)
        {
            OnTextChanged?.Invoke(cellIndex, text);
        }
        
        public GameObject CreateContent(GameObject parent)
        {
            UIRoot = UIFactory.CreateUIObject("TransformCell", parent);
            UIFactory.SetLayoutGroup<HorizontalLayoutGroup>(UIRoot, false, false, true, true, 2, childAlignment: TextAnchor.MiddleCenter);
            Rect = UIRoot.GetComponent<RectTransform>();
            Rect.anchorMin = new Vector2(0, 1);
            Rect.anchorMax = new Vector2(0, 1);
            Rect.pivot = new Vector2(0.5f, 1);
            Rect.sizeDelta = new Vector2(25, 25);
            UIFactory.SetLayoutElement(UIRoot, minWidth: 100, flexibleWidth: 9999, minHeight: 25, flexibleHeight: 0);
            // Class input

            Text unityClassLbl = UIFactory.CreateLabel(UIRoot, "ComponentLabel", "Component:", TextAnchor.MiddleLeft);
            UIFactory.SetLayoutElement(unityClassLbl.gameObject, minWidth: 90, flexibleWidth: 0);

            componentInputField = UIFactory.CreateInputField(UIRoot, "CComponentInput", "...");
            UIFactory.SetLayoutElement(componentInputField.UIRoot, minHeight: 25, flexibleHeight: 0, flexibleWidth: 9999);
            componentInputField.OnValueChanged += OnInputChanged;

            GameObject layerDrop = UIFactory.CreateDropdown(UIRoot, "SearchTypeDropDown", out searchTypeDropdown, "Include", 14, OnSearchDropdownChanged, options);
            UIFactory.SetLayoutElement(layerDrop, minHeight: 25, minWidth: 100);
            
            typeCompleter = new TypeCompleter(typeof(ValueType), componentInputField, false, false, false);
            return UIRoot;
        }

        private void OnSearchDropdownChanged(int searchType)
        {
            OnSearchTypeChanged?.Invoke(cellIndex, searchType);
        }
    }
}