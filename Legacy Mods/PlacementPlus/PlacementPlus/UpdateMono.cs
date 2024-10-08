﻿using System;
using System.Collections.Generic;
using CoreLib;
using CoreLib.Submodules.ModComponent;
using CoreLib.Submodules.RewiredExtension;
using CoreLib.Util;
using Rewired;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace PlacementPlus;

public class UpdateMono : MonoBehaviour
{
    private Player player;
    
    private static float plusHoldTime;
    private static float minusHoldTime;

    private static readonly Dictionary<int, ObjectID> colorIndexLookup = new Dictionary<int, ObjectID>();
    
    private static int lastColorIndex = -1;
    private static int maxPaintIndex = -1;
    
    public UpdateMono(IntPtr ptr) : base(ptr)
    {
        
    }

    private void Awake()
    {
        RewiredExtensionModule.rewiredStart += OnRewiredStart;
    }

    private void OnRewiredStart()
    {
        player = ReInput.players.GetPlayer(0);
    }

    private void InitColorIndexLookup()
    {
        if (colorIndexLookup.Count > 0) return;
        
        World world = PugDatabase.world;
        
        EntityQuery brushQuery = world.EntityManager.CreateEntityQuery(
            ComponentModule.ReadOnly<ObjectDataCD>(),
            ComponentModule.ReadOnly<Prefab>(),
            ComponentModule.ReadOnly<PaintToolCD>());
        
        NativeArray<Entity> brushEntities = brushQuery.ToEntityArray(Allocator.Temp);
        foreach (Entity brushEntity in brushEntities)
        {
            ObjectDataCD objectDataCd = world.EntityManager.GetModComponentData<ObjectDataCD>(brushEntity);
            PaintToolCD paintToolCd = world.EntityManager.GetModComponentData<PaintToolCD>(brushEntity);
            if (paintToolCd.paintIndex != 0)
            {
                colorIndexLookup.Add(paintToolCd.paintIndex, objectDataCd.objectID);
                maxPaintIndex = Math.Max(maxPaintIndex, paintToolCd.paintIndex);
            }
        }

        brushEntities.Dispose();
        brushQuery.Dispose();
    }
    

    private void Update()
    {
        if (player != null)
        {
            if (player.GetButtonDown(PlacementPlusPlugin.CHANGE_ORIENTATION))
            {
                BrushExtension.ToggleMode();
            }
            
            if (player.GetButtonDown(PlacementPlusPlugin.CHANGE_COLOR))
            {
                Manager manager = GameManagers.GetMainManager();
                if (manager == null) return;
                PlayerController pc = manager.player;
                if (pc == null) return;
                InventoryHandler inventory = pc.playerInventoryHandler;
                if (inventory == null) return;

                ObjectDataCD item = inventory.GetObjectData(pc.equippedSlotIndex);
                if (PugDatabase.HasComponent<PaintToolCD>(item))
                {
                    InitColorIndexLookup();
                    if (lastColorIndex == -1)
                    {
                        lastColorIndex = PugDatabase.GetComponent<PaintToolCD>(item).paintIndex;
                    }

                    lastColorIndex++;
                    if (lastColorIndex > maxPaintIndex)
                    {
                        lastColorIndex = 1;
                    }

                    ObjectID newObjectId = colorIndexLookup[lastColorIndex];
                    
                    inventory.DestroyObject(pc.equippedSlotIndex, item.objectID);
                    inventory.CreateItem(pc.equippedSlotIndex, newObjectId, 1, pc.WorldPosition);
                }
            }

            if (player.GetButtonDown(PlacementPlusPlugin.INCREASE_SIZE))
            {
                BrushExtension.ChangeSize(1);
                plusHoldTime = 0;
            }

            if (player.GetButton(PlacementPlusPlugin.INCREASE_SIZE))
            {
                plusHoldTime += Time.deltaTime;
                if (plusHoldTime > PlacementPlusPlugin.minHoldTime.Value)
                {
                    plusHoldTime = 0;
                    BrushExtension.ChangeSize(1);
                }
            }

            if (player.GetButtonDown(PlacementPlusPlugin.DECREASE_SIZE))
            {
                BrushExtension.ChangeSize(-1);
                minusHoldTime = 0;
            }
            
            if (player.GetButton(PlacementPlusPlugin.DECREASE_SIZE))
            {
                minusHoldTime += Time.deltaTime;
                if (minusHoldTime > PlacementPlusPlugin.minHoldTime.Value)
                {
                    minusHoldTime = 0;
                    BrushExtension.ChangeSize(-1);
                }
            }

            BrushExtension.replaceTiles = player.GetButton(PlacementPlusPlugin.REPLACE_BUTTON);
        }
    }
}