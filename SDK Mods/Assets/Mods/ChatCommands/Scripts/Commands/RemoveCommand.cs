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

            var removeAll = parameters.Any(s => s is "all" or "-all");
            var fullName = string.Join(" ", parameters.Where(s => s is not ("all" or "-all")));
            
            CommandOutput output = CommandUtil.ParseItemName(fullName, out ObjectID target);
            if (target == ObjectID.None)
                return output;
            
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

            if (removeAll)
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
                "/remove {object ID} [-all] - Remove closest (in the vicinity) entity with matching ID. If all flag is set, all matching entites will be removed.\n" +
                "\nExample:" +
                "\n/remove CopperOreBoulder - Remove closest (in the vicinity) copper ore boulder";
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