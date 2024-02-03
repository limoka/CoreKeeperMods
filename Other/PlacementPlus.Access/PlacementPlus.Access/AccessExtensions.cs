using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Security;
using System.Security.Permissions;
using PugTilemap;
using UnityEngine;

[assembly: InternalsVisibleTo("PlacementPlus")]
[module: UnverifiableCode]
#pragma warning disable 618
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
#pragma warning restore 618

namespace PlacementPlus.Access;

internal static class AccessExtensions
{
    internal static PlayerController GetSlotOwner_Public(this PlacementHandler placementHandler)
    {
        return placementHandler.slotOwner;
    }
    
    internal static Vector2Int GetCurrentSize_Public(this PlacementHandler placementHandler)
    {
        return placementHandler.GetCurrentSize();
    }
    
    internal static ObjectInfo GetInfoAboutObjectToPlace_Public(this PlacementHandler placementHandler)
    {
        return placementHandler.infoAboutObjectToPlace;
    }
    
    internal static void SetInfoAboutObjectToPlace_Public(this PlacementHandler placementHandler, ObjectInfo newObject)
    {
        placementHandler.infoAboutObjectToPlace = newObject;
    }
    
    internal static int CanPlaceObjectAtPosition_Public(this PlacementHandler placementHandler, Vector3Int posToPlaceAt, int width, int height)
    {
        return placementHandler.CanPlaceObjectAtPosition(posToPlaceAt, width, height);
    }
    
    internal static Dictionary<Vector3Int, bool> GetTilesChecked_Public(this PlacementHandlerPainting placementHandler)
    {
        return placementHandler.tilesChecked;
    }
    
    internal static List<PlacementHandlerDigging.DiggableEntityAndInfo> GetDiggableObjects_Public(this PlacementHandlerDigging placementHandler)
    {
        return placementHandler.diggableObjects;
    }

    internal static bool GetAllowPlacingAnywhere_Public()
    {
        return PlacementHandler.allowPlacingAnywhere;
    }
    
    internal static void SetAllowPlacingAnywhere_Public(bool value)
    {
        PlacementHandler.allowPlacingAnywhere = value;
    }
    
    internal static void StartCooldownForItem_Public(this EquipmentSlot slot, float cooldown, bool isRegularHit = false)
    {
        slot.StartCooldownForItem(cooldown, isRegularHit);
    }
    
    internal static void PlayEffect_Public(this PaintToolSlot slot, int colorIndex, Vector3 effectPos)
    {
        slot.PlayEffect(colorIndex, effectPos);
    }
    
    internal static Tileset PaintIndexToTileset_Public(this PaintToolSlot slot, int colorIndex, TileInfo tileInfo)
    {
        return slot.PaintIndexToTileset(colorIndex, tileInfo);
    }
    
    internal static float GetSLOT_COOLDOWN_Public(this PlaceObjectSlot slot)
    {
        return slot.SLOT_COOLDOWN;
    }
    
    internal static float GetSLOT_COOLDOWN_Public(this PaintToolSlot slot)
    {
        return slot.SLOT_COOLDOWN;
    }
    
    internal static float GetSLOT_COOLDOWN_Public(this ShovelSlot slot)
    {
        return ShovelSlot.DIG_COOLDOWN;
    }
}