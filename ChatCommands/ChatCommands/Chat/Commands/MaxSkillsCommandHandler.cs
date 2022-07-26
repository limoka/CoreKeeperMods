using CoreLib;
using CoreLib.Submodules.ChatCommands;
using UnityEngine;

namespace ChatCommands.Chat.Commands;

public class MaxSkillsCommandHandler: IChatCommandHandler
{
    public CommandOutput Execute(string[] parameters)
    {
        PlayerController player = Players.GetCurrentPlayer();
        player.MaxOutAllSkills();
        return "Successfully maxed all skills";
    }

    public string GetDescription()
    {
        return "Use /maxSkills to maxes out all skills.";
    }

    public string[] GetTriggerNames()
    {
        return new[] {"maxSkills"};
    }
    
}