using System;
using System.Collections.Generic;
using CoreLib;
using CoreLib.Data.Configuration;
using CoreLib.Submodule.ControlMapping;
using CoreLib.Submodule.Localization;
using CoreLib.Util.Extension;
using HarmonyLib;
using PlacementPlus.Components;
using PlacementPlus.Systems.Network;
using PlayerEquipment;
using PugMod;
using Rewired;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Object = UnityEngine.Object;
using Logger = CoreLib.Util.Logger;

namespace PlacementPlus
{
    public class PlacementPlusMod : IMod
    {
        public const string MODNAME = "Placement Plus";
        public const string VERSION = "2.2.0";

        public static Logger Log = new Logger(MODNAME);
        public static ConfigFile Config;
        private LoadedMod modInfo;

        #region Excludes

        public static HashSet<ObjectID> defaultExclude = new HashSet<ObjectID>
        {
            // (Do not put Obsidian here — the exclusion list is entirely disabled 
            //  by the IgnoreBuiltinExclude setting, so it cannot prevent item loss. 
            //  The actual protection is handled by the survival Obsidian guard 
            //  in ObjectPlacementLogic.PlaceAt. 08-26)
            ObjectID.WoodenWorkBench,
            ObjectID.TinWorkbench,
            ObjectID.IronWorkBench,
            ObjectID.ScarletWorkBench,
            ObjectID.OctarineWorkbench,
            ObjectID.FishingWorkBench,
            ObjectID.JewelryWorkBench,
            ObjectID.AdvancedJewelryWorkBench,

            ObjectID.Furnace,
            ObjectID.SmelterKiln,

            ObjectID.GreeneryPod,
            ObjectID.Carpenter,
            ObjectID.AlchemyTable,
            ObjectID.TableSaw,

            ObjectID.CopperAnvil,
            ObjectID.TinAnvil,
            ObjectID.IronAnvil,
            ObjectID.ScarletAnvil,
            ObjectID.OctarineAnvil,

            ObjectID.ElectronicsTable,
            ObjectID.RailwayForge,
            ObjectID.PaintersTable,
            ObjectID.AutomationTable,
            ObjectID.CartographyTable,
            ObjectID.SalvageAndRepairStation,
            ObjectID.DistilleryTable,

            ObjectID.ElectricityGenerator,
            ObjectID.WoodDoor,
            ObjectID.StoneDoor,
            ObjectID.ElectricalDoor,

            ObjectID.Minecart,
            ObjectID.Boat,
            ObjectID.SpeederBoat,
            ObjectID.Torch
        };

        // Was a cfg entry (ExcludeItems); fixed in code since 2026-08-20 so it
        // stays out of the config menu. The user's list was exactly this.
        public static HashSet<ObjectID> userExclude = new HashSet<ObjectID>
        {
            ObjectID.InventoryChest,
        };

        #endregion


        public static ConfigEntry<int> maxSize;
        public static ConfigEntry<bool> ignoreBuiltinExclude;
        public static ConfigEntry<bool> allowWallVariants;
        public static ConfigEntry<bool> sledgePreviewEnabled;
        public static ConfigEntry<float> sledgePreviewOpacity;
        public static ConfigEntry<bool> shortMessages;

        // ── Settings deliberately NOT bound to the config file (user decision
        // 2026-08-20: keep the config menu short). The wrapper classes keep the
        // ".Value" shape so the generated .g.cs copies compile unchanged.

        // The tool-tier cap is permanently lifted — the whole point of this
        // build is tools at MaxBrushSize.
        public class FixedTrue { public bool Value => true; }
        public static readonly FixedTrue ignoreToolTierLimit = new FixedTrue();

        // Auto-repeat interval for held +/- keys, was cfg MinHoldTime.
        public const float MIN_HOLD_TIME = 0.15f;

        // Sledgehammer size/shape live in our own PlacementPlus/SledgeState.cfg
        // (written through the setters below), NOT in the ConfigFile — a bound
        // entry would show up in the config menu, and on a dedicated server
        // editing it there does nothing anyway (the size keys are the real
        // interface). The tiny file keeps the values across server restarts.
        public class SledgeSizeEntry
        {
            public int Value
            {
                get => sledgeSizeValue;
                set { sledgeSizeValue = value; SaveSledgeState(); }
            }
        }

        public class SledgeSquareEntry
        {
            public bool Value
            {
                get => sledgeSquareValue;
                set { sledgeSquareValue = value; SaveSledgeState(); }
            }
        }

