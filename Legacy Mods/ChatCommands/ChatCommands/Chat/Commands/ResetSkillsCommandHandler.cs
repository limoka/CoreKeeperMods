using CoreLib;
using CoreLib.Submodules.ChatCommands;
using UnityEngine;

namespace ChatCommands.Chat.Commands;

public class ResetSkillsCommandHandler:IChatCommandHandler
{
    public CommandOutput Execute(string[] parameters)
    {
        PlayerController player = Players.GetCurrentPlayer();
        player.ResetAllSkills();
        return "Successfully reset all skills";
    }

    public string GetDescription()
    {
        return "Use /resetSkills to reset all skills to 0.";
    }

    public string[] GetTriggerNames()
    {
        return new[] {"resetSkills"};
    }
    
}