﻿using CoreLib.Commands;
using CoreLib.Util;
using PugMod;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

#pragma warning disable CS0618

namespace ChatCommands.Chat.Commands
{
    public class HealCommandHandler : IClientCommandHandler
    {
        public CommandOutput Execute(string[] parameters)
        {
            if (parameters.Length != 1) return Heal();
            try
            {
                int amount = int.Parse(parameters[0]);
                return Heal(amount);
            }
            catch { return Heal(); }
        }

        public string GetDescription()
        {
            return "Use /heal to fully heal player.\n/heal [amount] for a specific amount.";
        }

        public string[] GetTriggerNames()
        {
            return new[] { "heal" };
        }

        private CommandOutput Heal(int amount = -1)
        {
            PlayerController player = Players.GetCurrentPlayer();
            int healAmount = amount < 0 ? (player.GetMaxHealth() - player.currentHealth) : amount;
            
            player.playerCommandSystem.SetHealth(player.entity, player.currentHealth + healAmount);
            return $"Successfully healed {healAmount} HP";
        }
    }
}