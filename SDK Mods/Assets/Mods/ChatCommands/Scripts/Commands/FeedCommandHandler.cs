using CoreLib.Commands;
using CoreLib.Util;
using PugMod;
using Unity.Entities;
using Unity.Mathematics;

namespace ChatCommands.Chat.Commands
{
    public class FeedCommandHandler : IServerCommandHandler
    {
        public CommandOutput Execute(string[] parameters, Entity sender)
        {
            if (parameters.Length > 0 &&
                int.TryParse(parameters[0], out int amount))
            {
                return Feed(sender, amount);
            }
            
            return Feed(sender);
        }

        public string GetDescription()
        {
            return "Use /feed to fully feed player.\n" +
                   "/feed {amount} for a specific amount.";
        }

        public string[] GetTriggerNames()
        {
            return new[] { "feed" };
        }

        static CommandOutput Feed(Entity sender, int amount = -1)
        {
            var player = sender.GetPlayerEntity();
            
            World serverWorld = API.Server.World;
            EntityManager entityManager = serverWorld.EntityManager;
            
            HungerCD hunger = entityManager.GetComponentData<HungerCD>(player);
            int hungerAmount = amount < 0 ? 100 : amount;
            
            hunger.hunger = math.clamp(hunger.hunger + hungerAmount, 0, 100);
            entityManager.SetComponentData(player, hunger);
            
            return $"Successfully fed {hungerAmount} food";
        }
    }
}