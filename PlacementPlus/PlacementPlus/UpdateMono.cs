using System;
using CoreLib;
using CoreLib.Util;
using Rewired;
using UnityEngine;

namespace PlacementPlus;

public class UpdateMono : MonoBehaviour
{
    private Player player;

    public UpdateMono(IntPtr ptr) : base(ptr)
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
            if (player.GetButtonDown(PlacementPlusPlugin.CHANGE_ORIENTATION))
            {
                BrushExtension.ToggleMode();
            }
            
            if (player.GetButtonDown(PlacementPlusPlugin.ROTATE))
            {
                BrushExtension.ChangeRotation(1);
            }

            if (player.GetButtonDown(PlacementPlusPlugin.INCREASE_SIZE))
            {
                BrushExtension.ChangeSize(1);
            }

            if (player.GetButtonDown(PlacementPlusPlugin.DECREASE_SIZE))
            {
                BrushExtension.ChangeSize(-1);
            }
        }
    }
}