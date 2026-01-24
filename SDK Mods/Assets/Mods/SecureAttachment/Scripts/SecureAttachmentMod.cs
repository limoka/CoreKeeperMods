using System;
using System.Collections.Generic;
using System.Linq;
using CoreLib;
using CoreLib.Data.Configuration;
using CoreLib.Submodule.Audio;
using CoreLib.Submodule.Entity;
using CoreLib.Submodule.EquipmentSlot;
using CoreLib.Util.Extension;
using PugMod;
using Unity.Entities;
using UnityEngine;
using Logger = CoreLib.Util.Logger;
using Object = UnityEngine.Object;

namespace SecureAttachment
{
    public class SecureAttachmentMod : IMod
    {
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

        internal static Logger Log = new Logger("Secure Attachment");
        public static ConfigFile Config;

        public const string MOD_ID = "SecureAttachment";
        public const string VERSION = "2.0.3";
        public const string MOD_NAME = "Secure Attachment";

        internal static LoadedMod modInfo;

        internal static ConfigEntry<string> userMountedListString;
        internal static HashSet<ObjectID> userMountedList = new HashSet<ObjectID>();

        internal static ConfigEntry<bool> attachChests;

        public static SfxID wrenchSfx;
        public static EffectID wrenchEffect;

        public void EarlyInit()
        {
            Log.LogInfo($"Loading {MOD_NAME}, version: {VERSION}");

            CoreLibMod.LoadSubmodule(
                typeof(EntityModule),
                typeof(EquipmentSlotModule),
                typeof(AudioModule));

            modInfo = this.GetModInfo();
            if (modInfo == null)
            {
                Log.LogError("Failed to load Rail Logistics: mod metadata not found!");
                return;
            }

            Config = new ConfigFile("SecureAttachment/SecureAttachment.cfg", true, modInfo);


            attachChests = Config.Bind("General", "attachChests", true, "Make all chests indestructible and removable only with the wrench?");

            userMountedListString = Config.Bind("General", "additionalItems", "",
                "List of comma delimited additional items for which to enable secure attachment feature.");

            ParseConfigString();

            mountedObjects.UnionWith(userMountedList);
            
            API.Authoring.OnObjectTypeAdded += ModifyPlaceables;

            EquipmentSlotModule.RegisterEquipmentSlot<WrenchEquipmentSlot>(
                WrenchEquipmentSlot.WrenchObjectType,
                EquipmentSlotModule.PLACEMENT_PREFAB,
                new WrenchSlotLogic()
            );

            wrenchEffect = AudioModule.AddEffect(new UnmountEffect());
            
            Log.LogInfo($"{MOD_NAME} mod is loaded!");
        }

        public void Init()
        {
        }

        public void Shutdown()
        {
        }

        public void ModObjectLoaded(Object obj)
        {
            if (obj == null) return;

            if (obj is AudioClip clip && obj.name.Contains("wrench"))
            {
                wrenchSfx = AudioModule.AddSoundEffect(clip);
            }
        }

        public void Update()
        {
        }

        public static void MakeMounted(ObjectID objectID)
        {
            mountedObjects.Add(objectID);
        }


        private void ModifyPlaceables(
            Entity entity,
            GameObject authoringdata,
            EntityManager entitymanager
        )
        {
            var objectId = authoringdata.GetEntityObjectID();
            if (mountedObjects.Contains(objectId))
            {
                MakeMounted(entity, entitymanager, 1);
            }
            else if (attachChests.Value &&
                     chestIds.Contains(objectId))
            {
                MakeMounted(entity, entitymanager, 0);
            }
        }

        private static void MakeMounted(Entity entity, EntityManager entitymanager, int tier)
        {
            entitymanager.AddComponentData(entity, new MountedCD()
            {
                wrenchTier = tier
            });

            if (!entitymanager.HasComponent<IndestructibleCD>(entity))
                entitymanager.AddComponent<IndestructibleCD>(entity);
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
                    Log.LogWarning($"Error parsing item name! Item '{item}' is not a valid item name!");
                }
            }
        }
    }
}