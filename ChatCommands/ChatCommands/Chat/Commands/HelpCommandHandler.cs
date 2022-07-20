using System;
using System.Linq;
using PlayerCommand;
using UnityEngine;

namespace ChatCommands.Chat.Commands;

public class HelpCommandHandler: IChatCommandHandler
{
    public CommandOutput Execute(string[] parameters)
    {
        switch (parameters.Length)
        {
            case 0:
                string commandsString = CommandRegistry.CommandHandlers.Aggregate("\n", (str, handler) => handler.GetTriggerNames().Length > 0 ? $"{str}\n{handler.GetTriggerNames()[0]}" : str);
                return $"\nUse /help {{command}} for more information.\nCommands:{commandsString}";
            case 1:
                try
                {
                    IChatCommandHandler validCommandHandler = CommandRegistry.CommandHandlers.Find(element => element.GetTriggerNames().Contains(parameters[0]));
                    return validCommandHandler.GetDescription();
                } catch { return "This command does not exist. Do /help to view all commands.";}

            default:
                return new CommandOutput("Invalid arguments. Do /help to view all commands.", Color.red);
        }
    }

    public string GetDescription()
    {
        return "Use /help {command} for more information on a command.";
    }

    public string[] GetTriggerNames()
    {
        return new[] {"help"};
    }
}