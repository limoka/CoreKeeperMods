using System;
using System.Collections;
using System.Collections.Generic;
using ECSExtension.Cache;
using ECSExtension.Widgets;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;
using UnityExplorer;
using UnityExplorer.CacheObject;
using UnityExplorer.Inspectors;
using UnityExplorer.UI.Panels;
using UnityExplorer.UI.Widgets.AutoComplete;
using UniverseLib;
using UniverseLib.Runtime;
using UniverseLib.UI;
using UniverseLib.UI.Models;
using UniverseLib.UI.Widgets.ScrollView;


namespace ECSExtension
{
    public class EntityInspector : InspectorBase, ICacheObjectController
    {
        public Entity currentEntity;
        public World currentWorld;
        public EntityManager entityManager;
        
        public int currentWorldIndex;
        
        public List<World> validWorlds;

        public GameObject Content;
        private ScrollPool<ECSComponentCell> componentScroll;
        private ECSComponentList ecsComponentList;
        private EntityInfoPanel entityInfoPanel;
        private InputFieldRef addCompInput;
        
        public override GameObject CreateContent(GameObject parent)
        {
            UIRoot = UIFactory.CreateVerticalGroup(parent, "EntityInspector", true, false, true, true, 5,
                new Vector4(4, 4, 4, 4), new Color(0.065f, 0.065f, 0.065f));

            Content = UIFactory.CreateVerticalGroup(UIRoot, "TopPane", true, false, true, true, 5,
                new Vector4(4, 4, 4, 4), new Color(0.065f, 0.065f, 0.065f));

            UIFactory.SetLayoutElement(Content, minHeight: 100, preferredHeight: 100, flexibleHeight: 0, flexibleWidth: 9999);
            UIFactory.SetLayoutGroup<VerticalLayoutGroup>(Content, spacing: 3, padTop: 2, padBottom: 2, padLeft: 2, padRight: 2);

            entityInfoPanel = new EntityInfoPanel(this);
            
            ConstructLists();

            return UIRoot;
        }

        public override void Update()
        {
        }

        public override void OnBorrowedFromPool(object target)
        {
            base.OnBorrowedFromPool(target);

            currentEntity = (Entity)target;
            Target = currentEntity;

            validWorlds = new List<World>();
            foreach (World world in World.All)
            {
                if (world.EntityManager.Exists(currentEntity))
                {
                    validWorlds.Add(world);
                }
            }

            if (validWorlds.Count == 0)
            {
                CloseInspector();
                return;
            }

            currentWorld = validWorlds[0];
            entityManager = currentWorld.EntityManager;
            currentWorldIndex = 0;

            ECSHelper.WorldDestroyed += OnWorldDestroyed;

            string currentBaseTabText = $"[ECS] {GetEntityName()}";
            Tab.TabText.text = currentBaseTabText;
            
            entityInfoPanel.UpdateEntityInfo(true, true);

            RuntimeHelper.StartCoroutine(InitCoroutine());
        }

        private void OnWorldDestroyed(World obj)
        {
            if (obj == currentWorld)
            {
                CloseInspector();
            }
        }

        public string GetEntityName()
        {
            if (currentWorld == null) return currentEntity.ToString();
            return currentWorld.EntityManager.GetNameSafe(currentEntity);
        }

        public void SetWorld(int index)
        {
            if (index >= 0 && index < validWorlds.Count)
            {
                currentWorld = validWorlds[index];
                entityManager = currentWorld.EntityManager;
                currentWorldIndex = index;
                RuntimeHelper.StartCoroutine(InitCoroutine());
            }
        }

        private IEnumerator InitCoroutine()
        {
            yield return null;
            
            UpdateComponents();

            LayoutRebuilder.ForceRebuildLayoutImmediate(InspectorPanel.Instance.ContentRect);
        }
        

        private NativeArray<ComponentType> GetComponents()
        {
            if (currentEntity != Entity.Null)
            {
                return entityManager.GetComponentTypes(currentEntity);
            }

            return new NativeArray<ComponentType>();
        }

        public void InspectEntity(Entity entity)
        {
            currentEntity = entity;
            UpdateComponents();
        }

        public CacheComponent<T> GetComponentData<T>() where T : unmanaged, IComponentData
        {
            if (currentEntity == Entity.Null) return null;
            
            return new CacheComponent<T>(this);
        }
        
