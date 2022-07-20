using System;
using System.Runtime.InteropServices;
using CoreLib;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics;
using UnityEngine;
using Math = System.Math;

namespace ChatCommands.Chat.Commands;

public class NoclipCommand : IChatCommandHandler
{
    public bool noclipActive = false;
    public static CollisionFilter defaultFilter;

    public CommandOutput Execute(string[] parameters)
    {
        PlayerController player = Players.GetCurrentPlayer();

        try
        {
            switch (parameters.Length)
            {
                case 0:
                    noclipActive = !noclipActive;
                    break;
                case 1:
                    if (bool.TryParse(parameters[0], out bool value))
                    {
                        noclipActive = value;
                    }
                    break;
                case 2 when parameters[0].Equals("speed"):
                    if (float.TryParse(parameters[1], out float multiplier))
                    {
                        multiplier = Math.Clamp(multiplier, 0.5f, 10f);
                        player.noClipMovementSpeedMultipler = 6.25f*multiplier;
                        return $"noclip speed multiplier now is {multiplier}";
                    }
                    return new CommandOutput($"{parameters[1]} is not a valid number!", Color.red);
            }

            if (noclipActive)
            {
                player.EnterState(player.sNoClip);
            }
            else
            {
                player.EnterState(player.sWalk);
            }

            return $"Noclip is {noclipActive}";
        }
        catch (Exception e)
        {
            ChatCommandsPlugin.logger.LogWarning($"Failed to execute command noclip: \n{e}");
            return new CommandOutput("Failed to execute!", Color.red);
        }
    }

    public string GetDescription()
    {
        return "noclip - move freely without physical limitations!\nnoclip speed <multilplier> - set noclip speed multiplier";
    }

    public string[] GetTriggerNames()
    {
        return new[] { "noclip" };
    }
}