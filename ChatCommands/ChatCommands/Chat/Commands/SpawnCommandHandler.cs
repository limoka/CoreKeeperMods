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
        bool hasSpawnablePrefab = true;
        string fullName = String.Concat(parameters).ToLower().Replace(" ", "");
        var successfulParse = Enum.TryParse(fullName, true, out ObjectID objId);
        try
        {
            
            hasSpawnablePrefab = Manager._instance._ecsManager.ecsPrefabTable.prefabList[0].GetComponent<PugDatabaseAuthoring>().prefabList._items.Where(x => x.name.ToLower().Replace("entity", "") == fullName).Select(x => x.objectInfo.prefabInfos[0].prefab).First() != null;
            ChatCommandsPlugin.logger.LogInfo($"{fullName} has spawnable prefab: {hasSpawnablePrefab}, successful parse: {successfulParse}");
        } catch (Exception ex) { hasSpawnablePrefab = true; ChatCommandsPlugin.logger.LogInfo($"{fullName} {ex.Message}"); }

        if (!hasSpawnablePrefab)
        {
            player.playerCommandSystem.CreateAndDropEntity(objId, player.RenderPosition);
            return $"Spawned entity {objId}";
        }

        if (successfulParse)
        {
            player.playerCommandSystem.CreateEntity(objId, player.RenderPosition);
            return $"Spawned entity {objId}";
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
        return $"Spawned entity {objId}";
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