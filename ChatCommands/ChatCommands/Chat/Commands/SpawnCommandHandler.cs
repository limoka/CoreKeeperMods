using System;
using System.Linq;
using CoreLib;
using CoreLib.Submodules.ChatCommands;
using HarmonyLib;
using UnityEngine;

namespace ChatCommands.Chat.Commands;

public class SpawnCommandHandler : IChatCommandHandler
{
    public CommandOutput Execute(string[] parameters)
    {
        PlayerController player = GameManagers.GetMainManager().player;
        if (player == null) return new CommandOutput("Internal error", Color.red);

        string fullName = parameters.Take(parameters.Length).Join(null, " ");
        fullName = fullName.ToLower();

        if (Enum.TryParse(fullName, out ObjectID objId))
        {
            player.playerCommandSystem.CreateEntity(objId, player.RenderPosition);
            return $"Spawned entity {objId.ToString()}";
        }

        string[] keys = GiveCommandHandler.friendlyNameDict.Keys.Where(s => s.Contains(fullName)).ToArray();
        if (keys.Length == 0)
        {
            return new CommandOutput($"No entity named '{fullName}' found!", Color.red);
        }

        if (keys.Length > 1)
        {
            return new CommandOutput($"Ambigous match ({keys.Length} results):\n{keys.Take(10).Join(null, "\n")}{(keys.Length > 10 ? "\n..." : "")}",
                Color.red);
        }
        objId = GiveCommandHandler.friendlyNameDict[keys[0]];
        
        player.playerCommandSystem.CreateEntity(objId, player.RenderPosition);
        return $"Spawned entity {objId.ToString()}";
    }

    public string GetDescription()
    {
        return "Spawn any entity into the world";
    }

    public string[] GetTriggerNames()
    {
        return new[] { "spawn" };
    }
}