using System;
using Mods.UnityExplorer.Scripts.ECS;
using Unity.Entities;
using UnityExplorer;
using UnityExplorer.CacheObject;
using UnityExplorer.CacheObject.Views;

namespace ECSExtension.Cache
{
    public sealed class CacheSharedComponent<T> : BaseComponentCache<T> where T : unmanaged, ISharedComponentData
    {
        public CacheSharedComponent(EntityInspector inspector) : base(inspector) { }
        
        public override void TrySetUserValue(object value)
        {
            if (value is T component)
            {
                EntityManager.SetSharedComponent(Entity, component);
            }
        }

        public override object TryEvaluate()
        {
            try
            {
                return EntityManager.GetSharedComponent<T>(Entity);
            }
            catch (Exception e)
            {
                ExplorerCore.LogWarning(e);
            }

            return null;
        }
    }
}