        public CacheSharedComponent<T> GetSharedComponentData<T>() where T : unmanaged, ISharedComponentData
        {
            if (currentEntity == Entity.Null) return null;
            
            return new CacheSharedComponent<T>(this);
        }
        
        public CacheManagedComponent<T> GetManagedComponentData<T>() where T : class, IComponentData, new()
        {
            if (currentEntity == Entity.Null) return null;
            
            return new CacheManagedComponent<T>(this);
        }

        public DynamicBuffer<T> GetDynamicBuffer<T>() where T : unmanaged, IBufferElementData
        {
            return entityManager.GetBuffer<T>(currentEntity);
        }

        public void RemoveComponent(ComponentType type)
        {
            currentWorld.EntityManager.RemoveComponent(currentEntity, type);
        }
        
        private void OnAddComponentClicked(string text)
        {
            Type type = ReflectionUtility.GetTypeByName(text);
            if (type != null)
            {
                try
                {
#if CPP
                    int index = TypeManager.GetTypeIndex(Il2CppType.From(type));
#else
                    int index = TypeManager.GetTypeIndex(type);
#endif
                    if (index > 0)
                    {
                        entityManager.AddComponent(currentEntity, ComponentType.FromTypeIndex(index));
                        UpdateComponents();
                    }
                }
                catch (Exception ex)
                {
                    ExplorerCore.LogWarning($"Exception adding component: {ex.ReflectionExToString()}");
                }
            }
            else
            {
                ExplorerCore.LogWarning($"Could not find any Type by the name '{text}'!");
            }
        }

        private void ConstructLists()
        {
            var listHolder = UIFactory.CreateUIObject("ListHolders", UIRoot);
            UIFactory.SetLayoutGroup<HorizontalLayoutGroup>(listHolder, false, true, true, true, 8, 2, 2, 2, 2);
            UIFactory.SetLayoutElement(listHolder, minHeight: 150, flexibleWidth: 9999, flexibleHeight: 9999);

            // Components

            var rightGroup = UIFactory.CreateUIObject("ComponentGroup", listHolder);
            UIFactory.SetLayoutElement(rightGroup, flexibleWidth: 9999, flexibleHeight: 9999);
            UIFactory.SetLayoutGroup<VerticalLayoutGroup>(rightGroup, false, false, true, true, 2);

            var compLabel = UIFactory.CreateLabel(rightGroup, "CompListTitle", "Components", TextAnchor.MiddleCenter, default, false, 16);
            UIFactory.SetLayoutElement(compLabel.gameObject, flexibleWidth: 9999);

            // Add Child
            var addComponentRow = UIFactory.CreateUIObject("AddComponentPageRow", rightGroup);
            UIFactory.SetLayoutGroup<HorizontalLayoutGroup>(addComponentRow, false, false, true, true, 2);
            
            addCompInput = UIFactory.CreateInputField(addComponentRow, "AddCompInput", "Enter a Component type...");
            UIFactory.SetLayoutElement(addCompInput.Component.gameObject, minHeight: 25, preferredWidth: 9999);

            ButtonRef addCompButton = UIFactory.CreateButton(addComponentRow, "AddCompButton", "Add Comp");
            UIFactory.SetLayoutElement(addCompButton.Component.gameObject, minHeight: 25, minWidth: 80);
            addCompButton.OnClick += () => { OnAddComponentClicked(addCompInput.Text); };

            // comp autocompleter
            new TypeCompleter(typeof(ValueType), addCompInput, false, false, false);
            
            componentScroll = UIFactory.CreateScrollPool<ECSComponentCell>(rightGroup, "ComponentList", out GameObject compObj,
                out GameObject compContent, new Color(0.11f, 0.11f, 0.11f));
            UIFactory.SetLayoutElement(compObj, flexibleHeight: 9999);
            UIFactory.SetLayoutElement(compContent, flexibleHeight: 9999);

            ecsComponentList = new ECSComponentList(componentScroll, GetComponents);
            ecsComponentList.Parent = this;
            componentScroll.Initialize(ecsComponentList); 
        }

        public override void CloseInspector()
        {
            ECSHelper.WorldDestroyed -= OnWorldDestroyed;
            InspectorManager.ReleaseInspector(this);
        }

        public void UpdateComponents()
        {
            ecsComponentList.RefreshData();
            ecsComponentList.ScrollPool.Refresh(true);
        }

        public CacheObjectBase ParentCacheObject => null;
        public bool CanWrite => true;
    }
}