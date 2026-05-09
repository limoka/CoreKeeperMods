using System;
using Unity.Entities;
using UnityExplorer;

namespace ECSExtension.Cache
{
    public sealed class CacheManagedComponent<T> : BaseComponentCache<T> where T : class, IComponentData, new()
    {
        public CacheManagedComponent(EntityInspector inspector) : base(inspector) { }

        public override void TrySetUserValue(object value)
        {
            if (value is T component)
            {
                EntityManager.SetComponentData(Entity, component);
            }
        }

        public override object TryEvaluate()
        {
            try
            {
                return EntityManager.GetComponentData<T>(Entity);
            }
            catch (Exception e)
            {
                ExplorerCore.LogWarning(e);
            }

            return null;
        }
    }
}