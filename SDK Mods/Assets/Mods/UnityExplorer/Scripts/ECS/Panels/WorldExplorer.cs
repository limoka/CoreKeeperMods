using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;
using UnityExplorer;
using UnityExplorer.ObjectExplorer;
using UnityExplorer.UI.Panels;
using UniverseLib.Runtime;
using UniverseLib.UI;
using UniverseLib.UI.Models;
using UniverseLib.UI.Widgets.ScrollView;
using UniverseLib.Utility;



namespace ECSExtension.Panels
{
    public class WorldExplorer : UITabPanel
    {
        public ObjectExplorerPanel Parent { get; }

        public WorldExplorer(ObjectExplorerPanel parent)
        {
            Parent = parent;
            
            ECSHelper.WorldCreated += OnWorldStateChanged;
            ECSHelper.WorldDestroyed += OnWorldStateChanged;
        }

        public override GameObject UIRoot => uiRoot;
        private GameObject uiRoot;

        public EntityTree Tree;

        //private GameObject refreshRow;
        private Dropdown sceneDropdown;
        private readonly Dictionary<World, Dropdown.OptionData> sceneToDropdownOption = new Dictionary<World, Dropdown.OptionData>();
        
        // scene loader
        private World _selectedWorld;
        private QueryComponentList queryComponentList;
        private Toggle includDisabledToggle;
        private InputFieldRef nameFilterInputField;


        public World SelectedWorld
        {
            get => _selectedWorld;
            private set
            {
                _selectedWorld = value;
                Tree.SetWorld(value);
            }
        }

        public override string Name => "World Explorer";

        public override void Update()
        {
        }

        public void UpdateTree()
        {
            PopulateWorldDropdown(World.All);
            Tree.RefreshData(false);
        }

        private void OnWorldSelectionDropdownChanged(int value)
        {
            if (value < 0 || World.All.Count <= value)
                return;
            SelectedWorld = World.All[value];
            Tree.RefreshData(true);
        }

        private void SceneHandler_OnInspectedSceneChanged(World world)
        {
            if (!sceneToDropdownOption.ContainsKey(world))
                PopulateWorldDropdown(World.All);

            if (sceneToDropdownOption.ContainsKey(world))
            {
                Dropdown.OptionData opt = sceneToDropdownOption[world];
                int idx = sceneDropdown.options.IndexOf(opt);
                if (sceneDropdown.value != idx)
                    sceneDropdown.value = idx;
                else
                    sceneDropdown.captionText.text = opt.text;
            }
        }

        private void OnWorldStateChanged(World world)
        {
            UpdateTree();
        }

        private void PopulateWorldDropdown(World.NoAllocReadOnlyCollection<World> loadedScenes)

        {
            sceneToDropdownOption.Clear();
            sceneDropdown.options.Clear();

            foreach (World world in loadedScenes)
            {
                if (sceneToDropdownOption.ContainsKey(world))
                    continue;

                string name = world.Name?.Trim();
                
                if (string.IsNullOrEmpty(name))
                    name = "<untitled>";

                Dropdown.OptionData option = new Dropdown.OptionData(name);
                sceneDropdown.options.Add(option);
                sceneToDropdownOption.Add(world, option);
            }
        }

        
        private void DoQuery()
        {
            PopulateWorldDropdown(World.All);
            ComponentType[] include = queryComponentList.GetComponents(QueryComponentList.SearchType.Include);
            ComponentType[] exclude = queryComponentList.GetComponents(QueryComponentList.SearchType.Exclude);
            bool includeDisabled = includDisabledToggle.isOn;
            Tree.UseQuery(include, exclude, includeDisabled);
            Tree.RefreshData(true);
        }
        
        private void OnInputChanged(string filter)
        {
            Tree.SetFilter(filter);
        }
        
        void OnCellClicked(Entity obj) => InspectorManager.Inspect(obj);

