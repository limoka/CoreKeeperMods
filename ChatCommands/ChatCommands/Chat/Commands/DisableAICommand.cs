using System;
using CoreLib;
using Il2CppSystem.Runtime.InteropServices;
using UnhollowerRuntimeLib;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace ChatCommands.Chat.Commands;

public class DisableAICommand : IChatCommandHandler
{
    public static bool allowAttack = true;

    public CommandOutput Execute(string[] parameters)
    {
        World serverWorld = Manager._instance._ecsManager.ServerWorld;
        EntityManager entityManager = serverWorld.EntityManager;

        try
        {
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
        catch (Exception e)
        {
            return new CommandOutput("Failed to execute!", Color.red);
        }
    }

    public string GetDescription()
    {
        return "Use passive to stop enemies from targeting you.";
    }

    public string[] GetTriggerNames()
    {
        return new[] { "passive" };
    }
}