        public static readonly SledgeSizeEntry sledgeSize = new SledgeSizeEntry();
        public static readonly SledgeSquareEntry sledgeSquare = new SledgeSquareEntry();

        private static int sledgeSizeValue;
        private static bool sledgeSquareValue = true;

        private const string SLEDGE_STATE_PATH = "PlacementPlus/SledgeState.cfg";

        private static void LoadSledgeState()
        {
            try
            {
                if (!API.ConfigFilesystem.FileExists(SLEDGE_STATE_PATH)) return;

                string text = System.Text.Encoding.UTF8.GetString(
                    API.ConfigFilesystem.Read(SLEDGE_STATE_PATH));
                foreach (string line in text.Split('\n'))
                {
                    string[] pair = line.Trim().Split('=');
                    if (pair.Length != 2) continue;
                    if (pair[0] == "size" && int.TryParse(pair[1], out int size))
                        sledgeSizeValue = Math.Max(0, Math.Min(3, size));
                    if (pair[0] == "square" && bool.TryParse(pair[1], out bool square))
                        sledgeSquareValue = square;
                }
            }
            catch (Exception e)
            {
                Log.LogWarning($"Sledge state load failed: {e.Message}");
            }
        }

        private static void SaveSledgeState()
        {
            try
            {
                API.ConfigFilesystem.Write(SLEDGE_STATE_PATH, System.Text.Encoding.UTF8.GetBytes(
                    $"size={sledgeSizeValue}\nsquare={sledgeSquareValue}"));
            }
            catch (Exception e)
            {
                Log.LogWarning($"Sledge state save failed: {e.Message}");
            }
        }

        // Last SLEDGE_*_MESSAGE values received from the server (-1 = none yet).
        // On a dedicated server the client's own cfg is not authoritative, so
        // prediction uses these once they arrive.
        public static int sledgeSizeSynced = -1;
        public static int sledgeShapeSynced = -1;

        // Step 0 keeps the stock collider (1.4, a 3x3 tile hit).
        // Square shape (arc360): the tile hit is a square of half-extent
        // round(base/2) — even bases (4/6/8 → 5x5/7x7/9x9) dodge the
        // round-half-to-even edge cases. Arc shape keeps the stock swing, whose
        // cross axis is doubled, so the base is halved to make the step number
        // the swath's WIDTH instead (2/3/4 → 5x3/7x5/9x5); the game hard-caps
        // tile hits at 9x9 either way.
        public static float SledgeStepToBase(int step, bool square)
        {
            if (step <= 0) return 1.4f;
            return square ? 2f * step + 2f : step + 1f;
        }

        internal static ClientModCommandSystem commandSystem;

        private static float plusHoldTime;
        private static float minusHoldTime;
        private static bool lastReplaceState;
        
        internal static Player rwPlayer;

        public const string CHANGE_ORIENTATION = "PlacementPlus_ChangeOrientation";
        public const string CHANGE_TOOL_MODE = "PlacementPlus_ChangeToolMode";

        public const string INCREASE_SIZE = "PlacementPlus_IncreaseSize";
        public const string DECREASE_SIZE = "PlacementPlus_DecreaseSize";
        
        public const string REVERSE_DIRECTION = "PlacementPlus_ReverseDirection";
        public const string REPLACE_BUTTON = "PlacementPlus_ReplaceButton";

