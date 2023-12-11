using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityExplorer.ObjectExplorer;
using UniverseLib;
using UniverseLib.UI;
using UniverseLib.UI.Models;
using Object = UnityEngine.Object;

namespace UnityExplorer.UI.Panels
{
    public class ObjectExplorerPanel : UEPanel
    {
        public override string Name => "Object Explorer";
        public override UE_UIManager.Panels PanelType => UE_UIManager.Panels.ObjectExplorer;

        public override int MinWidth => 350;
        public override int MinHeight => 200;
        public override Vector2 DefaultAnchorMin => new(0.125f, 0.175f);
        public override Vector2 DefaultAnchorMax => new(0.325f, 0.925f);

        public SceneExplorer SceneExplorer => tabPages[0] as SceneExplorer;

        public override bool ShowByDefault => true;
        public override bool ShouldSaveActiveState => true;

        public int SelectedTab = 0;
        private readonly List<UITabPanel> tabPages = new();
        private readonly List<ButtonRef> tabButtons = new();
        private GameObject tabGroup;

        public ObjectExplorerPanel(UIBase owner) : base(owner) { }

        public void AddTab(UITabPanel tab)
        {
            tab.ConstructUI(ContentRoot);
            tabPages.Add(tab);
            AddTabButton(tab.Name);
        }

        public void SetTab(int tabIndex)
        {
            if (SelectedTab != -1)
                DisableTab(SelectedTab);

            UIModel content = tabPages[tabIndex];
            content.SetActive(true);

            ButtonRef button = tabButtons[tabIndex];
            RuntimeHelper.SetColorBlock(button.Component, UniversalUI.EnabledButtonColor, UniversalUI.EnabledButtonColor * 1.2f);

            SelectedTab = tabIndex;
            SaveInternalData();
        }

        private void DisableTab(int tabIndex)
        {
            tabPages[tabIndex].SetActive(false);
            RuntimeHelper.SetColorBlock(tabButtons[tabIndex].Component, UniversalUI.DisabledButtonColor, UniversalUI.DisabledButtonColor * 1.2f);
        }

        public override void Update()
        {
            if (SelectedTab >= 0 && SelectedTab < tabPages.Count)
            {
                tabPages[SelectedTab].Update();
            }
        }

        public override string ToSaveData()
        {
            return string.Join("|", new string[] { base.ToSaveData(), SelectedTab.ToString() });
        }

        protected override void ApplySaveData(string data)
        {
            base.ApplySaveData(data);

            try
            {
                int tab = int.Parse(data.Split('|').Last());
                SelectedTab = tab;
            }
            catch
            {
                SelectedTab = 0;
            }

            SelectedTab = Math.Max(0, SelectedTab);
            SelectedTab = Math.Min(1, SelectedTab);

            SetTab(SelectedTab);
        }

        protected override void ConstructPanelContent()
        {
            // Tab bar
            tabGroup = UIFactory.CreateHorizontalGroup(ContentRoot, "TabBar", true, true, true, true, 2, new Vector4(2, 2, 2, 2));
            UIFactory.SetLayoutElement(tabGroup, minHeight: 25, flexibleHeight: 0);

            AddTab(new SceneExplorer(this));
            AddTab(new ObjectSearch(this));

            // default active state: Active
            this.SetActive(true);
        }

        private void AddTabButton(string label)
        {
            ButtonRef button = UIFactory.CreateButton(tabGroup, $"Button_{label}", label);

            int idx = tabButtons.Count;
            //button.onClick.AddListener(() => { SetTab(idx); });
            button.OnClick += () => { SetTab(idx); };

            tabButtons.Add(button);

            DisableTab(tabButtons.Count - 1);
        }

        private void RemoveTabButton(int index)
        {
            if (index >= 0 && index < tabButtons.Count)
            {
                ButtonRef button = tabButtons[index];
                Object.Destroy(button.GameObject);
                tabButtons.RemoveAt(index);
            }
        }
    }
}