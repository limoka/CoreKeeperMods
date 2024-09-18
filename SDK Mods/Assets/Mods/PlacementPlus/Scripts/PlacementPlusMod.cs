using System;
using System.Collections.Generic;
using CoreLib;
using CoreLib.Data.Configuration;
using CoreLib.RewiredExtension;
using CoreLib.Util.Extensions;
using HarmonyLib;
using Inventory;
using PlacementPlus.Components;
using PlacementPlus.Systems.Network;
using PugMod;
using PugTilemap;
using Rewired;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;
using Action = CoreKeeperInput.Action;
using Object = UnityEngine.Object;
using Logger = CoreLib.Util.Logger;

namespace PlacementPlus
{
    public class PlacementPlusMod : IMod
    {
        public const string MODNAME = "Placement Plus";
        public const string VERSION = "2.0.2";

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
            
            modInfo.TryLoadBurstAssembly();
            
            RewiredExtensionModule.rewiredStart += OnRewiredStart;
            API.Authoring.OnObjectTypeAdded += EditPlayer;
            
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
            if (player == null) return;
            
            if (rwPlayer == null) return;
            
            if (Manager.ui.isAnyInventoryShowing) return;
            if (Manager.ui.instrumentUI.isShowing) return;
            if (Manager.menu.IsAnyMenuActive()) return;
            if (!Manager.input.singleplayerInputModule.InputEnabled) return;

            if (rwPlayer.GetButtonDown(CHANGE_ORIENTATION))
            {
                commandSystem.ChangeOrientation(player.entity);
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
                if (plusHoldTime > minHoldTime.Value)
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
                if (minusHoldTime > minHoldTime.Value)
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
    }
}