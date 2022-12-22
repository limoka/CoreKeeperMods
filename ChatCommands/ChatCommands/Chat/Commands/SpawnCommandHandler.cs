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

        int variation = 0;
        
        int nameArgCount = parameters.Length;
        if (nameArgCount > 1 && int.TryParse(parameters[^1], out int val))
        {
            variation = val;
            nameArgCount--;
        }
        
        string fullName = parameters.Take(nameArgCount).Join(null, " ");
        var successfulParse = Enum.TryParse(fullName, true, out ObjectID objId);
        if (successfulParse)
        {
            return SpawnID(player, objId, variation);
        }
        
        string[] keys = GiveCommandHandler.friendlyNameDict.Keys.Where(s => s.Contains(fullName)).ToArray();
        if (keys.Length == 0)
        {
            return new CommandOutput($"No entity named '{fullName}' found!", Color.red);
        }

        if (keys.Length > 1)
        {
            return new CommandOutput(
                $"Ambiguous match ({keys.Length} results):\n{keys.Take(10).Join(null, "\n")}{(keys.Length > 10 ? "\n..." : "")}",
                Color.red);
        }

        objId = GiveCommandHandler.friendlyNameDict[keys[0]];

        return SpawnID(player, objId, variation);
    }

    private static CommandOutput SpawnID(PlayerController player, ObjectID objId, int variation)
    {
        if (objId == ObjectID.Player)
        {
            return new CommandOutput("I'm going to pretend you did not ask for this. You don't know what you are doing!", Color.red);
        }
        
        ObjectInfo info = PugDatabase.GetObjectInfo(objId);
        bool hasSpawnablePrefab = info.prefabInfos._items[0].prefab != null;
        
        if (!hasSpawnablePrefab)
        {
            player.playerCommandSystem.CreateAndDropEntity(objId, player.RenderPosition, variation);
            return $"Spawned item {objId}";
        }

        player.playerCommandSystem.CreateEntity(objId, player.RenderPosition, variation);
        return $"Spawned entity {objId}";
    }

    public string GetDescription()
    {
        return "Spawn any entity into the world";
    }

    public string[] GetTriggerNames()
    {
        return new[] {"spawn"};
    }
}