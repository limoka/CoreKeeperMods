using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using PugMod;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using Logger = PlacementPlus.Util.Logger;
using Object = UnityEngine.Object;

namespace PlacementPlus
{
    public class PlacementPlusMod : IMod
    {
        public const string MODNAME = "Placement Plus";
        public const string VERSION = "1.7.1";

        public static Logger Log = new Logger(MODNAME);
        private LoadedMod modInfo;

        private static KeyCode[] lessKeys =
        {
            KeyCode.KeypadPlus,
            KeyCode.Plus,
            KeyCode.Equals,
            KeyCode.Greater,
            KeyCode.RightBracket
        };
        
        private static KeyCode[] moreKeys =
        {
            KeyCode.KeypadMinus,
            KeyCode.Minus,
            KeyCode.Less,
            KeyCode.LeftBracket
        };
        
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


        public static string excludeString = userExclude.Join();
        public static int maxSize = 7;
        public static float minHoldTime = 0.15f;


        private static float plusHoldTime;
        private static float minusHoldTime;

        private static readonly Dictionary<int, ObjectID> colorIndexLookup = new Dictionary<int, ObjectID>();

        private static int lastColorIndex = -1;
        private static int maxPaintIndex = -1;

        public void EarlyInit()
        {
            Log.LogInfo($"Mod version: {VERSION}");
            modInfo = GetModInfo(this);
            if (modInfo == null)
            {
                Log.LogError($"Failed to load {MODNAME}: mod metadata not found!");
                return;
            }

            Log.LogInfo("Placement Plus mod is loaded!");
        }

        public static LoadedMod GetModInfo(IMod mod)
        {
            return API.ModLoader.LoadedMods.FirstOrDefault(modInfo => modInfo.Handlers.Contains(mod));
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

        private bool GetAnyKeyDown(KeyCode[] keys)
        {
            return keys.Any(Input.GetKeyDown);
        }
        
        private bool GetAnyKey(KeyCode[] keys)
        {
            return keys.Any(Input.GetKey);
        }

        public void Update()
        {
            var manager = Manager.main;
            if (manager == null) return;
            var player = manager.player;
            if (player == null) return;

            if (Input.GetKeyDown(KeyCode.C))
            {
                BrushExtension.ToggleMode();
            }
            
            var shift = Input.GetKey(KeyCode.LeftShift);

            if (Input.GetKeyDown(KeyCode.V))
            {
                InventoryHandler inventory = player.playerInventoryHandler;
                if (inventory == null) return;

                ObjectDataCD item = inventory.GetObjectData(player.equippedSlotIndex);
                var objectInfo = PugDatabase.GetObjectInfo(item.objectID, item.variation);
                
                if (PugDatabase.HasComponent<PaintToolCD>(item))
                {
                    CyclePaintBrush(item, shift, inventory, player);
                }
                else if (objectInfo != null && 
                         objectInfo.objectType == ObjectType.RoofingTool)
                {
                    BrushExtension.ToggleRoofingMode();
                }
            }

            if (GetAnyKeyDown(lessKeys))
            {
                BrushExtension.ChangeSize(1);
                plusHoldTime = 0;
            }

            if (GetAnyKey(lessKeys))
            {
                plusHoldTime += Time.deltaTime;
                if (plusHoldTime > minHoldTime)
                {
                    plusHoldTime = 0;
                    BrushExtension.ChangeSize(1);
                }
            }

            if (GetAnyKeyDown(moreKeys))
            {
                BrushExtension.ChangeSize(-1);
                minusHoldTime = 0;
            }

            if (GetAnyKey(moreKeys))
            {
                minusHoldTime += Time.deltaTime;
                if (minusHoldTime > minHoldTime)
                {
                    minusHoldTime = 0;
                    BrushExtension.ChangeSize(-1);
                }
            }

            BrushExtension.replaceTiles = Input.GetKey(KeyCode.LeftAlt);
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