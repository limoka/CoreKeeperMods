using MovableSpawners;
using PugConversion;
using UnityEngine;

namespace Mods.MovableSpawners.Scripts
{
    public class EditPugConverter : PugConverter
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