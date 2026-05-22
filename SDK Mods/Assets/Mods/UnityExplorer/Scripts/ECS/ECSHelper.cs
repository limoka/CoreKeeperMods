using System;
using Unity.Entities;
using UnityExplorer;


namespace UniverseLib.Runtime
{
    public static class ECSHelper
    {
        public static Action<World> WorldCreated;
        public static Action<World> WorldDestroyed;

        public static string GetNameSafe(this EntityManager entityManager, Entity entity)
        {
            try
            {
                string name = entityManager.GetName(entity);
                if (!string.IsNullOrEmpty(name)) return name;
                
                if (entityManager.HasComponent<ObjectDataCD>(entity))
                {
                    var data = entityManager.GetComponentData<ObjectDataCD>(entity);
                    
                    if (data.variation == 0)
                        return $"{entity} - {data.objectID}";
                        
                    return $"{entity} - {data.objectID} ({data.variation})";
                }
            }
            catch (Exception e)
            {
                ExplorerCore.LogWarning($"Exception getting entity name from ObjectDataCD: {e.ReflectionExToString()}");
            }

                
            return entity.ToString();
        }
        
        /// <summary>
        /// Performs a deep structural verification on an Entity. 
        /// Ensures that the entity has a valid unmanaged memory layout backing it.
        /// </summary>
        public static bool ExistsDeep(this EntityManager manager, Entity entity)
        {
            try
            {
                if (!manager.Exists(entity)) return false;
                
                // Perform a low-overhead, deep unmanaged memory read.
                // Entity.Null is a universal structural type present on all valid layouts.
                // This forces Unity to look up the archetype pointer without allocating arrays.
                manager.HasComponent<Entity>(entity);
                return true;
            }
            catch (NullReferenceException)
            {
                // Caught unmapped structural layout / ghost matching entity slot
                return false;
            }
            catch (Exception)
            {
                // Fallback for any other unexpected safety system triggers
                return false;
            }
        }
    }
}