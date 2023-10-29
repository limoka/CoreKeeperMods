using CoreLib.Commands;
using PugMod;
using Unity.Collections;
using Unity.Entities;

namespace ChatCommands.Chat.Commands
{
    public class DisableAICommand : IServerCommandHandler
    {
        public static bool allowAttack = true;

        public CommandOutput Execute(string[] parameters, Entity sender)
        {
            World serverWorld = API.Server.World;
            EntityManager entityManager = serverWorld.EntityManager;

            allowAttack = !allowAttack;

            EntityQuery query =
                serverWorld.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<PheromoneAdderCD>());

            var result = query.ToEntityArray(Allocator.Temp);

            foreach (Entity playerEntity in result)
            {
                FactionCD faction = entityManager.GetComponentData<FactionCD>(playerEntity);
                faction.faction = allowAttack ? FactionID.Player : FactionID.AttacksAllButNotAttacked;
                faction.originalFaction = faction.faction;
                entityManager.SetComponentData(playerEntity, faction);
            }

            return allowAttack ? "Enemy AI is no longer passive" : "Enemy AI is passive";
        }

        public string GetDescription()
        {
            return "Use /passive to stop enemies from targeting you.";
        }

        public string[] GetTriggerNames()
        {
            return new[] { "passive" };
        }
    }
}