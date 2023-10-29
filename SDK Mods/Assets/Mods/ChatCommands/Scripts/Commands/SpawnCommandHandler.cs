using System.Linq;
using CoreLib.Commands;
using CoreLib.Commands.Communication;
using HarmonyLib;
using PugMod;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace ChatCommands.Chat.Commands
{
    public class SpawnCommandHandler : IServerCommandHandler
    {
        public CommandOutput Execute(string[] parameters, Entity sender)
        {
            Entity player = sender.GetPlayerEntity();
            if (player == Entity.Null) return new CommandOutput("Internal error: player not found", CommandStatus.Error);

            int variation = 0;

            int numberParams = GetNumberParams(parameters);
            int nameArgCount = parameters.Length;

            if (numberParams < 2 || nameArgCount < 3) return new CommandOutput("Not enough arguments! Check usage via /help spawn.", CommandStatus.Error);

            if (numberParams > 2)
            {
                if (!int.TryParse(parameters[nameArgCount - 1], out variation))
                    return new CommandOutput($"{parameters[nameArgCount - 1]} is not a valid number!");
                nameArgCount--;
            }

            var translation = API.Server.World.EntityManager.GetComponentData<Translation>(player);
            
            int2 pos = CommandUtil.ParsePos(parameters, nameArgCount - 1, translation.Value, out var commandOutput1);
            if (commandOutput1 != null)
                return commandOutput1.Value;

            nameArgCount -= 2;

            string fullName = parameters.Take(nameArgCount).Join(null, " ");

            CommandOutput output = CommandUtil.ParseItemName(fullName, out ObjectID objId);
            if (objId == ObjectID.None)
                return output;

            return SpawnID(player, objId, variation, pos);
        }

        public string GetDescription()
        {
            return "Use /spawn any entity into the world\n" +
                   "/spawn {object name} {x} {y} [variation]\n" +
                   "x and y is target world position. Use '~' to set position relative to you.";
        }

        public string[] GetTriggerNames()
        {
            return new[] { "spawn" };
        }

        private static int GetNumberParams(string[] parameters)
        {
            int numberParams = 0;

            for (int i = parameters.Length - 1; i >= 0; i--)
            {
                string value = string.Join("", parameters[i].Split('~'));
                if (int.TryParse(value, out int _))
                {
                    numberParams++;
                    continue;
                }

                break;
            }

            return numberParams;
        }

        private static CommandOutput SpawnID(Entity player, ObjectID objId, int variation, int2 pos)
        {
            if (objId == ObjectID.Player)
                return new CommandOutput("I'm going to pretend you did not ask for this. You don't know what you are doing!", CommandStatus.Warning);

            ObjectInfo info = PugDatabase.GetObjectInfo(objId);
            bool hasSpawnablePrefab = info.prefabInfos[0].prefab != null;
            EntityManager entityManager = API.Server.World.EntityManager;
            var database = entityManager.GetDatabase();

            if (!hasSpawnablePrefab)
            {
                ContainedObjectsBuffer containedObjectsBuffer = new ContainedObjectsBuffer
                {
                    objectData = new ObjectDataCD
                    {
                        objectID = info.objectID,
                        amount = 1,
                        variation = variation
                    }
                };
                EntityUtility.DropNewEntity(entityManager.World, containedObjectsBuffer, pos.ToFloat3(), database, player);
                return $"Spawned item {objId}";
            }

            EntityUtility.CreateEntity(entityManager.World, pos.ToFloat3(), objId, 1, database, variation);
            return $"Spawned entity {objId}";
        }
    }
}