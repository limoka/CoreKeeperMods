using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.Collections;
using Unity.Entities;
using UniverseLib;

namespace UnityExplorer.CacheObject.IValues
{
    public class ECSInteractiveCollection : InteractiveList
    {
        public override void CacheEntries(object value)
        {
            if (CheckINativeList(value))
            {
                var entryType = value.GetType().GenericTypeArguments.FirstOrDefault();

                var genericMethod = typeof(ECSInteractiveCollection)
                    .GetMethod(nameof(CacheINativeList))
                    .MakeGenericMethod(entryType);

                genericMethod.Invoke(this, new[] { value });

                return;
            }

            base.CacheEntries(value);
        }
        
        private bool CheckINativeList(object value)
        {
            try
            {
                Type type = value.GetType();
                if (type.GetInterfaces().Any(it => it.IsGenericType && it.GetGenericTypeDefinition() == typeof(INativeList<>)))
                    IsWritableGenericIList = true;
                else
                    IsWritableGenericIList = false;

                if (IsWritableGenericIList)
                {
                    // Find the "this[int index]" property.
                    // It might be a private implementation.
                    foreach (PropertyInfo prop in type.GetProperties(ReflectionUtility.FLAGS))
                    {
                        if ((prop.Name == "Item"
                             || (prop.Name.StartsWith("Unity.Collections.INativeList<") && prop.Name.EndsWith(">.Item")))
                            && prop.GetIndexParameters() is ParameterInfo[] parameters
                            && parameters.Length == 1
                            && parameters[0].ParameterType == typeof(int))
                        {
                            genericIndexer = prop;
                            break;
                        }
                    }

                    if (genericIndexer == null)
                    {
                        ExplorerCore.LogWarning($"Failed to find indexer property for INativeList<T> type '{type.FullName}'!");
                        IsWritableGenericIList = false;
                    }
                }
            }
            catch (Exception ex)
            {
                ExplorerCore.LogWarning($"Exception processing IEnumerable for INativeList<T> check: {ex.ReflectionExToString()}");
                IsWritableGenericIList = false;
            }

            return IsWritableGenericIList;
        }

        public void CacheINativeList<T>(INativeList<T> list) where T : unmanaged {
            NotSupportedLabel.gameObject.SetActive(false);

            if (list.Length > cachedEntries.Count)
            {
                for (int i = cachedEntries.Count; i < list.Length; i++)
                {
                    var cache = new CacheListEntry();
                    cache.SetListOwner(this, i);
                    cachedEntries.Add(cache);
                }
            }else if (cachedEntries.Count > list.Length)
            {
                for (int i = cachedEntries.Count - 1; i >= list.Length; i--)
                {
                    CacheListEntry cache = cachedEntries[i];
                    if (cache.CellView != null)
                        cache.UnlinkFromView();

                    cache.ReleasePooledObjects();
                    cachedEntries.RemoveAt(i);
                }
            }

            for (int i = 0; i < list.Length; i++)
            {
                var  cache = cachedEntries[i];
                cache.SetFallbackType(EntryType);
                cache.SetValueFromSource(list[i]);
            }
        }
    }
}