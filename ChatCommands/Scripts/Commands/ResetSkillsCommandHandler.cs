using System;
using CoreLib.Submodule.Command.Data;
using CoreLib.Submodule.Command.Interface;
using CoreLib.Submodule.Command.Util;
using CoreLib.Util;
using PugMod;
using Unity.Entities;

namespace ChatCommands.Chat.Commands
{
    public class ResetSkillsCommandHandler : IServerCommandHandler
    {
        public CommandOutput Execute(string[] parameters, Entity sender)
        {
            Entity player = sender.GetPlayerEntity();
            
            for (int i = 0; i < (int)SkillID.NUM_SKILLS; ++i)
            {
                SetSkillCommandHandler.SetSkillValue(player, (SkillID) i, 0);
            }
            
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
}