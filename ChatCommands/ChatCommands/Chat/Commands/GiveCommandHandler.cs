using System;
using CoreLib;
using UnityEngine;

namespace ChatCommands.Chat.Commands;

public class GiveCommandHandler : IChatCommandHandler
{
    public CommandOutput Execute(string[] parameters)
    {
        if (Enum.TryParse(parameters[0], out ObjectID objId))
        {
            int count = 1;
            if (parameters.Length == 2)
            {
                if (int.TryParse(parameters[1], out int val))
                {
                    count = val;
                }
                else
                {
                    return new CommandOutput($"Invalid count: {parameters[1]}", Color.red);
                }
            }

            AddToInventory(objId, count);
            return $"Successfully added {count} {parameters[0]}";
        }

        return new CommandOutput($"Invalid item: {parameters[0]}", Color.red);
    }

    public string GetDescription()
    {
        return
            "Give yourself any item. Options:\n/give name:{itemName} [count]\n/give id:{itemId} [count]\nThe count parameter defaults to 1.";
    }

    public string[] GetTriggerNames()
    {
        return new[] { "give" };
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