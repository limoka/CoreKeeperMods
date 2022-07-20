using CoreLib;
using UnityEngine;

namespace ChatCommands.Chat.Commands;

public class GiveCommandHandler : IChatCommandHandler
{
    public CommandOutput Execute(string[] parameters)
    {
        string[] argType = parameters[0].Split(':');
        switch (argType.Length)
        {
            case 2 when argType[0] == "id":
                try
                {
                    ObjectID objId = (ObjectID)int.Parse(argType[1]);
                    try
                    {
                        int count = (parameters.Length == 2) ? int.Parse(parameters[1]) : 0;
                        AddToInventory(objId, count);
                        return $"Successfully added {count} {argType[1]}";
                    }
                    catch
                    {
                        AddToInventory(objId, 1);
                        return $"Successfully added 1 {argType[1]}";
                    }
                }
                catch
                {
                    return new CommandOutput("Invalid object Id", Color.red);
                }
            case 2 when argType[0] == "name":
            {
                ObjectID.TryParse(argType[1], out ObjectID objId);
                try
                {
                    int count = (parameters.Length == 2) ? int.Parse(parameters[1]) : 0;
                    AddToInventory(objId, count);
                    return $"Successfully added {count} {argType[1]}";
                }
                catch
                {
                    AddToInventory(objId, 1);
                    return $"Successfully added {1} {argType[1]}";
                }
            }
            default:
                return new CommandOutput("Invalid command. Try /give name:{itemName} {count?} or /give id:{itemId} {count?}", Color.red);
        }
    }

    public string GetDescription()
    {
        return
            "Give yourself any item. Options:\n/give name:{itemName} {count?}\n/give id:{itemId} {count?}\nThe count parameter defaults to 1.";
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