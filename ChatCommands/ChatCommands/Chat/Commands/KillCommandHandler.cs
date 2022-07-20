using CoreLib;
using UnityEngine;

namespace ChatCommands.Chat.Commands;

public class KillCommandHandler:IChatCommandHandler
{
    public CommandOutput Execute(string[] parameters)
    {
        PlayerController player = Players.GetCurrentPlayer();
        if (player == null) return new CommandOutput("There was an issue, try again later.", Color.red);
        player.Kill();
        return "Successfully killed player";
    }

    public string GetDescription()
    {
        return "Kills the player. Kinda self explanatory.";
    }

    public string[] GetTriggerNames()
    {
        return new[] {"kill"};
    }
}