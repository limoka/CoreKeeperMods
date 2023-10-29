using System;
using CoreLib.Commands;
using CoreLib.Commands.Communication;
using CoreLib.Util;
using PugMod;
using Unity.Entities;

namespace ChatCommands.Chat.Commands
{
    public class ResetSkillsCommandHandler : IClientCommandHandler
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
}