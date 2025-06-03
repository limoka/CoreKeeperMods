using CoreLib.Commands;
using CoreLib.Util;
using PugMod;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

#pragma warning disable CS0618

namespace ChatCommands.Chat.Commands
{
    public class HealCommandHandler : IServerCommandHandler
    {
        public CommandOutput Execute(string[] parameters, Entity sender)
        {
            if (parameters.Length > 0 &&
                int.TryParse(parameters[0], out int amount))
            {
                return Heal(sender, amount);
            }
            
            return Heal(sender);
        }

        public string GetDescription()
        {
            return "Use /heal to fully heal player.\n" +
                   "/heal [amount] for a specific amount.\n" +
                   "Example: /heal";
        }

        public string[] GetTriggerNames()
        {
            return new[] { "heal" };
        }

        private CommandOutput Heal(Entity sender, int amount = -1)
        {
            var player = sender.GetPlayerEntity();
            
            World serverWorld = API.Server.World;
            EntityManager entityManager = serverWorld.EntityManager;
            
            HealthCD health = entityManager.GetComponentData<HealthCD>(player);
            var conditions = entityManager.GetBuffer<SummarizedConditionEffectsBuffer>(player);
            int maxHealthWithConditions = health.GetMaxHealthWithConditions(conditions);
            
            int healAmount = amount < 0 ? maxHealthWithConditions : amount;
            
            health.health = math.clamp(health.health + healAmount, 0, maxHealthWithConditions);
            entityManager.SetComponentData(player, health);
            
            return $"Successfully healed {healAmount} HP";
        }
    }
}