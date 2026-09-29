using System;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;
using UniverseLib;
using UniverseLib.Runtime;
using UniverseLib.UI;
using UniverseLib.UI.Models;
using UniverseLib.UI.ObjectPool;
using UniverseLib.UI.Widgets.ScrollView;

namespace ECSExtension
{
    public class EntityCell : ICell, IPooledObject
    {
        public float DefaultHeight => 25f;
        public GameObject UIRoot { get; set; }
        public RectTransform Rect { get; set; }

        public bool Enabled => UIRoot.activeSelf;
        public void Enable() => UIRoot.SetActive(true);
        public void Disable() => UIRoot.SetActive(false);
        
        public Entity entity;

        public Action<Entity> OnEntityClicked;
        public Action<Entity, bool> onEnableClicked;
        private Toggle EnabledToggle;
        private ButtonRef NameButton;


        public void ConfigureCell(Entity entity, EntityManager entityManager)
        {
            this.entity = entity;
            NameButton.ButtonText.text = entityManager.GetNameSafe(entity);
            EnabledToggle.SetIsOnWithoutNotify(entityManager.IsEnabled(entity));
        }
        
        private void MainButtonClicked()
        {
            OnEntityClicked?.Invoke(entity);
        }

        private void OnEnableClicked(bool value)
        {
            onEnableClicked?.Invoke(entity, value);
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

            GameObject spacerObj = UIFactory.CreateUIObject("Spacer", UIRoot, new Vector2(0, 0));
            UIFactory.SetLayoutElement(spacerObj, minWidth: 0, flexibleWidth: 0, minHeight: 0, flexibleHeight: 0);

            // Expand arrow

            var label =UIFactory.CreateLabel(UIRoot, "DotObj", "▪");
            UIFactory.SetLayoutElement(label.gameObject, minWidth: 15, flexibleWidth: 0, minHeight: 25, flexibleHeight: 0);

            // Enabled toggle

            GameObject toggleObj = UIFactory.CreateToggle(UIRoot, "BehaviourToggle", out EnabledToggle, out Text behavText, default, 17, 17);
            UIFactory.SetLayoutElement(toggleObj, minHeight: 17, flexibleHeight: 0, minWidth: 17);
            EnabledToggle.onValueChanged.AddListener(OnEnableClicked);

            // Name button

            GameObject nameBtnHolder = UIFactory.CreateHorizontalGroup(UIRoot, "NameButtonHolder",
                false, false, true, true, childAlignment: TextAnchor.MiddleLeft);
            UIFactory.SetLayoutElement(nameBtnHolder, flexibleWidth: 9999, minHeight: 25, flexibleHeight: 0);
            nameBtnHolder.AddComponent<Mask>().showMaskGraphic = false;

            NameButton = UIFactory.CreateButton(nameBtnHolder, "NameButton", "Name");
            UIFactory.SetLayoutElement(NameButton.Component.gameObject, flexibleWidth: 9999, minHeight: 25, flexibleHeight: 0);
            Text nameLabel = NameButton.Component.GetComponentInChildren<Text>();
            nameLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            nameLabel.alignment = TextAnchor.MiddleLeft;

            // Setup selectables

            Color normal = new Color(0.11f, 0.11f, 0.11f);
            Color highlight = new Color(0.25f, 0.25f, 0.25f);
            Color pressed = new Color(0.05f, 0.05f, 0.05f);
            Color disabled = new Color(1, 1, 1, 0);
            RuntimeHelper.SetColorBlock(NameButton.Component, normal, highlight, pressed, disabled);

            NameButton.OnClick += MainButtonClicked;

            UIRoot.SetActive(false);

            return UIRoot;
        }
    }
}