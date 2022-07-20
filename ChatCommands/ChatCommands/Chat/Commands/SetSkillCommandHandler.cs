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
        try
        {
            int level = int.Parse(parameters[1]);
            if (level is < 0 or > 100) return "Invalid level provided. Should be a number 0-100.";
            PlayerController player = Players.GetCurrentPlayer();
            if (player == null) return "There was an issue, try again later.";

            try
            {
                SkillID skillID = Enum.Parse<SkillID>(parameters[0]);
                player.SetSkillLevel(skillID, level);
            }
            catch (ArgumentException)
            {
                return new CommandOutput($"Skill '{parameters[0]}' is not valid!", Color.red);
            }

            return $"{parameters[0]} successfully set to level {level}";
        }
        catch
        {
            return new CommandOutput("Invalid level provided. Should be a number 0-100.", Color.red);
        }
    }

    public string GetDescription()
    {
        return "Sets the given skill to the given level.\n/setSkill {skillName} {level}";
    }

    public string[] GetTriggerNames()
    {
        return new[] { "setSkill" };
    }
}