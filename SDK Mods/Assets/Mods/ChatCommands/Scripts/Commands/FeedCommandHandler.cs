using CoreLib.Commands;
using CoreLib.Util;
using PugMod;
using Unity.Entities;
using Unity.Mathematics;

namespace ChatCommands.Chat.Commands
{
    public class FeedCommandHandler : IClientCommandHandler
    {
        public CommandOutput Execute(string[] parameters)
        {
            if (parameters.Length != 1) return Feed();
            try
            {
                int amount = int.Parse(parameters[0]);
                return Feed(amount);
            }
            catch { return Feed(); }
        }

        public string GetDescription()
        {
            return "Use /feed to fully feed player.\n/feed {amount} for a specific amount.";
        }

        public string[] GetTriggerNames()
        {
            return new[] { "feed" };
        }

        static CommandOutput Feed(int amount = -1)
        {
            PlayerController player = Players.GetCurrentPlayer();
            int hungerAmount = amount < 0 ? (100 - player.hungerComponent.hunger) : amount;
            player.AddHunger(hungerAmount);
            return $"Successfully fed {hungerAmount} food";
        }
    }
}