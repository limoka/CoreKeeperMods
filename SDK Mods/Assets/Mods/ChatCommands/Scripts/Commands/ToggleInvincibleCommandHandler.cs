using CoreLib.Commands;
using CoreLib.Util;
using CoreLib.Util.Extensions;

namespace ChatCommands.Chat.Commands
{
    public class ToggleInvincibleCommandHandler : IClientCommandHandler
    {
        public CommandOutput Execute(string[] parameters)
        {
            PlayerController player = Players.GetCurrentPlayer();
            bool newValue = !player.GetValue<bool>("invincible");
            player.SetInvincibility(newValue);
            return $"Successfully set invincibility to {newValue}";
        }

        public string GetDescription()
        {
            return "Use /invincible to toggle invincibility for the player.";
        }

        public string[] GetTriggerNames()
        {
            return new[] { "invincible" };
        }
    }
}