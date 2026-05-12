using Pug.Conversion;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Scripting;

namespace Mods.MovableSpawners.Scripts.Data
{
    public class ModSummonAreaAuthoring : MonoBehaviour
    {
        public ObjectID objectToSpawn;
    }

    public struct ModSummonAreaCD : IComponentData
    {
        public ObjectID objectToSpawn;
    }

    [Preserve]
    public class ModSummonAreaConverter : SingleAuthoringComponentConverter<ModSummonAreaAuthoring>
    {
        protected override void Convert(ModSummonAreaAuthoring authoring)
        {
            AddComponentData(new ModSummonAreaCD
            {
                objectToSpawn = authoring.objectToSpawn,
            });
        }
    }
}