        public override void ConstructUI(GameObject content)
        {
            uiRoot = UIFactory.CreateUIObject("WorldExplorer", content);
            UIFactory.SetLayoutGroup<VerticalLayoutGroup>(uiRoot, true, true, true, true, 0, 2, 2, 2, 2);
            UIFactory.SetLayoutElement(uiRoot, flexibleHeight: 9999);

            // Tool bar (top area)

            GameObject toolbar = UIFactory.CreateVerticalGroup(uiRoot, "Toolbar", true, true, true, true, 2, new Vector4(2, 2, 2, 2),
               new Color(0.15f, 0.15f, 0.15f));

            // Scene selector dropdown

            GameObject dropRow = UIFactory.CreateHorizontalGroup(toolbar, "DropdownRow", true, true, true, true, 5, default, new Color(1, 1, 1, 0));
            UIFactory.SetLayoutElement(dropRow, minHeight: 25, flexibleWidth: 9999);

            Text dropLabel = UIFactory.CreateLabel(dropRow, "SelectorLabel", "World:", TextAnchor.MiddleLeft, Color.cyan, false, 15);
            UIFactory.SetLayoutElement(dropLabel.gameObject, minHeight: 25, minWidth: 60, flexibleWidth: 0);

            GameObject dropdownObj = UIFactory.CreateDropdown(dropRow, "SceneDropdown", out sceneDropdown, "<notset>", 13, OnWorldSelectionDropdownChanged);
            UIFactory.SetLayoutElement(dropdownObj, minHeight: 25, flexibleHeight: 0, flexibleWidth: 9999);

            //SceneHandler.Update();
            PopulateWorldDropdown(World.All);
            sceneDropdown.captionText.text = sceneToDropdownOption.First().Value.text;

            // Filter row
            
            GameObject filterRow = UIFactory.CreateVerticalGroup(toolbar, "FilterGroup", true, false, true, true, 2, new Vector4(2, 2, 2, 2),
                new Color(0.15f, 0.15f, 0.15f));

            UIFactory.SetLayoutElement(filterRow, minHeight: 75);
            
            var queryComponentScroll = UIFactory.CreateScrollPool<QueryComponentCell>(filterRow, "QueryList", out GameObject uiRoot1,
                out GameObject compContent, new Color(0.11f, 0.11f, 0.11f));
            UIFactory.SetLayoutElement(uiRoot1, minHeight: 25,  flexibleHeight: -1);
            UIFactory.SetLayoutElement(compContent, minHeight: 25, flexibleHeight: -1);
            
            GameObject viewPort = compContent.transform.parent.gameObject;
            LayoutElement layoutElement = viewPort.GetComponent<LayoutElement>();
            layoutElement.flexibleHeight = -1;
            
            queryComponentList = new QueryComponentList(queryComponentScroll, layoutElement);
            
            GameObject isDisabledToggle = UIFactory.CreateToggle(filterRow, "IncludeDisabled", out includDisabledToggle, out Text toggleText);
            UIFactory.SetLayoutElement(isDisabledToggle, minHeight: 25, minWidth: 80);
            toggleText.text = "Include disabled";
            toggleText.color = Color.grey;
            includDisabledToggle.isOn = false;
            
            ButtonRef queryButton = UIFactory.CreateButton(filterRow, "QueryButton", "Query");
            UIFactory.SetLayoutElement(queryButton.Component.gameObject, minHeight: 25, flexibleHeight: 0);
            queryButton.OnClick += DoQuery;

            // name filter row
            
            GameObject nameFilterRow = UIFactory.CreateHorizontalGroup(toolbar, "Name Filter", true, true, true, true, 2, new Vector4(2, 2, 2, 2));
            UIFactory.SetLayoutElement(nameFilterRow, minHeight: 30, flexibleHeight: 0);
            
            nameFilterInputField = UIFactory.CreateInputField(nameFilterRow, "Name Filter Field", "Search and press enter...");
            UIFactory.SetLayoutElement(nameFilterInputField.UIRoot, minHeight: 25, flexibleHeight: 0, flexibleWidth: 9999);
            nameFilterInputField.Component.GetOnEndEdit().AddListener(OnInputChanged);
            
            
            // tree labels row

            GameObject labelsRow = UIFactory.CreateHorizontalGroup(toolbar, "LabelsRow", true, true, true, true, 2, new Vector4(2, 2, 2, 2));
            UIFactory.SetLayoutElement(labelsRow, minHeight: 30, flexibleHeight: 0);

            Text nameLabel = UIFactory.CreateLabel(labelsRow, "NameLabel", "Entity", TextAnchor.MiddleLeft, color: Color.grey);
            UIFactory.SetLayoutElement(nameLabel.gameObject, flexibleWidth: 9999, minHeight: 25);

            // Transform Tree

            ScrollPool<EntityCell> scrollPool = UIFactory.CreateScrollPool<EntityCell>(uiRoot, "EntityTree", out GameObject scrollObj,
                out GameObject scrollContent, new Color(0.11f, 0.11f, 0.11f));
            UIFactory.SetLayoutElement(scrollObj, flexibleHeight: 9999);
            UIFactory.SetLayoutElement(scrollContent, flexibleHeight: 9999);

            Tree = new EntityTree(scrollPool, OnCellClicked);

            if (World.All.Count > 0)
            {
                SelectedWorld = World.All[0];
            }

            Tree.RefreshData(true);
        }
    }
}