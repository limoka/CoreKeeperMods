using Pug.Conversion;
using PugMod;
using UnityEngine;
using UnityEngine.Scripting;

namespace Mods.MovableSpawners.Scripts.Data
{
    public class ModDropLootAuthoring : MonoBehaviour
    {
        public string objectName;
        public int amount = 1;
    }

    [Preserve]
    public class ModDropLootConverter : SingleAuthoringComponentConverter<ModDropLootAuthoring>
    {
        protected override void Convert(ModDropLootAuthoring authoring)
        {
            EnsureHasBuffer<DropsLootBuffer>();

            AddToBuffer(new DropsLootBuffer
            {
                lootDropID = API.Authoring.GetObjectID(authoring.objectName),
                amount = authoring.amount,
                multiplayerAmountAdditionScaling = 0,
                skipIfScanned = default,
                requiredContentBundle = default
            });
            
            AddComponentData(new ChanceToDropLootCD
            {
                chance = 1f
            });
        }
    }
}