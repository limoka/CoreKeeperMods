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
            string name = entityManager.GetName(entity);
            if (!string.IsNullOrEmpty(name)) return name;
            
            try
            {
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
    }
}