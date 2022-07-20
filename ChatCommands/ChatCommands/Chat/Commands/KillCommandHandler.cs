using CoreLib;
using UnityEngine;

namespace ChatCommands.Chat.Commands;

public class KillCommandHandler:IChatCommandHandler
{
    public CommandOutput Execute(string[] parameters)
    {
        PlayerController player = Players.GetCurrentPlayer();
        player.Kill();
        return "Successfully killed player";
    }

    public string GetDescription()
    {
        return "Use /kill to kill the player.";
    }

    public string[] GetTriggerNames()
    {
        return new[] {"kill"};
    }
}