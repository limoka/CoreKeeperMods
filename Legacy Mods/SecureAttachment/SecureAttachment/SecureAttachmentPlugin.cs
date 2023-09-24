using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using CoreLib;
using CoreLib.Submodules.Audio;
using CoreLib.Submodules.Equipment;
using CoreLib.Submodules.JsonLoader;
using CoreLib.Submodules.ModEntity;
using CoreLib.Submodules.ModEntity.Atributes;
using BepInEx.Unity.IL2CPP;
using CoreLib.Submodules.ModComponent;
using CoreLib.Submodules.ModResources;

namespace SecureAttachment
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    [BepInDependency(CoreLibPlugin.GUID)]
    [CoreLibSubmoduleDependency(nameof(JsonLoaderModule), nameof(EquipmentSlotModule), nameof(AudioModule), nameof(ComponentModule))]
    [EntityModification]
    public class SecureAttachmentPlugin : BasePlugin
    {
        public static ManualLogSource logger;


        public static HashSet<ObjectID> mountedObjects = new HashSet<ObjectID>
        {
            ObjectID.RobotArm,
            ObjectID.Drill,
            ObjectID.ConveyorBelt,
            ObjectID.ElectricityGenerator,
            ObjectID.Lever,
            ObjectID.DelayCircuit,
            ObjectID.PulseCircuit,
            ObjectID.LogicCircuit,
            ObjectID.ElectricityStick,
            ObjectID.GalaxiteTurret,
            ObjectID.TempleTurret,
            ObjectID.Kiln1,
            ObjectID.Kiln2,
            ObjectID.SmelterKiln
        };

        public static HashSet<ObjectID> chestIds = new HashSet<ObjectID>()
        {
            ObjectID.InventoryChest,
            ObjectID.InventoryLarvaHiveChest,
            ObjectID.InventoryMoldDungeonChest,
            ObjectID.InventoryAncientChest,
            ObjectID.InventorySeaBiomeChest,
            ObjectID.InventoryDesertBiomeChest,
            ObjectID.InventoryLavaChest,
            ObjectID.GingerbreadChest,
            ObjectID.BossChest,
            ObjectID.GlurchChest,
            ObjectID.GhormChest,
            ObjectID.HivemotherChest,
            ObjectID.IvyChest,
            ObjectID.EasterChest,
            ObjectID.MorphaChest,
            ObjectID.OctopusBossChest,
            ObjectID.KingSlimeChest,
            ObjectID.LavaSlimeBossChest,
            ObjectID.HivemotherHalloweenChest,
            ObjectID.UnlockedPrinceChest,
            ObjectID.UnlockedQueenChest,
            ObjectID.UnlockedKingChest,
            ObjectID.CopperChest,
            ObjectID.IronChest,
            ObjectID.ScarletChest,
            ObjectID.OctarineChest,
            ObjectID.GalaxiteChest
        };

        internal static ConfigEntry<string> userMountedListString;
        internal static HashSet<ObjectID> userMountedList = new HashSet<ObjectID>();

        internal static ConfigEntry<bool> attachChests;

        public static SfxID wrenchSfx;
        private static ResourceData resources;

        public override void Load()
        {
            logger = Log;

            attachChests = Config.Bind("General", "attachChests", true, "Make all chests indestructible and removable only with the wrench?");

            userMountedListString = Config.Bind("General", "additionalItems", "",
                "List of comma delimited additional items for which to enable secure attachment feature.");

            ComponentModule.RegisterECSComponent<MountedCD>();
            ComponentModule.RegisterECSComponent<MountedCDAuthoring>();
            
            ComponentModule.RegisterECSComponent<WrenchCD>();
            ComponentModule.RegisterECSComponent<WrenchCDAuthoring>();

            ParseConfigString();

            mountedObjects.UnionWith(userMountedList);

            string pluginfolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            JsonLoaderModule.LoadFolder(PluginInfo.PLUGIN_GUID, pluginfolder);
            EquipmentSlotModule.RegisterEquipmentSlot<WrenchEquipmentSlot>(EquipmentSlotModule.PLACEMENT_PREFAB);
            EntityModule.RegisterEntityModifications(Assembly.GetExecutingAssembly());

            resources = new ResourceData(CoreLibPlugin.GUID, "SecureAttachment", pluginfolder);
            resources.LoadAssetBundle("secureattachment");
            ResourcesModule.AddResource(resources);
            
            wrenchSfx = AudioModule.AddSoundEffect("Assets/SecureAttachment/wrench-sound");

            logger.LogInfo($"{PluginInfo.PLUGIN_NAME} mod is loaded!");
        }

        public static void MakeMounted(ObjectID objectID)
        {
            mountedObjects.Add(objectID);
        }
        

        [EntityModification]
        public static void ModifyPlaceables(EntityMonoBehaviourData entityData)
        {
            if (mountedObjects.Contains(entityData.objectInfo.objectID))
            {
                MountedCDAuthoring mountedCdAuthoring = entityData.gameObject.AddComponent<MountedCDAuthoring>();
                mountedCdAuthoring.wrenchTier.Value = 1;
            }else if (attachChests.Value &&
                      chestIds.Contains(entityData.objectInfo.objectID))
            {
                MountedCDAuthoring mountedCdAuthoring = entityData.gameObject.AddComponent<MountedCDAuthoring>();
                mountedCdAuthoring.wrenchTier.Value = 0;
            }
        }

        private static void ParseConfigString()
        {
            string itemsNoSpaces = userMountedListString.Value.Replace(" ", "");
            if (string.IsNullOrEmpty(itemsNoSpaces)) return;

            string[] split = itemsNoSpaces.Split(',');
            userMountedList.Clear();
            foreach (string item in split)
            {
                try
                {
                    ObjectID itemEnum = (ObjectID)Enum.Parse(typeof(ObjectID), item);
                    userMountedList.Add(itemEnum);
                }
                catch (ArgumentException)
                {
                    logger.LogWarning($"Error parsing item name! Item '{item}' is not a valid item name!");
                }
            }
        }
    }
}