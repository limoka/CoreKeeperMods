using System;
using System.Collections.Generic;
using ECSExtension.Cache;
using HarmonyLib;
using Mods.UnityExplorer.Scripts.ECS;
using Unity.Collections;
using Unity.Entities;
using UnityExplorer;
using UniverseLib;
using UniverseLib.Runtime;
using UniverseLib.UI.Widgets.ScrollView;

namespace ECSExtension
{
    public class ECSComponentList : ButtonNativeListHandler<ComponentType, ECSComponentCell>
    {
        public EntityInspector Parent;

        public ECSComponentList(ScrollPool<ECSComponentCell> scrollPool, Func<NativeArray<ComponentType>> getEntriesMethod)
            : base(scrollPool, getEntriesMethod, null, null, null)
        {
            SetICell = SetComponentCell;
            ShouldDisplay = CheckShouldDisplay;
            OnCellClicked = OnComponentClicked;
        }

        public void Clear()
        {
            RefreshData();
            ScrollPool.Refresh(true, true);
        }

        private bool CheckShouldDisplay(ComponentType _, string __) => true;

        public override void OnCellBorrowed(ECSComponentCell cell)
        {
            base.OnCellBorrowed(cell);

            cell.OnDestroyClicked += OnDestroyClicked;
        }

        private void OnComponentClicked(int index)
        {
            var entries = GetEntries();

            if (index < 0 || index >= entries.Length)
                return;

            try
            {
                InvokeForComponent(entries[index]);
            }
            catch (Exception e)
            {
                string componentName = entries[index].GetManagedType().FullName;
                ExplorerCore.LogWarning($"Error getting component data {componentName}, message: {e.Message}, stacktrace:\n {e.StackTrace}");
            }
        }

        private void InvokeForComponent(ComponentType comp)
        {
            Type componentType = comp.GetManagedType();

            var category = TypeManager.GetTypeInfo(comp.TypeIndex).Category;

            if (category == TypeManager.TypeCategory.BufferData)
            {
                InvokeMethod(nameof(InspectBuffer), componentType);
            }
            else if (category == TypeManager.TypeCategory.ComponentData)
            {
                InvokeMethod(nameof(InspectComponent), componentType);
            }else if (category == TypeManager.TypeCategory.ISharedComponentData)
            {
                InvokeMethod(nameof(InspectSharedComponent), componentType);
            }else if (category == TypeManager.TypeCategory.UnityEngineObject)
            {
                InvokeMethod(nameof(InspectMangedComponent), componentType);
            }
        }

        private void InvokeMethod(string methodName, Type componentType)
        {
            var method = typeof(ECSComponentList).GetMethod(methodName, AccessTools.all);
            method.MakeGenericMethod(componentType)
                .Invoke(this, Array.Empty<object>());
        }

        private void InspectBuffer<T>() where T : unmanaged, IBufferElementData
        {
            DynamicBuffer<T> dynamicBuffer = Parent.GetDynamicBuffer<T>();
            InspectorManager.Inspect(new BufferView<T>(dynamicBuffer));
        }
        
        private void InspectComponent<T>() where T : unmanaged, IComponentData
        {
            CacheComponent<T> data = Parent.GetComponentData<T>();
            InspectorManager.Inspect(data.TryEvaluate(), data);
        }
        
        private void InspectSharedComponent<T>() where T : unmanaged, ISharedComponentData
        {
            CacheSharedComponent<T> data = Parent.GetSharedComponentData<T>();
            InspectorManager.Inspect(data.TryEvaluate(), data);
        }
        
        private void InspectMangedComponent<T>() where T : class, IComponentData, new()
        {
            CacheManagedComponent<T> data = Parent.GetManagedComponentData<T>();
            InspectorManager.Inspect(data.TryEvaluate(), data);
        }

        private void OnDestroyClicked(int index)
        {
            try
            {
                var entries = GetEntries();
                var comp = entries[index];

                Parent.RemoveComponent(comp);
                Parent.UpdateComponents();
            }
            catch (Exception ex)
            {
                ExplorerCore.LogWarning($"Exception destroying Component: {ex.ReflectionExToString()}");
            }
        }

        private static readonly Dictionary<string, string> compToStringCache = new Dictionary<string, string>();

        // Called from ButtonListHandler.SetCell, will be valid
        private void SetComponentCell(ECSComponentCell cell, int index)
        {
            var entries = GetEntries();
            cell.Enable();

            try
            {
                cell.ConfigureCell(entries[index]);
            }
            catch (Exception e)
            {
                ExplorerCore.Log($"Error setting component name: {e.Message}, stacktrace: {e.StackTrace}");
            }
        }
    }
}