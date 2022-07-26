using CoreLib;
using CoreLib.Submodules.ChatCommands;
using UnityEngine;

namespace ChatCommands.Chat.Commands;

public class ToggleInvincibleCommandHandler:IChatCommandHandler
{
    public CommandOutput Execute(string[] parameters)
    {
        PlayerController player = Players.GetCurrentPlayer();
        player.SetInvincibility(!player.invincible);
        return $"Successfully set invincibility to {!player.invincible}";
    }

    public string GetDescription()
    {
        return "Use /invincible to toggle invincibility for the player.";
    }

    public string[] GetTriggerNames()
    {
        return new[] {"invincible"};
    }
    
}