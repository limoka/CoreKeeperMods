using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using PugMod;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace PlacementPlus
{
    [HarmonyPatch]
    public static class MapUpdateSystem_Patch
    {
        internal static List<BrushRect> mapUpdateRects = new List<BrushRect>();
        private static int skipFrames;

        internal static void RevealRect(BrushRect rect)
        {
            mapUpdateRects.Add(rect);
            skipFrames += 5;
        }

        [HarmonyPatch(typeof(MapUpdateSystem), "UpdateTiles")]
        [HarmonyPrefix]
        private static void PreUpdateTiles(MapUpdateSystem __instance)
        {
            if (mapUpdateRects.Count <= 0) return;
            skipFrames--;
            if (skipFrames > 0) return;

            var colorLookup = API.Client.World.GetOrCreateSystemManaged<TileTypeColorLookupSystem>().CreateLookupHelper();
            var tileLookup = Manager.multiMap.GetTileLayerLookup();

            foreach (BrushRect brushRect in mapUpdateRects)
            {
                foreach (int3 pos in brushRect)
                {
                    var localPos = EntityMonoBehaviour.ToRenderFromWorld(pos.ToInt2());
                    var surfaceTile = tileLookup.GetTopTile(localPos);
                    Color color = colorLookup.GetColorByTileType(surfaceTile.tileset, surfaceTile.tileType);
                    __instance.SetColorOverridesThisUpdate(new Vector3Int(pos.x, 0, pos.y), color);
                }
            }

            mapUpdateRects.Clear();
        }
    }
}