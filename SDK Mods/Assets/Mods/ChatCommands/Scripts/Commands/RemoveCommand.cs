using System;
using System.Collections.Generic;
using System.Linq;
using CoreLib.Commands;
using CoreLib.Commands.Communication;
using CoreLib.Util.Extensions;
using PugMod;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace ChatCommands.Chat.Commands
{
    public class RemoveCommand : IServerCommandHandler
    {
        public CommandOutput Execute(string[] parameters, Entity sender)
        {
            Entity player = sender.GetPlayerEntity();
            if (player == Entity.Null) return "null player!";

            if (parameters.Length == 0) return new CommandOutput("Please enter target ID", CommandStatus.Error);

            if (!Enum.TryParse(parameters[0], out ObjectID target)) 
                return new CommandOutput($"No such object {parameters[0]}", CommandStatus.Error);
            
            EntityManager entityManager = API.Server.World.EntityManager;
            EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<ObjectDataCD>(), ComponentType.ReadOnly<LocalTransform>());

            var array = query.ToEntityArray(Allocator.Temp);
            var matches = new List<Entity>();

            foreach (Entity entity in array)
            {
                ObjectDataCD objectData = entityManager.GetComponentData<ObjectDataCD>(entity);
                if (objectData.objectID == target) matches.Add(entity);
            }

            if (matches.Count == 0) return new CommandOutput("Found no such entity!", CommandStatus.Error);

            if (parameters.Length > 1 && parameters.Contains("all"))
            {
                foreach (Entity entity in matches) DestroyEntity(entity);
                return "Destroyed entities successfully!";
            }
            
            var translation = API.Server.World.EntityManager.GetComponentData<LocalTransform>(player);

            var sorted = matches
                .OrderBy(entity => math.length(entityManager.GetComponentData<LocalTransform>(entity).Position - translation.Position))
                .ToList();

            DestroyEntity(sorted.First());
            return "Destroyed entity successfully!";
        }

        public string GetDescription()
        {
            return
                "/remove {object ID} [all|slow] - Remove closest entity with matching ID. If any flag is set, all matching entites will be removed. If slow flag is set the command will use slower search pattern.";
        }

        public string[] GetTriggerNames()
        {
            return new[] { "remove" };
        }

        public static void DestroyEntity(Entity entity)
        {
            EntityManager entityManager = API.Server.World.EntityManager;
            if (!EntityUtility.EntityExists(entity, entityManager.World)) return;

            if (entityManager.HasComponent<HealthCD>(entity))
            {
                HealthCD component3 = entityManager.GetComponentData<HealthCD>(entity);
                component3.health = 0;
                entityManager.SetComponentData(entity, component3);
            }
            else
            {
                entityManager.AddComponent<EntityDestroyedCD>(entity);
            }
        }
    }
}