using System;
using System.Collections.Generic;
using CoreLib;
using CoreLib.Data.Configuration;
using CoreLib.Submodule.Audio;
using CoreLib.Submodule.ControlMapping;
using CoreLib.Submodule.Entity;
using CoreLib.Submodule.EquipmentSlot;
using CoreLib.Util.Extension;
using PugMod;
using Rewired;
using Unity.Entities;
using UnityEngine;
using Logger = CoreLib.Util.Logger;
using Object = UnityEngine.Object;

namespace SecureAttachment
{
    public class SecureAttachmentMod : IMod
    {
        public static HashSet<ObjectID> mountedObjects = new()
        {
            ObjectID.RobotArm,
            ObjectID.Drill,
            ObjectID.ConveyorBelt,
            ObjectID.ConveyorBeltSplitter,
            ObjectID.ElectricityGenerator,
            ObjectID.UndergroundElectricityGenerator,
            ObjectID.Lever,
            ObjectID.CrossCircuit,
            ObjectID.DelayCircuit,
            ObjectID.PulseCircuit,
            ObjectID.LogicCircuit,
            ObjectID.TCircuit,
            ObjectID.ICircuit,
            ObjectID.LCircuit,
            ObjectID.ElectricityStick,
            ObjectID.GalaxiteTurret,
            ObjectID.GalaxiteTrap,
            ObjectID.TempleTurret,
            ObjectID.Furnace,
            ObjectID.SmelterKiln,
            ObjectID.GlassSmelter,
            ObjectID.FuryForge,
            ObjectID.TableSaw,
            ObjectID.SpikeTrap,
            ObjectID.EggIncubator,
            ObjectID.SeedExtractor,
            ObjectID.RobotFarmArm,
            ObjectID.Sprinkler,
            ObjectID.ItemCollector,
            ObjectID.Shredder,
            ObjectID.Incinerator,
            ObjectID.SalvageAndRepairStation
        };

        public static HashSet<ObjectID> chestIds = new()
        {
            ObjectID.InventoryChest,
            ObjectID.InventoryLarvaHiveChest,
            ObjectID.InventoryMoldDungeonChest,
            ObjectID.InventoryAncientChest,
            ObjectID.InventorySeaBiomeChest,
            ObjectID.InventoryDesertBiomeChest,
            ObjectID.InventoryLavaChest,
            ObjectID.GingerbreadChest,
            ObjectID.ValentineChest,
            ObjectID.AlienChest,
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
            ObjectID.AtlantianWormChest,
            ObjectID.HydraBossNatureChest,
            ObjectID.HydraBossSeaChest,
            ObjectID.HydraBossDesertChest,
            ObjectID.CoreCommanderChest,
            ObjectID.WallBossChest,
            ObjectID.MalugazChest,
            ObjectID.GiantCicadaChest,
            ObjectID.CopperChest,
            ObjectID.IronChest,
            ObjectID.ScarletChest,
            ObjectID.OctarineChest,
            ObjectID.GalaxiteChest,
            ObjectID.SolariteChest,
            ObjectID.PassageChest,
            ObjectID.RobotBossChest,
            ObjectID.ReluciteChest,
            ObjectID.InventoryExcavationBiomeChest,
            ObjectID.HydraBossVoidChest,
            ObjectID.EerieChest
        };

        internal static Logger Log = new Logger("Secure Attachment");
        public static ConfigFile Config;

        public const string MOD_ID = "SecureAttachment";
        public const string VERSION = "3.0.0";
        public const string MOD_NAME = "Secure Attachment";

        internal static LoadedMod modInfo;

        internal static ConfigEntry<string> userMountedListString;
        internal static HashSet<ObjectID> userMountedList = new();

        internal static ConfigEntry<bool> attachChests;

        internal static SfxID wrenchSfx;
        internal static EffectID wrenchEffect;
        internal static EffectID wrenchFailedEffect;
        
