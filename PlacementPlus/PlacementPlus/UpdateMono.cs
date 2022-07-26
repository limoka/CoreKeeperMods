using System;
using System.Collections.Generic;
using CoreLib;
using CoreLib.Submodules.RewiredExtension;
using CoreLib.Util;
using Rewired;
using UnityEngine;

namespace PlacementPlus;

public class UpdateMono : MonoBehaviour
{
    private Player player;
    
    private static int lastColorIndex = -1;

    private static readonly Dictionary<int, int> colorIndexLookup = new Dictionary<int, int>
    {
        { 1, 71 },
        { 2, 72 },
        { 3, 70 },
        { 4, 73 },
        { 5, 74 },
        { 6, 75 },
        { 7, 76 },
        { 8, 77 }
    };

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

    private void Update()
    {
        if (player != null)
        {
            if (player.GetButtonDown(PlacementPlusPlugin.CHANGE_ORIENTATION))
            {
                BrushExtension.ToggleMode();
            }
            
            if (player.GetButtonDown(PlacementPlusPlugin.ROTATE))
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
                    if (lastColorIndex == -1)
                    {
                        lastColorIndex = PugDatabase.GetComponent<PaintToolCD>(item).paintIndex;
                    }

                    lastColorIndex++;
                    if (lastColorIndex > 8)
                    {
                        lastColorIndex = 1;
                    }

                    ObjectID newObjectId = (ObjectID)colorIndexLookup[lastColorIndex];
                    
                    inventory.DestroyObject(pc.equippedSlotIndex, item.objectID);
                    inventory.CreateItem(pc.equippedSlotIndex, newObjectId, 1, pc.WorldPosition);
                }
                else
                {
                    BrushExtension.ChangeRotation(1);
                }
            }

            if (player.GetButtonDown(PlacementPlusPlugin.INCREASE_SIZE))
            {
                BrushExtension.ChangeSize(1);
            }

            if (player.GetButtonDown(PlacementPlusPlugin.DECREASE_SIZE))
            {
                BrushExtension.ChangeSize(-1);
            }

            if (PlacementPlusPlugin.forceKeyMode.Value == KeyMode.HOLD)
            {
                BrushExtension.forceRotation = !player.GetButton(PlacementPlusPlugin.FORCEADJACENT);
            }
            else
            {
                if (player.GetButtonDown(PlacementPlusPlugin.FORCEADJACENT))
                {
                    BrushExtension.forceRotation = !BrushExtension.forceRotation;
                }
            }
        }
    }
}