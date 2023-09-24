using System;
using System.Collections.Generic;
using System.Linq;
using CoreLib.Submodules.ChatCommands;
using CoreLib.Util.Extensions;
using PlayerCommand;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace ChatCommands.Chat.Commands
{
    public class RemoveCommand : IChatCommandHandler
    {
        public CommandOutput Execute(string[] parameters)
        {
            PlayerController player = Manager._instance.player;
            if (player == null) return "null player!";

            if (parameters.Length == 0)
            {
                return new CommandOutput("Please enter target ID", Color.red);
            }

            if (Enum.TryParse(parameters[0], out ObjectID target))
            {
                ClientSystem commandSystem = player.playerCommandSystem;
                EntityManager entityManager = player.world.EntityManager;
                EntityQuery query;

                if (parameters.Length > 1 && parameters.Contains("slow"))
                {
                     query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<ObjectDataCD>(), ComponentType.ReadOnly<Translation>());
                }
                else
                {
                    query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<IndestructibleCD>());
                }

                var array = query.ToEntityArray(Allocator.Temp);
                List<Entity> matches = new List<Entity>();

                foreach (Entity entity in array)
                {
                    ObjectDataCD objectData = entityManager.GetComponentData<ObjectDataCD>(entity);
                    if (objectData.objectID == target)
                    {
                        matches.Add(entity);
                    }
                }

                if (matches.Count == 0)
                {
                    return new CommandOutput("Found no such entity!", Color.red);
                }

                if (parameters.Length > 1 && parameters.Contains("all"))
                {
                    foreach (Entity entity in matches)
                    {
                        commandSystem.DestroyEntity(entity, player.entity);
                    }
                }
                else
                {

                    List<Entity> sorted = matches
                        .OrderBy(entity => { return math.length(entityManager.GetComponentData<Translation>(entity).Value - player.WorldPosition.ToFloat3()); })
                        .ToList();

                    commandSystem.DestroyEntity(sorted.First(), player.entity);
                }
            }

            return new CommandOutput($"No such object {parameters[0]}", Color.red);
        }

        public string GetDescription()
        {
            return "/remove {object ID} [all|slow] - Remove closest entity with matching ID. If any flag is set, all matching entites will be removed. If slow flag is set the command will use slower search pattern.";
        }

        public string[] GetTriggerNames()
        {
            return new []{"remove"};
        }
    }
}