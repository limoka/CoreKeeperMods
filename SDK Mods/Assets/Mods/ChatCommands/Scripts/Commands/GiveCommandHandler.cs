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
    public class GiveCommandHandler : IServerCommandHandler
    {
        public CommandOutput Execute(string[] parameters, Entity sender)
        {
            if (parameters.Length == 0) return new CommandOutput("Please enter item name", CommandStatus.Error);
            Entity playerEntity = sender.GetPlayerEntity();

            if (parameters.Length > 3 && parameters[0] == "food") return GiveFood(playerEntity, parameters);

            return NormalGive(playerEntity, parameters);
        }

        public string GetDescription()
        {
            return
                "Use /give to give yourself any item. \n" +
                "/give {itemName} [count] [variation]\n" +
                "The count parameter defaults to 1. Variation defaults to 0\n" +
                "/give food {item1} + {item2} [count] Add any food. First item is used as a base ingredient.";
        }

        public string[] GetTriggerNames()
        {
            return new[] { "give" };
        }

        private CommandOutput GiveFood(Entity playerEntity, string[] parameters)
        {
            int count = 1;
            int nameArgCount = parameters.Length;
            if (nameArgCount > 1 && int.TryParse(parameters[^1], out int val))
            {
                count = val;
                nameArgCount--;
            }

            string args = parameters.Take(nameArgCount).TakeLast(nameArgCount - 1).Join(null, " ");
            if (!args.Contains('+')) return new CommandOutput("Plus sign '+' is missing. Please use it to separate item names!", CommandStatus.Error);

            string[] itemName = args.Split(" + ");
            if (itemName.Length > 2) return new CommandOutput("Too many items in input. Please provide only two items", CommandStatus.Error);

            CommandOutput output1 = CommandUtil.ParseItemName(itemName[0], out ObjectID item1);
            if (item1 == ObjectID.None)
                return output1;
            CommandOutput output2 = CommandUtil.ParseItemName(itemName[1], out ObjectID item2);
            if (item2 == ObjectID.None)
                return output2;

            if (!PugDatabase.HasComponent<CookingIngredientCD>(item1)) return new CommandOutput($"{item1} is not a cooking ingredient!", CommandStatus.Error);

            if (!PugDatabase.HasComponent<CookingIngredientCD>(item2)) return new CommandOutput($"{item2} is not a cooking ingredient!", CommandStatus.Error);

            CookingIngredientCD ingredient1 = PugDatabase.GetComponent<CookingIngredientCD>(item1);

            AddToInventory(playerEntity, ingredient1.turnsIntoFood, count, CookedFoodCD.GetFoodVariation(item1, item2));
            return $"Successfully added {count} {ingredient1.turnsIntoFood}";
        }

        private CommandOutput NormalGive(Entity playerEntity, string[] parameters)
        {
            int count = 1;
            int variation = 0;
            int nameArgCount = parameters.Length;
            if (nameArgCount > 1 && int.TryParse(parameters[^1], out int val))
            {
                count = val;
                nameArgCount--;
            }

            if (nameArgCount > 1 && int.TryParse(parameters[^2], out val))
            {
                variation = count;
                count = val;
                nameArgCount--;
            }

            string fullName = parameters.Take(nameArgCount).Join(null, " ");
            fullName = fullName.ToLower();

            CommandOutput output = CommandUtil.ParseItemName(fullName, out ObjectID objectID);
            if (objectID == ObjectID.None)
                return output;

            AddToInventory(playerEntity, objectID, count, variation);
            return $"Successfully added {count} {objectID}, variation {variation}";
        }

        private void AddToInventory(Entity playerEntity, ObjectID objId, int amount, int variation)
        {
            EntityManager entityManager = API.Server.World.EntityManager;
            var database = entityManager.GetDatabase();
            ref PugDatabase.EntityObjectInfo objectInfo = ref PugDatabase.GetEntityObjectInfo(objId, database, variation);
            float3 position = entityManager.GetComponentData<Translation>(playerEntity).Value;

            if (objectInfo.isStackable)
            {
                ContainedObjectsBuffer item = new ContainedObjectsBuffer
                {
                    objectData = new ObjectDataCD
                    {
                        objectID = objId,
                        amount = amount,
                        variation = variation
                    }
                };
                EntityUtility.DropNewEntity(API.Server.World, item, position, database, playerEntity);
                return;
            }

            ContainedObjectsBuffer item2 = new ContainedObjectsBuffer
            {
                objectData = new ObjectDataCD
                {
                    objectID = objId,
                    amount = objectInfo.initialAmount,
                    variation = variation
                }
            };
            for (int j = 0; j < amount; j++) EntityUtility.DropNewEntity(API.Server.World, item2, position, database, playerEntity);
        }
    }
}