using CoreLib.Submodules.ChatCommands;
using UnityEngine;

namespace ChatCommands.Chat.Commands
{
    public class SetRevealRadius : IChatCommandHandler
    {
        public CommandOutput Execute(string[] parameters)
        {
            if (parameters.Length <= 0)
            {
                return new CommandOutput("Please provide radius", Color.red);
            }

            if (parameters[0] == "default")
            {
                Manager.ui.mapUI.revealLargeMap = false;
                MapUI_Patch.bigRevealRadius = 12;
                return "Reveal radius is reset";
            }

            if (float.TryParse(parameters[0], out float value))
            {
                Manager.ui.mapUI.revealLargeMap = true;
                MapUI_Patch.bigRevealRadius = value;
                return $"Reveal radius is now {value}";
            }

            return new CommandOutput($"{parameters[0]} is not a valid number!", Color.red);
        }

        public string GetDescription()
        {
            return "Use /setReveal {radius} to set map reveal radius\n";
        }

        public string[] GetTriggerNames()
        {
            return new[] { "setReveal" };
        }
    }
}