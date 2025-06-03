using System;
using CoreLib.Commands;
using CoreLib.Commands.Communication;
using CoreLib.Util;
using PugMod;
using Unity.Entities;
using UnityEngine;

namespace ChatCommands.Chat.Commands
{
    public class SetSkillCommandHandler : IServerCommandHandler
    {
        public CommandOutput Execute(string[] parameters, Entity sender)
        {
            if (parameters.Length != 2)
                return new CommandOutput("Invalid arguments for command. Correct format:\n/setSkill {skillName} {level}", CommandStatus.Error);

            if (!int.TryParse(parameters[1], out int level)) 
                return new CommandOutput("Invalid level provided. Should be a number 0-100.", CommandStatus.Error);
            
            if (level is < 0 or > 100) return "Invalid level provided. Should be a number 0-100.";
            
            Entity player = sender.GetPlayerEntity();
            if (player == Entity.Null) return "There was an issue, try again later.";

            if (!Enum.TryParse(parameters[0], out SkillID skillID))
            {
                var allNames = Enum.GetNames(typeof(SkillID));
                return new CommandOutput($"Skill '{parameters[0]}' is not valid!\nKnown skill names: {string.Join(", ", allNames)}", CommandStatus.Error);
            }

            int skillFromLevel = SkillExtensions.GetSkillFromLevel(skillID, level);
            SetSkillValue(player, skillID, skillFromLevel);
            return $"{parameters[0]} successfully set to level {level}";
        }

        public static void SetSkillValue(Entity player, SkillID skillID, int amount)
        {
            World serverWorld = API.Server.World;
            EntityManager entityManager = serverWorld.EntityManager;

            var skillBuffer = entityManager.GetBuffer<SkillBuffer>(player);
            var skillConditionsBuffer = entityManager.GetBuffer<SkillConditionsBuffer>(player);

            ref SkillBuffer elementAt = ref skillBuffer.ElementAt((int)skillID);
            elementAt.Value = amount;
            
            ConditionData conditionDataForSkill = SkillExtensions.GetConditionDataForSkill(skillID, elementAt.Value);
            EntityUtility.SetSkillCondition(skillConditionsBuffer, conditionDataForSkill);
        }

        public string GetDescription()
        {
            return "Use /setSkill to set the given skill to the given level. Usage:\n" +
                   "/setSkill {skillName} {level}" +
                   "\n\n" +
                   "Example:\n" +
                   "/setSkill Mining 100";
        }

        public string[] GetTriggerNames()
        {
            return new[] { "setSkill" };
        }
    }
}