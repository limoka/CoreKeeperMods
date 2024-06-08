using System;
using System.Collections.Generic;
using CoreLib;
using CoreLib.Data.Configuration;
using CoreLib.RewiredExtension;
using CoreLib.Util.Extensions;
using HarmonyLib;
using PugMod;
using PugTilemap;
using Rewired;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using Action = CoreKeeperInput.Action;
using Object = UnityEngine.Object;
using Logger = CoreLib.Util.Logger;

namespace PlacementPlus
{
    public class PlacementPlusMod : IMod
    {
        public const string MODNAME = "Placement Plus";
        public const string VERSION = "1.7.8";

        public static Logger Log = new Logger(MODNAME);
        public static ConfigFile Config;
        private LoadedMod modInfo;

        #region Excludes

        public static HashSet<ObjectID> defaultExclude = new HashSet<ObjectID>
        {
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
        };

        public static HashSet<ObjectID> userExclude = new HashSet<ObjectID>
        {
            ObjectID.InventoryChest,
            ObjectID.InventoryLarvaHiveChest,
            ObjectID.InventoryMoldDungeonChest,
            ObjectID.InventoryAncientChest,
            ObjectID.InventorySeaBiomeChest,
            ObjectID.Torch,
            ObjectID.Campfire,
            ObjectID.DecorativeTorch1,
            ObjectID.DecorativePot,
            ObjectID.PlanterBox,
            ObjectID.Pedestal,
            ObjectID.StonePedestal,
            ObjectID.RuinsPedestal,
            ObjectID.Lamp,
            ObjectID.Sprinkler,
        };

        #endregion


        public static ConfigEntry<string> excludeString;
        public static ConfigEntry<int> maxSize;
        public static ConfigEntry<float> minHoldTime;


        private static float plusHoldTime;
        private static float minusHoldTime;

        private static readonly Dictionary<int, ObjectID> colorIndexLookup = new Dictionary<int, ObjectID>();

        private static int lastColorIndex = -1;
        private static int maxPaintIndex = -1;
        private static Player rwPlayer;

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

            CoreLibMod.LoadModule(typeof(RewiredExtensionModule));
            
            Config = new ConfigFile("PlacementPlus/PlacementPlus.cfg", true, modInfo);
            
            maxSize = Config.Bind("General", "MaxBrushSize", 7, new ConfigDescription("Max range the brush will have", new AcceptableValueRange<int>(3, 9)));

            excludeString = Config.Bind("General", "ExcludeItems", userExclude.Join(),
                "List of comma delimited items to automatically disable the area placement feature. You can reference 'ItemIDs.txt' file for all existing item ID's");

            minHoldTime = Config.Bind("General", "MinHoldTime", 0.15f,
                "Minimal hold time before your plus or minus presses are incremented automatically");

            ParseConfigString();
            
            RewiredExtensionModule.AddKeybind(CHANGE_ORIENTATION, "Change Orientation", KeyboardKeyCode.C);
            RewiredExtensionModule.AddKeybind(INCREASE_SIZE, "Increase Size", KeyboardKeyCode.KeypadPlus);
            RewiredExtensionModule.AddKeybind(DECREASE_SIZE, "Decrease Size", KeyboardKeyCode.KeypadMinus);
            RewiredExtensionModule.AddKeybind(CHANGE_TOOL_MODE, "Change Tool Mode", KeyboardKeyCode.V);
            RewiredExtensionModule.AddKeybind(REPLACE_BUTTON, "Hold to replace tiles", KeyboardKeyCode.LeftAlt);
            RewiredExtensionModule.AddKeybind(REVERSE_DIRECTION, "Hold to reverse direction", KeyboardKeyCode.CapsLock);
            
            RewiredExtensionModule.rewiredStart += OnRewiredStart;
            
            Log.LogInfo("Placement Plus mod is loaded!");
        }

        private static void ParseConfigString()
        {
            string itemsNoSpaces = excludeString.Value.Replace(" ", "");
            if (string.IsNullOrEmpty(itemsNoSpaces)) return;

            string[] split = itemsNoSpaces.Split(',');
            userExclude.Clear();
            foreach (string item in split)
            {
                try
                {
                    ObjectID itemEnum = (ObjectID)Enum.Parse(typeof(ObjectID), item);
                    if (itemEnum is ObjectID.Drill or ObjectID.MechanicalArm or ObjectID.ConveyorBelt) continue;

                    userExclude.Add(itemEnum);
                }
                catch (ArgumentException)
                {
                    Log.LogWarning($"Error parsing item name! Item '{item}' is not a valid item name!");
                }
            }
        }
        
