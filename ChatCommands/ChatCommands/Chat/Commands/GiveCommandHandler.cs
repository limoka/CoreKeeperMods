using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using CoreLib;
using CoreLib.Submodules.ChatCommands;
using HarmonyLib;
using UnityEngine;

namespace ChatCommands.Chat.Commands;

public class GiveCommandHandler : IChatCommandHandler
{
    public static Dictionary<string, ObjectID> friendlyNameDict = new Dictionary<string, ObjectID>();

    public CommandOutput Execute(string[] parameters)
    {
        int count = 1;
        int nameArgCount = parameters.Length;
        if (int.TryParse(parameters[^1], out int val))
        {
            count = val;
            nameArgCount--;
        }

        string fullName = parameters.Take(nameArgCount).Join(null, " ");
        fullName = fullName.ToLower();

        if (Enum.TryParse(fullName, out ObjectID objId))
        {
            AddToInventory(objId, count);
            return $"Successfully added {count} {parameters[0]}";
        }

        string[] keys = friendlyNameDict.Keys.Where(s => s.Contains(fullName)).ToArray();
        if (keys.Length == 0)
        {
            return new CommandOutput($"No item named '{fullName}' found!", Color.red);
        }

        if (keys.Length > 1)
        {
            try
            {
                string key = keys.First(s => s.Equals(fullName));
                AddToInventory(friendlyNameDict[key], count);
                return $"Successfully added {count} {keys[0]}";
            }
            catch (Exception)
            {
                return new CommandOutput($"Ambigous match ({keys.Length} results):\n{keys.Take(10).Join(null, "\n")}{(keys.Length > 10 ? "\n..." : "")}", Color.red);
            }
        }

        AddToInventory(friendlyNameDict[keys[0]], count);
        return $"Successfully added {count} {keys[0]}";
    }

    public string GetDescription()
    {
        return
            "Use /give to give yourself any item. \n/give {itemName} [count]\nThe count parameter defaults to 1.";
    }

    public string[] GetTriggerNames()
    {
        return new[] { "give" };
    }

    private bool GetCount(string[] parameters, out int count)
    {
        count = 1;
        if (parameters.Length == 2)
        {
            if (int.TryParse(parameters[1], out int val))
            {
                count = val;
            }
            else
            {
                return false;
            }
        }

        return true;
    }

    private void AddToInventory(ObjectID objId, int amount)
    {
        PlayerController player = GameManagers.GetMainManager().player;
        if (player == null) return;
        InventoryHandler handler = player.playerInventoryHandler;
        if (handler == null) return;
        handler.CreateItem(0, objId, amount, player.WorldPosition, 0);
    }
}