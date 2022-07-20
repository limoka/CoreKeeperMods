using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ChatCommands.Chat.Commands;

public static class CommandRegistry
{
    public static List<IChatCommandHandler> CommandHandlers;

    static CommandRegistry()
    {
        Type[] commands = Assembly.GetExecutingAssembly().GetTypes().Where(type => typeof(IChatCommandHandler).IsAssignableFrom(type)).ToArray();
        CommandHandlers = new List<IChatCommandHandler>(commands.Length);
        
        foreach (Type commandType in commands)
        {
            if (commandType == typeof(IChatCommandHandler)) continue;

            try
            {
                IChatCommandHandler handler = (IChatCommandHandler)Activator.CreateInstance(commandType);
                CommandHandlers.Add(handler);
            }
            catch (Exception e)
            {
                ChatCommandsPlugin.logger.LogWarning($"Failed to register command {commandType}!\n{e}");
            }
        }
    }
}