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
        protected EntityManager entityManager;
        protected Entity entity;

        protected BaseComponentCache(EntityInspector inspector)
        {
            Owner = inspector;
            entityManager = inspector.currentWorld.EntityManager;
            entity = inspector.currentEntity;
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