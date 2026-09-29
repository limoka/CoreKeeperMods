using System.Collections.Generic;
using HarmonyLib;
using Mods.PlacementPlus.Scripts.Util;
using PlayerEquipment;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace PlacementPlus
{
    [HarmonyPatch]
    public static class EquipmentSystem_Patch
    {
        internal static PlacementPlusLookups plusLookupsClient;
        internal static PlacementPlusLookups plusLookupsServer;

        // Determines if the server-side prefix is alive in the active environment (DLL renaming, 
        // singleplayer, or hosting) — if it is alive, the existing path handles grid placement, 
        // so ServerBrushExecutor remains dormant. 
        // ⚠️ This must be evaluated based on a "recent few seconds" window: A simple bool that 
        // remains true once set gets contaminated when a player switches from singleplayer → dedicated server 
        // (without restarting the game). This causes prediction spawning to revive on dedicated servers, 
        // reintroducing the freeze issue (Observed on 08-25).
        private static float s_serverPrefixLastSeenReal = -100f;

        internal static void MarkServerPrefixAlive()
        {
            s_serverPrefixLastSeenReal = UnityEngine.Time.realtimeSinceStartup;
        }

        internal static bool serverPrefixAlive =>
            UnityEngine.Time.realtimeSinceStartup - s_serverPrefixLastSeenReal < 3f;

        internal static PlacementPlusLookups GetLookups(bool isServer)
        {
            return isServer ? plusLookupsServer : plusLookupsClient;
        }

        // On Burst-enabled dedicated servers, this prefix (all Harmony patches here) is never called — 
        // in that environment, ServerBrushExecutor places the grid instead (DLL renaming is no longer 
        // required since 1.2.0). This path is only active on singleplayer, host, or servers that renamed the DLL.
        [HarmonyPatch(typeof(EquipmentUpdateSystem), nameof(EquipmentUpdateSystem.OnUpdate))]
        [HarmonyPrefix]
        public static void OnUpdate(EquipmentUpdateSystem __instance, ref SystemState state)
        {
            bool isServer = state.WorldUnmanaged.IsServer();

            if (isServer)
            {
                MarkServerPrefixAlive();
                plusLookupsServer.Init(ref state);
            }
            else
                plusLookupsClient.Init(ref state);

            ApplySledgeSize(ref state, isServer);
        }

        // Stock arc angle per sledge type, so step 0 can put it back.
        private static readonly Dictionary<ObjectID, ArcAngle> s_sledgeArcOriginals = new Dictionary<ObjectID, ArcAngle>();

        // Original attackFXType — requires preservation and restoration, identical to the 'arc' obligation.
        private static readonly Dictionary<ObjectID, AttackFXType> s_sledgeFxOriginals = new Dictionary<ObjectID, AttackFXType>();

        // Name check allocates a string, so it is cached — this now runs every
        // frame for whoever is holding any melee weapon.
        private static readonly Dictionary<ObjectID, bool> s_isSledge = new Dictionary<ObjectID, bool>();

        private static bool IsSledge(ObjectID id)
        {
            if (!s_isSledge.TryGetValue(id, out bool isSledge))
            {
                isSledge = id.ToString().Contains("Sledge");
                s_isSledge[id] = isSledge;
            }
            return isSledge;
        }

        // The sledgehammer's hit area lives in MeleeWeaponCD on the (shared)
        // equipment prefab. This runs every frame but only writes on mismatch:
        // CoreEnhance's SledgeRange writes vanilla 1.4 into the same field once
        // a second EVEN WITH ITS SWITCH OFF, so a fire-and-forget write here
        // loses to it half the time (size visibly ping-ponged, 2026-08-21).
        // internal: Also called by ServerModCommandSystem — On Burst-enabled servers, 
        // this prefix itself is never called (F-3 experiment), so our own continuous 
        // system must apply it instead. It is harmless even if called from both sides 
        // because it uses a reconcile check that only writes when they differ.
        internal static void ApplySledgeSize(ref SystemState state, bool isServer)
        {
            if (PlacementPlusMod.sledgeSize == null) return;

            int step = PlacementPlusMod.sledgeSize.Value;
            bool square = PlacementPlusMod.sledgeSquare?.Value ?? true;
            // On a dedicated server the client's cfg is not the one that counts —
            // once the server has broadcast its values, prediction uses those.
            if (!isServer)
            {
                if (PlacementPlusMod.sledgeSizeSynced >= 0)
                    step = PlacementPlusMod.sledgeSizeSynced;
                if (PlacementPlusMod.sledgeShapeSynced >= 0)
                    square = PlacementPlusMod.sledgeShapeSynced == 1;
            }

            float baseSize = PlacementPlusMod.SledgeStepToBase(step, square);

            var query = state.GetEntityQuery(
                ComponentType.ReadOnly<EquippedObjectCD>(),
                ComponentType.ReadOnly<PlayerGhost>());
            using var players = query.ToEntityArray(Allocator.Temp);

            var em = state.EntityManager;
            foreach (Entity player in players)
            {
                var held = em.GetComponentData<EquippedObjectCD>(player);
                Entity prefab = held.equipmentPrefab;

                if (prefab == Entity.Null || !em.HasComponent<MeleeWeaponCD>(prefab)) continue;
                if (!IsSledge(held.containedObject.objectData.objectID)) continue;

                var melee = em.GetComponentData<MeleeWeaponCD>(prefab);
                ObjectID heldId = held.containedObject.objectData.objectID;

                // Arc weapons get their cross axis doubled by GetSizeOfHitCollider
                // (a 180° swing is wider than deep), which turned 5x5 into 9x5.
                // arc360 drops that factor and gives a true square while enlarged;
                // stock angle comes back at step 0 or in ARC shape mode.
                // attackFXType is changed along with it: When GetHitCollider encounters 
                // an Arc+arc360 combination, it turns the entity validation collider into a "sphere". 
                // As a result, all 9x9 tiles (square loop) were destroyed, but edge placeables (conveyors) 
                // survived because they fell outside the circle radius (Observed on 2026-08-21). 
                // 'Line' takes the box branch, ensuring its entity validation matches the tile and preview perfectly.
                ArcAngle wantArc;
                AttackFXType wantFx;
                if (step > 0 && square)
                {
                    if (melee.arcAngle != ArcAngle.arc360 && !s_sledgeArcOriginals.ContainsKey(heldId))
                        s_sledgeArcOriginals[heldId] = melee.arcAngle;
                    if (melee.attackFXType != AttackFXType.Line && !s_sledgeFxOriginals.ContainsKey(heldId))
                        s_sledgeFxOriginals[heldId] = melee.attackFXType;
                    wantArc = ArcAngle.arc360;
                    wantFx = AttackFXType.Line;
                }
                else
                {
                    wantArc = s_sledgeArcOriginals.TryGetValue(heldId, out var stockArc)
                        ? stockArc
                        : melee.arcAngle;
                    wantFx = s_sledgeFxOriginals.TryGetValue(heldId, out var stockFx)
                        ? stockFx
                        : melee.attackFXType;
                }

                if (melee.baseHitColliderSize == baseSize && melee.arcAngle == wantArc &&
                    melee.attackFXType == wantFx) continue;

                melee.baseHitColliderSize = baseSize;
                melee.arcAngle = wantArc;
                melee.attackFXType = wantFx;
                em.SetComponentData(prefab, melee);
            }
        }
    }
}