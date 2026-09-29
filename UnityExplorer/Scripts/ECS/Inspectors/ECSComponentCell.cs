using System;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;
using UniverseLib.UI;
using UniverseLib.UI.Models;
using UniverseLib.UI.Widgets.ButtonList;

namespace ECSExtension
{
    public class ECSComponentCell : ButtonCell
    {
        public Toggle EnabledToggle;
        public ButtonRef DestroyButton;
        public Text TypeLabel;
        
        public Action<bool, int> OnEnabledToggled;
        public Action<int> OnDestroyClicked;

        public void ConfigureCell(ComponentType type, EntityInspector inspector)
        {
            Type monoType = type.GetManagedType();
            TypeManager.TypeInfo typeInfo = TypeManager.GetTypeInfo(type.TypeIndex);

            var isEnableable = TypeManager.IsEnableable(type.TypeIndex);
            
            if (isEnableable)
            {
                var enabled = inspector.IsComponentEnabled(type);
                
                EnabledToggle.interactable = true;
                EnabledToggle.SetIsOnWithoutNotify(enabled);
                EnabledToggle.graphic.color = new Color(0.8f, 1, 0.8f, 0.3f);
            }
            else
            {
                EnabledToggle.interactable = false;
                EnabledToggle.SetIsOnWithoutNotify(true);
                EnabledToggle.graphic.color = new Color(0.2f, 0.2f, 0.2f);
            }
            
            Button.ButtonText.text = monoType.ToString();
            TypeLabel.text = GetCategoryText(typeInfo.Category);

        }

        private string GetCategoryText(TypeManager.TypeCategory category)
        {
            switch (category)
            {
                case TypeManager.TypeCategory.ComponentData:
                    return "Component";
                case TypeManager.TypeCategory.BufferData:
                    return "Buffer";
                case TypeManager.TypeCategory.ISharedComponentData:
                    return "Shared";
                case TypeManager.TypeCategory.EntityData:
                    return "Entity";
                case TypeManager.TypeCategory.UnityEngineObject:
                    return "UObject";
                default:
                    return "";
            }
        }
        
        private void EnabledToggled(bool val)
        {
            OnEnabledToggled?.Invoke(val, CurrentDataIndex);
        }
        
        private void DestroyClicked()
        {
            OnDestroyClicked?.Invoke(CurrentDataIndex);
        }

        public override GameObject CreateContent(GameObject parent)
        {
            var root = base.CreateContent(parent);

            // Add mask to button so text doesnt overlap on Close button
            //this.Button.Component.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            this.Button.ButtonText.horizontalOverflow = HorizontalWrapMode.Wrap;

            GameObject toggleObj = UIFactory.CreateToggle(UIRoot, "EnabledToggle", out EnabledToggle, out Text text);
            UIFactory.SetLayoutElement(toggleObj, minHeight: 25, minWidth: 25);
            EnabledToggle.onValueChanged.AddListener(EnabledToggled);
            // put at first object
            toggleObj.transform.SetSiblingIndex(0);
            
            TypeLabel = UIFactory.CreateLabel(UIRoot, "TypeLabel", "Component", TextAnchor.MiddleCenter);
            UIFactory.SetLayoutElement(TypeLabel.gameObject, minHeight: 21, minWidth: 100);
            
            DestroyButton = UIFactory.CreateButton(UIRoot, "DestroyButton", "X", new Color(0.3f, 0.2f, 0.2f));
            UIFactory.SetLayoutElement(DestroyButton.Component.gameObject, minHeight: 21, minWidth: 25);
            DestroyButton.OnClick += DestroyClicked;

            return root;
        }
    }
}