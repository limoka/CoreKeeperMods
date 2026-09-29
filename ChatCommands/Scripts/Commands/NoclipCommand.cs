using System;
using CoreLib.Submodule.Command.Data;
using CoreLib.Submodule.Command.Interface;
using CoreLib.Submodule.Command.Util;
using CoreLib.Util;
using PlayerState;
using PugMod;
using Unity.Entities;

namespace ChatCommands.Chat.Commands
{
    public class NoclipCommand : IServerCommandHandler
    {
        public CommandOutput Execute(string[] parameters, Entity sender)
        {
            Entity player = sender.GetPlayerEntity();
            World serverWorld = API.Server.World;
            EntityManager entityManager = serverWorld.EntityManager;

            var playerState = entityManager.GetComponentData<PlayerStateCD>(player);
            var noclipActive = playerState.HasAnyState(PlayerStateEnum.NoClip);

            switch (parameters.Length)
            {
                case 0:
                    noclipActive = !noclipActive;
                    break;
                case 1:
                    if (bool.TryParse(parameters[0], out bool value)) noclipActive = value;

                    break;
                case 2 when parameters[0].Equals("speed"):
                    if (!float.TryParse(parameters[1], out float multiplier))
                        return new CommandOutput($"{parameters[1]} is not a valid number!", CommandStatus.Error);

                    multiplier = Math.Clamp(multiplier, 0.5f, 10f);

                    var movement = entityManager.GetComponentData<PlayerMovementCD>(player);
                    movement.noClipMovementSpeedMultipler = 12.5f * multiplier;
                    entityManager.SetComponentData(player, movement);

                    return $"noclip speed multiplier now is {multiplier}";
            }


            playerState.SetNextState(noclipActive ? PlayerStateEnum.NoClip : PlayerStateEnum.Walk);
            entityManager.SetComponentData(player, playerState);

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