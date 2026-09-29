using CoreLib.Submodule.Command.Data;
using CoreLib.Submodule.Command.Interface;
using CoreLib.Submodule.Command.Util;
using Inventory;
using PugMod;
using Unity.Entities;
using Unity.Transforms;

namespace ChatCommands.Chat.Commands
{
    public class CopyCommandHandler : IServerCommandHandler
    {
        public string GetDescription()
        {
            return "Use /copy to give you some copy of currently held item. \n"
                   + "/copy [count]\n"
                   + "The count parameter defaults to 1.";
        }

        public string[] GetTriggerNames()
        {
            return new[] { "copy" };
        }

        public CommandOutput Execute(string[] parameters, Entity sender)
        {
            int count;
            if (parameters.Length == 0)
            {
                count = 1;
            }
            else if (!int.TryParse(parameters[0], out count) && count <= 0)
            {
                return new CommandOutput("Invalid stack count", CommandStatus.Error);
            }

            Entity playerEntity = sender.GetPlayerEntity();
            var querySystem = Manager.main.player.querySystem;
            var equippedObjectCD = querySystem.GetSingleton<EquippedObjectCD>();
            var containedObject = equippedObjectCD.containedObject;
            if (containedObject.objectData.objectID == ObjectID.None)
            {
                return new CommandOutput("No item in hand", CommandStatus.Error);
            }

            var objectInfo = PugDatabase.GetObjectInfo(
                containedObject.objectData.objectID,
                containedObject.objectData.variation
            );

            var entityManager = API.Server.World.EntityManager;
            var database = entityManager.GetDatabase();
            var position = entityManager.GetComponentData<LocalTransform>(playerEntity).Position;
            if (objectInfo.isStackable)
            {
                var item = new ContainedObjectsBuffer
                {
                    objectData = new ObjectDataCD
                    {
                        objectID = containedObject.objectData.objectID,
                        amount = count,
                        variation = containedObject.objectData.variation
                    }
                };
                EntityUtility.DropNewEntity(
                    API.Server.World,
                    item,
                    position,
                    database,
                    playerEntity
                );
            }
            else
            {
                var item = new ContainedObjectsBuffer
                {
                    objectData = new ObjectDataCD
                    {
                        objectID = containedObject.objectData.objectID,
                        amount = 1,
                        variation = containedObject.objectData.variation
                    }
                };
                for (var j = 0; j < count; j++)
                    EntityUtility.DropNewEntity(
                        API.Server.World,
                        item,
                        position,
                        database,
                        playerEntity
                    );
            }

            return $"Successfully added {count} {containedObject.objectData.objectID}, variation {containedObject.objectData.variation}";
        }
    }
}
