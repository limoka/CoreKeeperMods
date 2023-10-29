using CoreLib.Commands;
using CoreLib.Commands.Communication;
using PugMod;
using Unity.Entities;

namespace ChatCommands.Chat.Commands
{
    public class ClearInvCommandHandler : IServerCommandHandler
    {
        public CommandOutput Execute(string[] parameters, Entity sender)
        {
            Entity playerEntity = sender.GetPlayerEntity();
            if (!EntityUtility.EntityExists(playerEntity, API.Server.World))
                return new CommandOutput("Internal error: player not found!", CommandStatus.Error);

            EntityManager entityManager = API.Server.World.EntityManager;
            var itemsBuffer = entityManager.GetBuffer<ContainedObjectsBuffer>(playerEntity);
            for (int i = 0; i < itemsBuffer.Length; i++)
            {
                if (itemsBuffer[i].objectID == ObjectID.None) continue;
                itemsBuffer[i] = default;
            }

            return "Successfully cleared inventory";
        }

        public string GetDescription()
        {
            return "Use /clear to clear player inventory.";
        }

        public string[] GetTriggerNames()
        {
            return new[] { "clearInv", "clearInventory" };
        }
    }
}