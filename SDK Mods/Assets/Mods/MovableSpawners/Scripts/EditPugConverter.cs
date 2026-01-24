using MovableSpawners;
using Pug.Conversion;
using UnityEngine;

namespace Mods.MovableSpawners.Scripts
{
    public class EditPugConverter : Converter
    {
        public override void Convert(GameObject authoring)
        {
            var entityData = authoring.GetComponent<EntityMonoBehaviourData>();
            if (entityData == null ||
                entityData.objectInfo.objectID != ObjectID.SummonArea) return;
            
            MovableSpawnersMod.Log.LogInfo($"Making {entityData.objectInfo.objectID}, {entityData.objectInfo.variation} placeable");
            
            SetProperty("PlaceableObject/placeableObject");
            SetProperty("PlaceableObject/variationToPlace", entityData.objectInfo.variation);
            SetProperty("PlaceableObject/canBePlacedOnAnyWalkableTile");
        }
    }
}