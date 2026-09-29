using System;
using System.Linq;
using System.Reflection;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityExplorer;
using UnityExplorer.CacheObject;
using UnityExplorer.CacheObject.IValues;
using UniverseLib;

namespace Mods.UnityExplorer.Scripts.ECS.Cache
{
    public class InteractiveBlobReference : InteractiveList
    {
        private bool ContainsBlobArray = false;
        
        public override void CacheEntries(object value)
        {
            if (CheckBlobArrayRef(value))
            {
                var elementType = value.GetType().GenericTypeArguments.FirstOrDefault()!;

                if (ContainsBlobArray)
                {
                    var arrayElementType = elementType.GenericTypeArguments.FirstOrDefault();
                    if (arrayElementType == null) return;

                    var genericMethod = typeof(InteractiveBlobReference)
                        .GetMethod(nameof(CacheBlobArrayRef))!
                        .MakeGenericMethod(arrayElementType);

                    genericMethod.Invoke(this, new[] { value });
                }
                else
                {
                    var genericMethod = typeof(InteractiveBlobReference)
                        .GetMethod(nameof(CacheBlobRef))!
                        .MakeGenericMethod(elementType);

                    genericMethod.Invoke(this, new[] { value });
                }

                return;
            }

            base.CacheEntries(value);
        }
        
        private bool CheckBlobArrayRef(object value)
        {
            IsWritableGenericIList = false;
            ContainsBlobArray = false;
            
            try
            {
                Type type = value.GetType();
                if (type.IsConstructedGenericType && 
                    type.GetGenericTypeDefinition() == typeof(BlobAssetReference<>))
                {
                    var elementType = type.GenericTypeArguments.FirstOrDefault();
                    if (elementType == null) return false;

                    IsWritableGenericIList = true;
                    
                    if (elementType.IsConstructedGenericType &&
                        elementType.GetGenericTypeDefinition() == typeof(BlobArray<>))
                    {
                        ContainsBlobArray = true;
                    }
                }
            }
            catch (Exception ex)
            {
                ExplorerCore.LogWarning($"Exception processing IEnumerable for BlobArray<T> check: {ex.ReflectionExToString()}");
                IsWritableGenericIList = false;
                ContainsBlobArray = false;
            }

            return IsWritableGenericIList;
        }

        public void CacheBlobRef<T>(BlobAssetReference<T> reference) where T : unmanaged {
            NotSupportedLabel.gameObject.SetActive(false);

            if (!reference.IsCreated)
            {
                DisableCells(0);
                return;
            }
            
            ref var value = ref reference.Value;

            if (cachedEntries.Count == 0)
            {
                var cache = new CacheListEntry();
                cache.SetListOwner(this, 0);
                cachedEntries.Add(cache);
            }else if (cachedEntries.Count > 1)
            {
                DisableCells(1);
            }
            
            var cacheVal = cachedEntries[0];
            cacheVal.SetFallbackType(EntryType);
            cacheVal.SetValueFromSource(value);
        }
        
        public void CacheBlobArrayRef<T>(BlobAssetReference<BlobArray<T>> reference) where T : unmanaged {
            NotSupportedLabel.gameObject.SetActive(false);

            if (!reference.IsCreated)
            {
                DisableCells(0);
                return;
            }
            
            ref var array = ref reference.Value;

            if (array.Length > cachedEntries.Count)
            {
                for (int i = cachedEntries.Count; i < array.Length; i++)
                {
                    var cache = new CacheListEntry();
                    cache.SetListOwner(this, i);
                    cachedEntries.Add(cache);
                }
            }else if (cachedEntries.Count > array.Length)
            {
                DisableCells(array.Length);
            }

            for (int i = 0; i < array.Length; i++)
            {
                var  cache = cachedEntries[i];
                cache.SetFallbackType(EntryType);
                cache.SetValueFromSource(array[i]);
            }
        }

        private void DisableCells(int length)
        {
            for (int i = cachedEntries.Count - 1; i >= length; i--)
            {
                CacheListEntry cache = cachedEntries[i];
                if (cache.CellView != null)
                    cache.UnlinkFromView();

                cache.ReleasePooledObjects();
                cachedEntries.RemoveAt(i);
            }
        }
    }
}