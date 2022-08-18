using CoreLib.Submodules.ChatCommands;
using Unity.Collections;
using Unity.Entities;

namespace ChatCommands.Chat.Commands;

public class DisableAICommand : IChatCommandHandler
{
    public static bool allowAttack = true;

    public CommandOutput Execute(string[] parameters)
    {
        World serverWorld = Manager._instance._ecsManager.ServerWorld;
        EntityManager entityManager = serverWorld.EntityManager;

        allowAttack = !allowAttack;

        EntityQuery query =
            serverWorld.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<PheromoneAdderCD>());

        NativeArray<Entity> result = query.ToEntityArray(Allocator.Temp);

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