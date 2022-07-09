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
            if (PugDatabase.HasComponent<DirectionBasedOnVariationCD>(item))
            {
                DirectionBasedOnVariationCD variationCd = PugDatabase.GetComponent<DirectionBasedOnVariationCD>(item);
                if (!variationCd.alignWithNearbyAffectorsWhenPlaced)
                {
                    BrushExtension.PlayEffects(__instance, initialPos, __instance.placementHandler.infoAboutObjectToPlace);
                    __instance.StartCooldownForItem(__instance.SLOT_COOLDOWN );

                    Vector3Int pos = worldPos + initialPos;
                    
                    if (__instance.placementHandler.CanPlaceObjectAtPosition(pos, 1, 1) <= 0) return false;
                    if (!pc.CanConsumeEntityInSlot(__instance, 1)) return false;
                    
                    ObjectDataCD newObj = item;
                    newObj.variation = BrushExtension.currentRotation;
                    float3 targetPos = pos.ToFloat3();

                    pc.instantiatePrefabsSystem.PrespawnEntity(newObj, targetPos);
                    pc.playerCommandSystem.CreateEntity(newObj.objectID, targetPos, newObj.variation);
                    
                    pc.playerInventoryHandler.Consume(pc.equippedSlotIndex, 1, true);
                    pc.SetEquipmentSlotToNonUsableIfEmptySlot(__instance);

                    return false;
                }
            }
            
            return true;
        }

        ObjectInfo itemInfo = __instance.placementHandler.infoAboutObjectToPlace;
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