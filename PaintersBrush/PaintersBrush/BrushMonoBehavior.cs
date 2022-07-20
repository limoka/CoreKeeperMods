using System;
using System.Collections.Generic;
using CoreLib;
using CoreLib.Util;
using Rewired;
using UnityEngine;

namespace PaintersBrush;

public class BrushMonoBehavior : MonoBehaviour
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

    public BrushMonoBehavior(IntPtr ptr) : base(ptr)
    {
        
    }

    private void Awake()
    {
        RewiredKeybinds.rewiredStart += OnRewiredStart;
    }

    private void OnRewiredStart()
    {
        player = ReInput.players.GetPlayer(0);
    }

    private void Update()
    {
        if (player != null)
        {
            if (player.GetButtonDown(PaintersBrushPlugin.CHANGE_COLOR))
            {
                PlayerController pc = GameManagers.GetMainManager().player;
                InventoryHandler inventory = pc.playerInventoryHandler;

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
            }
        }
    }

}