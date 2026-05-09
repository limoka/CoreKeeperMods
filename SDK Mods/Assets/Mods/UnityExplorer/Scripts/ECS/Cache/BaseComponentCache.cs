using System;
using ECSExtension;
using Unity.Entities;
using UnityExplorer;
using UnityExplorer.CacheObject;
using UnityExplorer.CacheObject.Views;
// ReSharper disable VirtualMemberCallInConstructor

namespace ECSExtension.Cache
{
    public abstract class BaseComponentCache<T>  : CacheObjectBase
    {
        private readonly EntityInspector _inspector;
        
        protected EntityManager EntityManager => _inspector.currentWorld.EntityManager;
        protected Entity Entity => _inspector.currentEntity;

        protected BaseComponentCache(EntityInspector inspector)
        {
            Owner = inspector;
            _inspector = inspector;
            SetFallbackType(typeof(T));
        }

        public override bool ShouldAutoEvaluate => true;
        public override bool HasArguments => false;
        public override bool CanWrite => true;
        public override bool RefreshFromSource => true;

        protected override bool TryAutoEvaluateIfUnitialized(CacheObjectCell objectcell)
        {
            CacheMemberCell cell = objectcell as CacheMemberCell;
            cell.EvaluateHolder.SetActive(false);

            if (State == ValueState.NotEvaluated)
                SetValueFromSource(TryEvaluate());

            return true;
        }
    }
}