        private void OnRewiredStart()
        {
            rwPlayer = ReInput.players.GetPlayer(0);
        }
        
        public void Init() { }

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

        private void InitColorIndexLookup()
        {
            if (colorIndexLookup.Count > 0) return;

            World world = GetWorld();

            EntityQuery brushQuery = world.EntityManager.CreateEntityQuery(typeof(ObjectDataCD),
                typeof(Prefab),
                typeof(PaintToolCD));

            NativeArray<Entity> brushEntities = brushQuery.ToEntityArray(Allocator.Temp);
            foreach (Entity brushEntity in brushEntities)
            {
                ObjectDataCD objectDataCd = world.EntityManager.GetComponentData<ObjectDataCD>(brushEntity);
                PaintToolCD paintToolCd = world.EntityManager.GetComponentData<PaintToolCD>(brushEntity);
                if (paintToolCd.paintIndex != 0)
                {
                    colorIndexLookup.Add(paintToolCd.paintIndex, objectDataCd.objectID);
                    maxPaintIndex = Math.Max(maxPaintIndex, paintToolCd.paintIndex);
                }
            }

            brushEntities.Dispose();
            brushQuery.Dispose();
        }

        public void Update()
        {
            var manager = Manager.main;
            if (manager == null) return;
            var player = manager.player;
            if (player == null) return;
            
            if (rwPlayer == null) return;
            
            if (Manager.ui.isAnyInventoryShowing) return;
            if (Manager.ui.instrumentUI.isShowing) return;
            if (Manager.menu.IsAnyMenuActive()) return;
            if (!Manager.input.singleplayerInputModule.InputEnabled) return;

            if (rwPlayer.GetButtonDown(CHANGE_ORIENTATION))
            {
                BrushExtension.ToggleMode();
            }

            var backwards = rwPlayer.GetButton(REVERSE_DIRECTION);

            if (rwPlayer.GetButtonDown(CHANGE_TOOL_MODE))
            {
                ToggleToolMode(player, backwards);
                return;
            }

            if (rwPlayer.GetButtonDown(INCREASE_SIZE))
            {
                BrushExtension.ChangeSize(1);
                plusHoldTime = 0;
            }

            if (rwPlayer.GetButton(INCREASE_SIZE))
            {
                plusHoldTime += Time.deltaTime;
                if (plusHoldTime > minHoldTime.Value)
                {
                    plusHoldTime = 0;
                    BrushExtension.ChangeSize(1);
                }
            }

            if (rwPlayer.GetButtonDown(DECREASE_SIZE))
            {
                BrushExtension.ChangeSize(-1);
                minusHoldTime = 0;
            }

            if (rwPlayer.GetButton(DECREASE_SIZE))
            {
                minusHoldTime += Time.deltaTime;
                if (minusHoldTime > minHoldTime.Value)
                {
                    minusHoldTime = 0;
                    BrushExtension.ChangeSize(-1);
                }
            }

            BrushExtension.replaceTiles = rwPlayer.GetButton(REPLACE_BUTTON);
        }

        private void ToggleToolMode(PlayerController player, bool backwards)
        {
            InventoryHandler inventory = player.playerInventoryHandler;
            if (inventory == null) return;

            ObjectDataCD item = inventory.GetObjectData(player.equippedSlotIndex);
            var objectInfo = PugDatabase.GetObjectInfo(item.objectID, item.variation);

            if (PugDatabase.HasComponent<PaintToolCD>(item))
            {
                CyclePaintBrush(item, backwards, inventory, player);
            }
            else if (objectInfo != null &&
                     objectInfo.objectType == ObjectType.RoofingTool)
            {
                BrushExtension.ToggleRoofingMode(backwards);
            }else if (objectInfo.tileType == TileType.wall)
            {
                BrushExtension.ToggleBlockMode(backwards);
            }


        }

        private void CyclePaintBrush(ObjectDataCD item, bool shift, InventoryHandler inventory, PlayerController player)
        {
            InitColorIndexLookup();
            if (lastColorIndex == -1)
            {
                lastColorIndex = PugDatabase.GetComponent<PaintToolCD>(item).paintIndex;
            }

            lastColorIndex += shift ? -1 : 1;
            if (lastColorIndex < 0)
                lastColorIndex = maxPaintIndex - 1;
            if (lastColorIndex > maxPaintIndex)
                lastColorIndex = 1;

            ObjectID newObjectId = colorIndexLookup[lastColorIndex];

            inventory.DestroyObject(player.equippedSlotIndex, item.objectID);
            inventory.CreateItem(player.equippedSlotIndex, newObjectId, 1, player.WorldPosition);
        }
    }
}