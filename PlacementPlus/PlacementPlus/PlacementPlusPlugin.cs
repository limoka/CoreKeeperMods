using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.IL2CPP;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using CoreLib;
using CoreLib.Submodules.Audio;
using CoreLib.Submodules.ModResources;
using CoreLib.Submodules.RewiredExtension;
using HarmonyLib;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Rewired;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PlacementPlus
{
    [BepInPlugin(MODGUID, MODNAME, VERSION)]
    [BepInDependency(CoreLibPlugin.GUID)]
    [CoreLibSubmoduleDependency(nameof(RewiredExtensionModule))]
    public class PlacementPlusPlugin : BasePlugin
    {
        public const string MODNAME = "Placement Plus";

        public const string MODGUID = "org.kremnev8.plugin.PlacementPlus";

        public const string VERSION = "1.4.0";

        public const string CHANGE_ORIENTATION = "PlacementPlus_ChangeOrientation";
        public const string ROTATE = "PlacementPlus_Rotate";

        public const string INCREASE_SIZE = "PlacementPlus_IncreaseSize";
        public const string DECREASE_SIZE = "PlacementPlus_DecreaseSize";
        
        public const string FORCE_ADJACENT = "PlacementPlus_ForceAdjacent";
        public const string REPLACE_BUTTON = "PlacementPlus_ReplaceButton";

        public static ManualLogSource logger;
        public new static ConfigFile Config;

        public static ResourceData resource;

        private static Il2CppReferenceArray<Object> m_iconSprites;
        private static GCHandle m_spritesHandle;

        #region Excludes

        public static HashSet<ObjectID> defaultExclude = new HashSet<ObjectID>()
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

        public static HashSet<ObjectID> userExclude = new HashSet<ObjectID>()
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
        public static ConfigEntry<KeyMode> forceKeyMode;
        public static ConfigEntry<float> minHoldTime;

        public static MusicManager.MusicRosterType customRoster;
        public static SfxID customEffect;

        public override void Load()
        {
            logger = Log;
            BepInPlugin metadata = MetadataHelper.GetMetadata(this);
            Config = new ConfigFile(Path.Combine(Paths.ConfigPath, "PlacementPlus", "PlacementPlus.cfg"), true, metadata);

            maxSize = Config.Bind("General", "MaxBrushSize", 7, new ConfigDescription("Max range the brush will have", new AcceptableValueRange<int>(3, 9)));

            forceKeyMode = Config.Bind("General", "ForceRotationKeyMode", KeyMode.HOLD, "Which mode should 'Force adjacent belt rotation' keybind use");
            
            excludeString = Config.Bind("General", "ExcludeItems", userExclude.Join(),
                "List of comma delimited items to automatically disable the area placement feature. You can reference 'ItemIDs.txt' file for all existing item ID's");

            minHoldTime = Config.Bind("General", "MinHoldTime", 0.15f,
                "Minimal hold time before your plus or minus presses are incremented automatically");

            
            ParseConfigString();
            WriteReferenceFile();

            RewiredExtensionModule.AddKeybind(CHANGE_ORIENTATION, "Change Orientation", KeyboardKeyCode.C);
            RewiredExtensionModule.AddKeybind(INCREASE_SIZE, "Increase Size", KeyboardKeyCode.KeypadPlus);
            RewiredExtensionModule.AddKeybind(DECREASE_SIZE, "Decrease Size", KeyboardKeyCode.KeypadMinus);
            RewiredExtensionModule.AddKeybind(ROTATE, "Rotate/Change Brush Color", KeyboardKeyCode.V);
            RewiredExtensionModule.AddKeybind(FORCE_ADJACENT, "Force adjacent belt rotation", KeyboardKeyCode.LeftControl);
            RewiredExtensionModule.AddKeybind(REPLACE_BUTTON, "Hold to replace tiles", KeyboardKeyCode.LeftShift);

            string pluginfolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            resource = new ResourceData(MODNAME, "PlacementPlus", pluginfolder);
            resource.LoadAssetBundle("placementplusbundle");
            ResourcesModule.AddResource(resource);

            m_iconSprites = resource.bundle.LoadAssetWithSubAssets("Assets/PlacementPlus/Textures/arrow_cursor.png", Il2CppType.Of<Sprite>());
            m_spritesHandle = GCHandle.Alloc(m_iconSprites);
            
            AddComponent<UpdateMono>();

            Harmony harmony = new Harmony(MODGUID);
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            logger.LogInfo("Placement Plus mod is loaded!");
        }

        public static Sprite GetSprite(int index)
        {
            if (m_iconSprites == null || m_iconSprites[index] == null)
            {
                logger.LogDebug("Had to reallocate the array!");
                m_iconSprites = resource.bundle.LoadAssetWithSubAssets("Assets/PlacementPlus/Textures/arrow_cursor.png", Il2CppType.Of<Sprite>());
            }

            return m_iconSprites[index].Cast<Sprite>();
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
                    logger.LogWarning($"Error parsing item name! Item '{item}' is not a valid item name!");
                }
            }
        }

        private static void WriteReferenceFile()
        {
            using FileStream stream = File.OpenWrite(Path.Combine(Paths.ConfigPath, "PlacementPlus", "ItemIDs.txt"));

            byte[] text = Encoding.UTF8.GetBytes(
                "#This file contains all known item ID's.\n#You can use this file as a reference while configuring 'AreaPlacement.cfg'\n");
            stream.Write(text, 0, text.Length);

            string[] allItems = Enum.GetNames(typeof(ObjectID));
            foreach (string item in allItems)
            {
                if (item.Equals("None") || item.Equals("LARGEST_ID")) continue;

                byte[] info = Encoding.UTF8.GetBytes("\n" + item);
                stream.Write(info, 0, info.Length);
            }
        }
    }
}