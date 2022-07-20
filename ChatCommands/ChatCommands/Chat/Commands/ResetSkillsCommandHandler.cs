using CoreLib;
using UnityEngine;

namespace ChatCommands.Chat.Commands;

public class ResetSkillsCommandHandler:IChatCommandHandler
{
    public CommandOutput Execute(string[] parameters)
    {
        PlayerController player = Players.GetCurrentPlayer();
        if (player == null) return new CommandOutput("There was an issue, try again later.", Color.red);
        player.ResetAllSkills();
        return "Successfully reset all skills";
    }

    public string GetDescription()
    {
        return "Resets all skills to 0.";
    }

    public string[] GetTriggerNames()
    {
        return new[] {"resetSkills"};
    }
}