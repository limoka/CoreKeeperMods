using System;
using System.Linq;
using CoreLib;
using CoreLib.Submodules.ChatCommands;
using HarmonyLib;
using Unity.Mathematics;
using UnityEngine;

namespace ChatCommands.Chat.Commands;

public class SpawnCommandHandler : IChatCommandHandler
{
    public CommandOutput Execute(string[] parameters)
    {
        PlayerController player = GameManagers.GetMainManager().player;
        if (player == null) return new CommandOutput("Internal error", Color.red);

        int variation = 0;
        int2 pos = int2.zero;

        int numberParams = parameters.Count(s => int.TryParse(RemoveRelative(s), out int _));
        int nameArgCount = parameters.Length;
        
        if (numberParams < 2 || nameArgCount < 3)
        {
            return new CommandOutput("Not enough arguments! Check usage.", Color.red);
        }

        pos = PlaceTileCommand.ParsePos(parameters, nameArgCount - 1, player, out CommandOutput? commandOutput);
        if (commandOutput != null)
            return commandOutput.Value;
            
        nameArgCount -= 2;
        
        if (nameArgCount > 1 && int.TryParse(parameters[nameArgCount - 1], out int val))
        {
            variation = val;
            nameArgCount--;
        }

        string fullName = parameters.Take(nameArgCount).Join(null, " ");
        
        CommandOutput output = GiveCommandHandler.ParseItemName(fullName, out ObjectID objId);
        if (objId == ObjectID.None)
            return output;

        return SpawnID(player, objId, variation, pos);
    }

    private static string RemoveRelative(string value)
    {
        return string.Join("", value.Split('~'));
    }

    private static CommandOutput SpawnID(PlayerController player, ObjectID objId, int variation, int2 pos)
    {
        if (objId == ObjectID.Player)
        {
            return new CommandOutput("I'm going to pretend you did not ask for this. You don't know what you are doing!", Color.red);
        }
        
        ObjectInfo info = PugDatabase.GetObjectInfo(objId);
        bool hasSpawnablePrefab = info.prefabInfos._items[0].prefab != null;
        
        if (!hasSpawnablePrefab)
        {
            player.playerCommandSystem.CreateAndDropEntity(objId, pos.ToFloat3(), variation);
            return $"Spawned item {objId}";
        }

        player.playerCommandSystem.CreateEntity(objId, pos.ToFloat3(), variation);
        return $"Spawned entity {objId}";
    }

    public string GetDescription()
    {
        return "Use /spawn any entity into the world\n" +
               "/spawn {object name} {x} {y} [variation]\n" +
               "x and y is target world position. Use '~' to set position relative to you.";
    }

    public string[] GetTriggerNames()
    {
        return new[] {"spawn"};
    }
}