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
                PlaceObjectSlot_Patch.ToggleMode();
            }

            if (player.GetButtonDown(PlacementPlusPlugin.INCREASE_SIZE))
            {
                PlaceObjectSlot_Patch.ChangeRadius(1);
            }

            if (player.GetButtonDown(PlacementPlusPlugin.DECREASE_SIZE))
            {
                PlaceObjectSlot_Patch.ChangeRadius(-1);
            }
        }
    }
}