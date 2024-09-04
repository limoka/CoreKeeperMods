using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Security;
using System.Security.Permissions;
using PlayerEquipment;
using PugTilemap;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

[assembly: InternalsVisibleTo("PlacementPlus")]
[module: UnverifiableCode]
#pragma warning disable 618
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
#pragma warning restore 618

namespace PlacementPlus.Access;

internal static class AccessExtensions
{
    public static bool FindPlaceablePositionFromMouseOrJoystick(
        Entity placementPrefab,
        int width,
        int height,
        NativeHashMap<int3, bool> tilesChecked,
        ref NativeList<PlacementHandler.EntityAndInfoFromPlacement> diggableEntityAndInfos,
        in EquipmentUpdateAspect equipmentUpdateAspect,
        in EquipmentUpdateSharedData equipmentUpdateSharedData,
        in LookupEquipmentUpdateData equipmentUpdateLookupData)
    {
        return PlacementHandler.FindPlaceablePositionFromMouseOrJoystick(
            placementPrefab,
            width,
            height,
            tilesChecked,
            ref diggableEntityAndInfos,
            equipmentUpdateAspect,
            equipmentUpdateSharedData,
            equipmentUpdateLookupData
        );
    }

    public static void FindPlaceablePositionFromOwnerDirection(
        Entity placementPrefab,
        int width,
        int height,
        NativeHashMap<int3, bool> tilesChecked,
        ref NativeList<PlacementHandler.EntityAndInfoFromPlacement> diggableEntityAndInfos,
        in EquipmentUpdateAspect equipmentUpdateAspect,
        in EquipmentUpdateSharedData equipmentUpdateSharedData,
        in LookupEquipmentUpdateData equipmentUpdateLookupData)
    {
        PlacementHandler.FindPlaceablePositionFromOwnerDirection(
            placementPrefab, 
            width,
            height, 
            tilesChecked,
            ref diggableEntityAndInfos,
            equipmentUpdateAspect,
            equipmentUpdateSharedData,
            equipmentUpdateLookupData
            );
    }

    public static int CanPlaceObjectAtPosition_PlacePublic(
        Entity placementPrefab,
        int3 posToPlaceAt,
        int width,
        int height,
        NativeHashMap<int3, bool> tilesChecked,
        in EquipmentUpdateAspect equipmentUpdateAspect,
        in EquipmentUpdateSharedData equipmentUpdateSharedData,
        in LookupEquipmentUpdateData equipmentUpdateLookupData
        ) {
        return PlacementHandler.CanPlaceObjectAtPosition(
            placementPrefab,
            posToPlaceAt,
            width, 
            height,
            tilesChecked,
            equipmentUpdateAspect,
            equipmentUpdateSharedData,
            equipmentUpdateLookupData
        );
    }

    public static int CanPlaceObjectAtPosition_ShovelPublic(
        Entity placementPrefab,
        int3 pos,
        int width,
        int height,
        ref NativeList<PlacementHandler.EntityAndInfoFromPlacement> diggableEntityAndInfos,
        in EquipmentUpdateAspect equipmentUpdateAspect,
        in EquipmentUpdateSharedData equipmentUpdateSharedData,
        in LookupEquipmentUpdateData equipmentUpdateLookupData)
    {
        return PlacementHandlerDigging.CanPlaceObjectAtPosition(
            placementPrefab,
            pos,
            width,
            height,
            ref diggableEntityAndInfos,
            equipmentUpdateAspect,
            equipmentUpdateSharedData,
            equipmentUpdateLookupData
        );
    }
}