using CoreLib.Commands;
using CoreLib.Commands.Communication;
using CoreLib.Util;
using CoreLib.Util.Extensions;

namespace ChatCommands.Chat.Commands
{
    public class ToggleInvincibleCommandHandler : IClientCommandHandler
    {
        public CommandOutput Execute(string[] parameters)
        {
            if (parameters.Length == 0) return new CommandOutput("Not enough arguments!", CommandStatus.Error);
            if (!bool.TryParse(parameters[0], out bool newValue)) return new CommandOutput($"'{parameters[0]}' is not a valid boolean!", CommandStatus.Error);
            
            PlayerController player = Players.GetCurrentPlayer();
            player.SetInvincibility(newValue);
            return $"Successfully set invincibility to {newValue}";
        }

        public string GetDescription()
        {
            return "Use /invincible {state} to set invincibility for the player.";
        }

        public string[] GetTriggerNames()
        {
            return new[] { "invincible" };
        }
    }
}