using System;
using Unity.Entities;


namespace UniverseLib.Runtime
{
    public static class ECSHelper
    {
        public static Action<World> WorldCreated;
        public static Action<World> WorldDestroyed;

        public static string GetNameSafe(this EntityManager entityManager, Entity entity)
        {
            string name = entityManager.GetName(entity);
            if (string.IsNullOrEmpty(name))
            {
                if (entityManager.HasComponent<ObjectDataCD>(entity))
                {
                    var data = entityManager.GetComponentData<ObjectDataCD>(entity);
                    
                    if (data.variation == 0)
                        return $"{entity} - {data.objectID}";
                    else
                        return $"{entity} - {data.objectID} ({data.variation})";
                }
                
                return entity.ToString();
            }

            return name;
        }
    }
}