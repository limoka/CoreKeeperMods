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

        int numberParams = GetNumberParams(parameters);
        int nameArgCount = parameters.Length;
        
        if (numberParams < 2 || nameArgCount < 3)
        {
            return new CommandOutput("Not enough arguments! Check usage via /help spawn.", Color.red);
        }

        if (numberParams > 2)
        {
            if (!int.TryParse(parameters[nameArgCount - 1], out variation))
                return new CommandOutput($"{parameters[nameArgCount - 1]} is not a valid number!");
            nameArgCount--;
        }
        
        int2 pos = CommandUtil.ParsePos(parameters, nameArgCount - 1, player, out CommandOutput? commandOutput1);
        if (commandOutput1 != null)
            return commandOutput1.Value;

        nameArgCount -= 2;

        string fullName = parameters.Take(nameArgCount).Join(null, " ");
        
        CommandOutput output = CommandUtil.ParseItemName(fullName, out ObjectID objId);
        if (objId == ObjectID.None)
            return output;

        return SpawnID(player, objId, variation, pos);
    }

    private static int GetNumberParams(string[] parameters)
    {
        int numberParams = 0;
        
        for (int i = parameters.Length - 1; i >= 0; i--)
        {
            string value = string.Join("", parameters[i].Split('~'));
            if (int.TryParse(value, out int _))
            {
                numberParams++;
                continue;
            }

            break;
        }

        return numberParams;
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