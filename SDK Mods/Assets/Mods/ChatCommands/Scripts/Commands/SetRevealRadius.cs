using CoreLib.Commands;
using CoreLib.Commands.Communication;
using CoreLib.Util.Extensions;
using PugMod;
using UnityEngine;

namespace ChatCommands.Chat.Commands
{
    public class SetRevealRadius : IClientCommandHandler
    {
        public CommandOutput Execute(string[] parameters)
        {
            var mapUpdateSystem = API.Client.World.GetExistingSystemManaged<MapUpdateSystem>();
            
            if (parameters.Length <= 0)
            {
                mapUpdateSystem.ToggleMapReveal();
                var mode = mapUpdateSystem.GetValue<bool>("_largeRevealDistance");
                return$"Reveal mode now: { (mode ? "large" : "default") }";
            }

            var newMode = parameters[0] == "large";
            mapUpdateSystem.SetValue("_largeRevealDistance", newMode);
            
            return$"Reveal mode now: { (newMode ? "large" : "default") }";
        }

        public string GetDescription()
        {
            return "Use /setReveal [mode] to set map reveal mode.\n" +
                   "Valid modes: default, large. If no mode provided, will toggle.\n" +
                   "Please note: this command NO LONGER supports setting reveal radius!";
        }

        public string[] GetTriggerNames()
        {
            return new[] { "setReveal" };
        }
    }
}