using CoreLib.Submodule.Command.Data;
using CoreLib.Submodule.Command.Interface;
using CoreLib.Submodule.Command.Util;
using Unity.Entities;

namespace ChatCommands.Chat.Commands
{
    public class InfoCommandHandler : IServerCommandHandler
    {
        public string GetDescription()
        {
            return "Use /info to get ObjectID and Variation of currently held item.";
        }

        public string[] GetTriggerNames()
        {
            return new[] { "info" };
        }

        public CommandOutput Execute(string[] parameters, Entity sender)
        {
            var querySystem = Manager.main.player.querySystem;
            var equippedObjectCD = querySystem.GetSingleton<EquippedObjectCD>();
            var containedObject = equippedObjectCD.containedObject;
            if (containedObject.objectData.objectID == ObjectID.None)
            {
                return new CommandOutput("No item in hand", CommandStatus.Error);
            }
            
            return "ObjectID: "
                   + containedObject.objectData.objectID
                   + ", Variation: "
                   + containedObject.objectData.variation;
        }
    }
}
