using System;
using CoreLib.Commands;
using CoreLib.Commands.Communication;
using CoreLib.Util;
using PugMod;
using Unity.Entities;

namespace ChatCommands.Chat.Commands
{
    public class MaxSkillsCommandHandler : IClientCommandHandler
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
}