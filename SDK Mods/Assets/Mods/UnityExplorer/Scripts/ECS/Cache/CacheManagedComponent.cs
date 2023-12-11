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