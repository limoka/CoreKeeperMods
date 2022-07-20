using System;
using CoreLib;
using UnityEngine;

namespace ChatCommands.Chat.Commands;

public class SetSkillCommandHandler : IChatCommandHandler
{
    public CommandOutput Execute(string[] parameters)
    {
        if (parameters.Length != 2)
            return new CommandOutput("Invalid arguments for command. Correct format:\n/setSkill {skillName} {level}", Color.red);

        if (int.TryParse(parameters[1], out int level))
        {
            if (level is < 0 or > 100) return "Invalid level provided. Should be a number 0-100.";
            PlayerController player = Players.GetCurrentPlayer();
            if (player == null) return "There was an issue, try again later.";

            if (Enum.TryParse(parameters[0], out SkillID skillID))
            {
                player.SetSkillLevel(skillID, level);
            }
            else
            {
                return new CommandOutput($"Skill '{parameters[0]}' is not valid!", Color.red);
            }

            return $"{parameters[0]} successfully set to level {level}";
        }

        return new CommandOutput("Invalid level provided. Should be a number 0-100.", Color.red);
    }

    public string GetDescription()
    {
        return "Use /setSkill to set the given skill to the given level. Usage:\n/setSkill {skillName} {level}";
    }

    public string[] GetTriggerNames()
    {
        return new[] { "setSkill" };
    }
}