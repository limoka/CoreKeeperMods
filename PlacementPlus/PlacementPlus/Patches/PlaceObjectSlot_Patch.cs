using System;
using HarmonyLib;
using PugTilemap;
using Unity.Mathematics;
using UnityEngine;

namespace PlacementPlus;

[HarmonyPatch]
public static class PlaceObjectSlot_Patch
{
    [HarmonyPatch(typeof(PlaceObjectSlot), nameof(PlaceObjectSlot.PlaceItem))]
    [HarmonyPrefix]
    public static bool OnPlace(PlaceObjectSlot __instance)
    {
        PlacementHandler.SetAllowPlacingAnywhere(false);

        PlayerController pc = __instance.slotOwner;
        ObjectDataCD item = pc.GetHeldObject();
        Vector3Int initialPos = __instance.placementHandler.bestPositionToPlaceAt;
        Vector3Int worldPos = new Vector3Int(pc.pugMapPosX, 0, pc.pugMapPosZ);

        if (BrushExtension.size == 0)
        {
            return true;
        }

        ObjectInfo itemInfo = PugDatabase.GetObjectInfo(item.objectID);
        if (!BrushExtension.IsItemValid(itemInfo)) return true;

        BrushExtension.PlayEffects(__instance, initialPos, itemInfo);
        BrushExtension.PlaceGrid(__instance, worldPos + initialPos, item, itemInfo);

        return false;
    }

    [HarmonyPatch(typeof(PaintToolSlot), nameof(PaintToolSlot.PlaceItem))]
    [HarmonyPrefix]
    public static bool OnPaint(PaintToolSlot __instance)
    {
        PlacementHandler.SetAllowPlacingAnywhere(false);
        if (BrushExtension.size == 0) return true;

        PlayerController pc = __instance.slotOwner;
        PlacementHandlerPainting handler = __instance.placementHandler.Cast<PlacementHandlerPainting>();
        bool entityExists = pc.world.EntityManager.Exists(handler.entityToPaint);
        if (entityExists) return true;

        ObjectDataCD item = __instance.objectReference;
        if (item.objectID <= 0) return true;
        if (!PugDatabase.HasComponent<PaintToolCD>(item)) return true;

        BrushExtension.PaintGrid(__instance, handler);

        return false;
    }
}