        public void EarlyInit()
        {
            Log.LogInfo($"Mod version: {VERSION}");
            modInfo = this.GetModInfo();
            if (modInfo == null)
            {
                Log.LogError($"Failed to load {MODNAME}: mod metadata not found!");
                return;
            }

            CoreLibMod.LoadSubmodule(typeof(ControlMappingModule));
            
            Config = new ConfigFile("PlacementPlus/PlacementPlus.cfg", true, modInfo);
            
            // Every entry is Client scope on purpose: GMCM's server sync assigns
            // ids to synced entries in a different order on the dedicated server
            // than on the client, so synced values land in the wrong entries
            // (a bool arriving in MaxBrushSize froze the whole menu with
            // FormatException spam, 2026-08-20). Server-side values are guarded
            // by our own RPCs instead; the server's cfg on disk is the knob.
            maxSize = Config.Bind("General", "MaxBrushSize", 9,
                new ConfigDescription("Max range the brush will have", new AcceptableValueRange<int>(3, 15)),
                new ConfigScope(ConfigAccessLevel.Client));

            allowWallVariants = Config.Bind("General", "AllowWallVariants", false,
                "Allow brush placement for objects that have wall-mounted variations (doors, torches, lamps). " +
                "The base mod blocks these because the variation is picked per tile — expect odd orientations.");

            ignoreBuiltinExclude = Config.Bind("General", "IgnoreBuiltinExclude", false,
                "Ignore the exclude list baked into the mod (workbenches, furnaces, doors, torch, minecart, boats). " +
                "Objects larger than 1x1 stay unplaceable regardless — that check happens earlier.");

            sledgePreviewEnabled = Config.Bind("General", "SledgehammerPreview", true,
                "Show a translucent square where the sledgehammer will hit tiles. Client-side only.");

            sledgePreviewOpacity = Config.Bind("General", "SledgehammerPreviewOpacity", 0.15f, new ConfigDescription(
                "Opacity of the sledgehammer preview.",
                new AcceptableValueRange<float>(0.02f, 1f)),
                new ConfigScope(ConfigAccessLevel.Client));
            
            shortMessages = Config.Bind("General", "ShortMessages", false,
                "When changing modes show short messages");

            //RegisterMenuNames();
            LoadSledgeState();

            int catID = ControlMappingModule.AddNewCategory("PlacementPlus");
            
            ControlMappingModule.AddKeyboardBind(CHANGE_ORIENTATION,  KeyboardKeyCode.C, categoryId: catID);
            ControlMappingModule.AddKeyboardBind(INCREASE_SIZE,  KeyboardKeyCode.KeypadPlus, categoryId: catID);
            ControlMappingModule.AddKeyboardBind(DECREASE_SIZE,  KeyboardKeyCode.KeypadMinus, categoryId: catID);
            ControlMappingModule.AddKeyboardBind(CHANGE_TOOL_MODE,  KeyboardKeyCode.V, categoryId: catID);
            ControlMappingModule.AddKeyboardBind(REPLACE_BUTTON, KeyboardKeyCode.LeftAlt, categoryId: catID);
            ControlMappingModule.AddKeyboardBind(REVERSE_DIRECTION, KeyboardKeyCode.CapsLock, categoryId: catID);
            
            //modInfo.TryLoadBurstAssembly();
            
            ControlMappingModule.rewiredStart += OnRewiredStart;
            API.Authoring.OnObjectTypeAdded += EditPlayer;
            
            Log.LogInfo("Placement Plus mod is loaded!");
        }

        private void EditPlayer(Entity entity, GameObject authoringdata, EntityManager entitymanager)
        {
            var objectId = authoringdata.GetEntityObjectID();
            if (objectId != ObjectID.Player) return;

            Log.LogInfo("Adding my components!");
            
            entitymanager.AddComponent<PlacementPlusState>(entity);
            entitymanager.AddBuffer<ShovelDigQueueBuffer>(entity);
        }

        private void OnRewiredStart()
        {
            rwPlayer = ReInput.players.GetPlayer(0);
        }
        
        public void Init()
        {
            API.Client.OnWorldCreated += ClientWorldInit;
            
            BurstDisabler.DisableBurstForSystemAndJobs<EquipmentUpdateSystem>();
            //BurstDisabler.DisableBurstForSystem<EquipmentLateUpdateSystem>();
        }

        private void ClientWorldInit()
        {
            var world = API.Client.World;
            commandSystem = world.GetOrCreateSystemManaged<ClientModCommandSystem>();
            Log.LogInfo($"Got the client system: {commandSystem}");
        }

        public void Shutdown() { }

        public void ModObjectLoaded(Object obj) { }


        private World GetWorld()
        {
            if (API.Client.World != null)
            {
                return API.Client.World;
            }

            return API.Server.World;
        }

