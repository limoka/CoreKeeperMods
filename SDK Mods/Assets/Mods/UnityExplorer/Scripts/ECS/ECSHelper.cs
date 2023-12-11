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
                return entity.ToString();
            }

            return name;
        }
    }
}