        internal static Emote.EmoteType emoteWrenchAuto;
        internal static Emote.EmoteType emoteWrenchMount;
        internal static Emote.EmoteType emoteWrenchUnmount;
        internal static Emote.EmoteType emoteWrenchNotUsable;
        
        internal const string TOGGLE_WRENCH_MODE_KEY = MOD_ID + ":ToggleWrenchMode";

        internal static Player rwPlayer;
        
        public void EarlyInit()
        {
            Log.LogInfo($"Loading {MOD_NAME}, version: {VERSION}");

            CoreLibMod.LoadSubmodule(
                typeof(EntityModule),
                typeof(EquipmentSlotModule),
                typeof(AudioModule),
                typeof(ControlMappingModule)
            );

            modInfo = this.GetModInfo();
            if (modInfo == null)
            {
                Log.LogError($"Failed to load {MOD_NAME}: mod metadata not found!");
                return;
            }

            Config = new ConfigFile($"{MOD_ID}/{MOD_ID}.cfg", true, modInfo);

            emoteWrenchAuto = EquipmentSlotModule.RegisterTextEmote($"{MOD_ID}:WrenchAuto");
            emoteWrenchMount = EquipmentSlotModule.RegisterTextEmote($"{MOD_ID}:WrenchMount");
            emoteWrenchUnmount = EquipmentSlotModule.RegisterTextEmote($"{MOD_ID}:WrenchUnmount");
            emoteWrenchNotUsable = EquipmentSlotModule.RegisterTextEmote($"{MOD_ID}:WrenchNotUsable");
            
            int catID = ControlMappingModule.AddNewCategory(MOD_ID);
            
            ControlMappingModule.AddKeyboardBind(TOGGLE_WRENCH_MODE_KEY, KeyboardKeyCode.G, categoryId: catID);
            ControlMappingModule.AddControllerBind(TOGGLE_WRENCH_MODE_KEY, GamepadTemplate.elementId_dPadLeft, categoryId: catID);
            
            ControlMappingModule.rewiredStart += OnRewiredStart;

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
            wrenchFailedEffect = AudioModule.AddEffect(new UnmountFailedEffect());
            
            Log.LogInfo($"{MOD_NAME} mod is loaded!");
        }

        public void Init()
        {
        }

        public void Shutdown()
        {
        }
        
        private void OnRewiredStart()
        {
            rwPlayer = ReInput.players.GetPlayer(0);
        }

        public void Update()
        {
            if (rwPlayer == null) return;

            if (rwPlayer.GetButtonDown(TOGGLE_WRENCH_MODE_KEY))
            {
                WrenchEquipmentSlot.ToggleMode();
            }
        }

        public void ModObjectLoaded(Object obj)
        {
            if (obj == null) return;

            if (obj is AudioClip clip && obj.name.Contains("wrench"))
            {
                wrenchSfx = AudioModule.AddSoundEffect(clip);
            }
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

            if (objectId == ObjectID.Player)
            {
                entitymanager.AddComponentData(entity, new WrenchStateCD());
            }
            
            if (mountedObjects.Contains(objectId))
            {
                MakeMounted(entity, entitymanager, objectId);
            }
            else if (attachChests.Value &&
                     chestIds.Contains(objectId))
            {
                MakeMounted(entity, entitymanager, objectId);
            }
        }

        private static void MakeMounted(Entity entity, EntityManager entitymanager, ObjectID objectID)
        {
            var hasMealsEaten = entitymanager.HasComponent<MealsEatenCD>(entity);
            if (hasMealsEaten)
            {
                Log.LogWarning($"Can't make {objectID} mounted! It seems to already have MealsEatenCD");
            }
            
            entitymanager.AddComponentData(entity, new MountedCD()
            {
                mountType = MountType.None,
                lastMountType = MountType.Unknown,
            });
            
            entitymanager.AddComponent<MealsEatenCD>(entity);

            if (!entitymanager.HasComponent<IndestructibleCD>(entity))
                entitymanager.AddComponent<IndestructibleCD>(entity);
            
            entitymanager.SetComponentEnabled<IndestructibleCD>(entity, false);
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