        public void Update()
        {
            var manager = Manager.main;
            if (manager == null) return;
            var player = manager.player;
            if (player == null)
            {
                // Because the player does not exist on the main menu, execution never reaches 
                // the hiding logic in UpdateSledgePreview. However, the preview clone is attached 
                // to the camera anchor (VolatileRenderAnchor) and survives the scene transition. 
                // If the player leaves [the game] on the exact frame the preview turns on, 
                // the grid remains visible on the title screen (Reported on 2026-08-21).
                if (sledgePreviewIcon != null && sledgePreviewIcon.gameObject.activeSelf)
                    sledgePreviewIcon.gameObject.SetActive(false);
                return;
            }

            if (rwPlayer == null) return;

            // Before the UI early-returns: the icon hides itself in its own
            // LateUpdate while a menu is open, so a stale position is harmless.
            UpdateSledgePreview(player);

            if (Manager.ui.isAnyInventoryShowing) return;
            if (Manager.ui.instrumentUI.isShowing) return;
            if (Manager.menu.IsAnyMenuActive()) return;
            if (!Manager.input.singleplayerInputModule.InputEnabled) return;

            if (rwPlayer.GetButtonDown(CHANGE_ORIENTATION))
            {
                // Buckets, watering cans, the seeder and sledgehammers ignore
                // the brush shape (they act on the game's own area), so the
                // toggle would only produce a mode message for a mode that does
                // nothing there. Don't send it.
                var em = API.Client.World.EntityManager;
                var slotType = em.GetComponentData<EquipmentSlotCD>(player.entity).slotType;
                bool shapeless = slotType == EquipmentSlotType.BucketSlot ||
                                 slotType == EquipmentSlotType.WaterCanSlot ||
                                 slotType == EquipmentSlotType.SeederSlot;

                if (!shapeless)
                {
                    var heldData = em.GetComponentData<EquippedObjectCD>(player.entity)
                        .containedObject.objectData;
                    var heldInfo = PugDatabase.GetObjectInfo(heldData.objectID, heldData.variation);
                    shapeless = heldInfo != null && heldInfo.objectType == ObjectType.Sledge;
                }

                if (!shapeless)
                {
                    commandSystem.ChangeOrientation(player.entity);
                }
            }

            var backwards = rwPlayer.GetButton(REVERSE_DIRECTION);

            if (rwPlayer.GetButtonDown(CHANGE_TOOL_MODE))
            {
                commandSystem.ChangeToolMode(player.entity, backwards);
                return;
            }

            if (rwPlayer.GetButtonDown(INCREASE_SIZE))
            {
                commandSystem.ChangeSize(player.entity, 1);
                plusHoldTime = 0;
            }

            if (rwPlayer.GetButton(INCREASE_SIZE))
            {
                plusHoldTime += Time.deltaTime;
                if (plusHoldTime > MIN_HOLD_TIME)
                {
                    plusHoldTime = 0;
                    commandSystem.ChangeSize(player.entity, 1);
                }
            }

            if (rwPlayer.GetButtonDown(DECREASE_SIZE))
            {
                commandSystem.ChangeSize(player.entity, -1);
                minusHoldTime = 0;
            }

            if (rwPlayer.GetButton(DECREASE_SIZE))
            {
                minusHoldTime += Time.deltaTime;
                if (minusHoldTime > MIN_HOLD_TIME)
                {
                    minusHoldTime = 0;
                    commandSystem.ChangeSize(player.entity, -1);
                }
            }

            var replaceTiles = rwPlayer.GetButton(REPLACE_BUTTON);
            if (replaceTiles != lastReplaceState)
            {
                commandSystem.SetReplaceState(player.entity, replaceTiles);
                lastReplaceState = replaceTiles;
            }
        }

        private static PlacementIcon sledgePreviewIcon;
        private static readonly int TransparancyProperty = Shader.PropertyToID("_transparancy");

