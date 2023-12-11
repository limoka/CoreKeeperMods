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
        public ButtonRef DestroyButton;
        public Text typeLabel;
        
        public Action<int> OnDestroyClicked;

        public void ConfigureCell(ComponentType type)
        {
            Type monoType = type.GetManagedType();
            TypeManager.TypeInfo typeInfo = TypeManager.GetTypeInfo(type.TypeIndex);
            
            Button.ButtonText.text = monoType.ToString();
            typeLabel.text = GetCategoryText(typeInfo.Category);

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

            typeLabel = UIFactory.CreateLabel(UIRoot, "TypeLabel", "Component", TextAnchor.MiddleCenter);
            UIFactory.SetLayoutElement(typeLabel.gameObject, minHeight: 21, minWidth: 100);
            
            DestroyButton = UIFactory.CreateButton(UIRoot, "DestroyButton", "X", new Color(0.3f, 0.2f, 0.2f));
            UIFactory.SetLayoutElement(DestroyButton.Component.gameObject, minHeight: 21, minWidth: 25);
            DestroyButton.OnClick += DestroyClicked;

            return root;
        }
    }
}