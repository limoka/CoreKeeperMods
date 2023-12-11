using System;
using Mods.UnityExplorer.Scripts.ECS;
using Unity.Entities;
using UnityExplorer;
using UnityExplorer.CacheObject;
using UnityExplorer.CacheObject.Views;
using UniverseLib.Runtime;

namespace ECSExtension.Cache
{
    public sealed class CacheComponent<T> : BaseComponentCache<T> where T : unmanaged, IComponentData
    {
        public CacheComponent(EntityInspector inspector) : base(inspector) { }

        public override void TrySetUserValue(object value)
        {
            if (value is T component)
            {
                entityManager.SetComponentData(entity, component);
            }
        }

        public override object TryEvaluate()
        {
            try
            {
                return entityManager.GetComponentData<T>(entity);
            }
            catch (Exception e)
            {
                ExplorerCore.LogWarning(e);
            }

            return null;
        }
    }
}