        // The sledgehammer has no placement preview of its own (it is a melee
        // weapon), so a private clone of the game's PlacementIcon is driven with
        // the same math the damage code uses (TryDealSledgeDamage), ±4 tile cap
        // included. Client-side only.
        private static void UpdateSledgePreview(PlayerController player)
        {
            bool show = false;
            var em = API.Client.World.EntityManager;

            if (sledgePreviewEnabled.Value &&
                !Manager.ui.isShowingMap &&
                !Manager.ui.isAnyInventoryShowing &&
                em.HasComponent<EquippedObjectCD>(player.entity) &&
                em.HasComponent<AnimationOrientationCD>(player.entity) &&
                em.HasComponent<LocalTransform>(player.entity))
            {
                var held = em.GetComponentData<EquippedObjectCD>(player.entity);
                var heldData = held.containedObject.objectData;
                ObjectInfo heldInfo = PugDatabase.GetObjectInfo(heldData.objectID, heldData.variation);

                if (heldInfo != null && heldInfo.objectType == ObjectType.Sledge &&
                    held.equipmentPrefab != Entity.Null &&
                    em.HasComponent<MeleeWeaponCD>(held.equipmentPrefab))
                {
                    var melee = em.GetComponentData<MeleeWeaponCD>(held.equipmentPrefab);
                    Vector3 facing = em.GetComponentData<AnimationOrientationCD>(player.entity)
                        .facingDirection.vec3;
                    float3 playerPos = em.GetComponentData<LocalTransform>(player.entity).Position;

                    // The live collider is a contested field — CoreEnhance
                    // rewrites it once a second even with its switch off, and
                    // the reconcile in ApplySledgeSize only wins back on the
                    // equipment tick, so reading it here blinked once a second.
                    // The preview shows the intended size instead; damage
                    // matches because the reconcile holds the collider there.
                    int step = sledgeSize.Value;
                    bool square = sledgeSquare?.Value ?? true;
                    if (sledgeSizeSynced >= 0) step = sledgeSizeSynced;
                    if (sledgeShapeSynced >= 0) square = sledgeShapeSynced == 1;

                    float arcFactor = step > 0 && square
                        ? 0f
                        : (melee.arcAngle == ArcAngle.arc180 ||
                           melee.arcAngle == ArcAngle.arc270 ? 1f : 0f);
                    float size = SledgeStepToBase(step, square);
                    float extra = melee.extraHitColliderReachSize;

                    float2 hitPos = new float2(facing.x, facing.z) * ((size + extra) / 2f);
                    float2 hitSize = new float2(
                        (size + math.abs(facing.x) * extra) * (1f + math.abs(facing.z) * arcFactor),
                        (size + math.abs(facing.z) * extra) * (1f + math.abs(facing.x) * arcFactor));

                    int2 half = math.min((int2)math.round(hitSize / 2f), new int2(4, 4));
                    int2 center = (int2)math.round(new float2(playerPos.x, playerPos.z) + hitPos);
                    int2 corner = center - half;

                    var icon = GetSledgePreviewIcon();
                    if (icon != null)
                    {
                        if (!icon.gameObject.activeSelf) icon.gameObject.SetActive(true);

                        // With the stock sprite shader the color alpha is enough;
                        // the float is kept for the original-shader fallback
                        // ("_transparancy", sic — 0 is opaque, larger fades out).
                        float alpha = math.clamp(sledgePreviewOpacity.Value, 0.02f, 1f);
                        icon.SR.color = new Color(1f, 1f, 1f, alpha);
                        if (icon.SR.material.HasProperty(TransparancyProperty))
                            icon.SR.material.SetFloat(TransparancyProperty, 1f - alpha);

                        icon.SetSize(half.x * 2 + 1, half.y * 2 + 1);
                        // Straight to transform: SetPosition would lerp through
                        // start/targetPosition transforms, and if those resolve
                        // outside the clone they belong to the real icon.
                        icon.transform.position = EntityMonoBehaviour.ToRenderFromWorld(
                            new Vector3Int(corner.x, (int)math.round(playerPos.y), corner.y));
                        show = true;
                    }
                }
            }

            if (!show && sledgePreviewIcon != null && sledgePreviewIcon.gameObject.activeSelf)
                sledgePreviewIcon.gameObject.SetActive(false);
        }

        private static PlacementIcon GetSledgePreviewIcon()
        {
            if (sledgePreviewIcon != null) return sledgePreviewIcon;

            // Any loaded PlacementIcon works as a template — the sprite, material
            // and 9-slice setup are what matter, the rest gets overridden.
            var templates = Resources.FindObjectsOfTypeAll<PlacementIcon>();
            if (templates.Length == 0) return null;

            sledgePreviewIcon = Object.Instantiate(templates[0]);
            sledgePreviewIcon.name = "PlacementPlus_SledgePreview";
            sledgePreviewIcon.transform.parent = Manager.camera.VolatileRenderAnchor;
            sledgePreviewIcon.transform.localScale = Vector3.one;
            sledgePreviewIcon.transform.rotation = Quaternion.identity;
            sledgePreviewIcon.SetState(true, 0, false, DisplayPlaceableType.Default);

            // Its LateUpdate would overwrite the transparency float every frame
            // (that is how the real icon fades while walking), so the component
            // is turned off and UpdateSledgePreview owns visibility instead.
            sledgePreviewIcon.enabled = false;
            sledgePreviewIcon.SR.enabled = true;
            sledgePreviewIcon.paddingSR.enabled = false;

            // The icon's own shader (Amplify/PlaceIcon) ignores both the sprite
            // color and, on this clone, the _transparancy float — so swap in the
            // stock sprite shader, where SR.color alpha behaves normally.
            Shader defaultSprites = Shader.Find("Sprites/Default");
            if (defaultSprites != null)
            {
                sledgePreviewIcon.SR.material = new Material(defaultSprites);
            }

            return sledgePreviewIcon;
        }
    }
}