using System;
using CoreLib.Commands;
using CoreLib.Commands.Communication;
using CoreLib.Util;
using PlayerState;

namespace ChatCommands.Chat.Commands
{
    public class NoclipCommand : IClientCommandHandler
    {
        public bool noclipActive;

        public CommandOutput Execute(string[] parameters)
        {
            PlayerController player = Players.GetCurrentPlayer();

            noclipActive = EntityUtility.GetComponentData<PlayerStateCD>(player.entity, player.world).HasAnyState(PlayerStateEnum.NoClip);

            switch (parameters.Length)
            {
                case 0:
                    noclipActive = !noclipActive;
                    break;
                case 1:
                    if (bool.TryParse(parameters[0], out bool value)) noclipActive = value;

                    break;
                case 2 when parameters[0].Equals("speed"):
                    if (float.TryParse(parameters[1], out float multiplier))
                    {
                        multiplier = Math.Clamp(multiplier, 0.5f, 10f);
                        player.noClipMovementSpeedMultipler = 6.25f * multiplier;
                        return $"noclip speed multiplier now is {multiplier}";
                    }

                    return new CommandOutput($"{parameters[1]} is not a valid number!", CommandStatus.Error);
            }

            if (noclipActive)
                player.playerCommandSystem.SetPlayerState(player.entity, PlayerStateEnum.NoClip);
            else
                player.playerCommandSystem.SetPlayerState(player.entity, PlayerStateEnum.Walk);

            return $"Noclip is {(noclipActive ? "active" : "inactive")}";
        }

        public string GetDescription()
        {
            return "Use /noclip to move freely without physical limitations!\n" +
                   "/noclip speed {multilplier} - set noclip speed multiplier";
        }

        public string[] GetTriggerNames()
        {
            return new[] { "noclip" };
        }
    }
}