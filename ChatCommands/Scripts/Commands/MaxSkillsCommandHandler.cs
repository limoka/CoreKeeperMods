using System;
using CoreLib.Submodule.Command.Data;
using CoreLib.Submodule.Command.Interface;
using CoreLib.Submodule.Command.Util;
using CoreLib.Util;
using PugMod;
using Unity.Entities;

namespace ChatCommands.Chat.Commands
{
    public class MaxSkillsCommandHandler : IServerCommandHandler
    {
        public CommandOutput Execute(string[] parameters, Entity sender)
        {
            Entity player = sender.GetPlayerEntity();
            
            for (int i = 0; i < (int)SkillID.NUM_SKILLS; ++i)
            {
                SkillID skillID = (SkillID) i;
                int maxLevel = SkillExtensions.GetMaxSkillLevel(skillID);
                int skillFromLevel = SkillExtensions.GetSkillFromLevel(skillID, maxLevel);
                SetSkillCommandHandler.SetSkillValue(player, skillID, skillFromLevel);
            }
            
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
}