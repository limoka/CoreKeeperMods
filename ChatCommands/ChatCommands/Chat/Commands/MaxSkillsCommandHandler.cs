using CoreLib;
using UnityEngine;

namespace ChatCommands.Chat.Commands;

public class MaxSkillsCommandHandler: IChatCommandHandler
{
    public CommandOutput Execute(string[] parameters)
    {
        PlayerController player = Players.GetCurrentPlayer();
        if (player == null) return new CommandOutput("There was an issue, try again later.", Color.red);
        player.MaxOutAllSkills();
        return "Successfully maxed all skills";
    }

    public string GetDescription()
    {
        return "Maxes out all skills.";
    }

    public string[] GetTriggerNames()
    {
        return new[] {"maxSkills"};
    }
}