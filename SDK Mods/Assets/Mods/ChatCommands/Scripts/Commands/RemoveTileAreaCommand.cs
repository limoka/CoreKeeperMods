using System;
using System.Linq;
using CoreLib.Commands;
using CoreLib.Commands.Communication;
using HarmonyLib;
using PugMod;
using PugTilemap;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace ChatCommands.Chat.Commands
{
    public class RemoveTileAreaCommand : IServerCommandHandler
    {
        public CommandOutput Execute(string[] parameters, Entity sender)
        {
            Entity player = sender.GetPlayerEntity();
            if (player == Entity.Null) return new CommandOutput("Internal error", CommandStatus.Error);

            if (parameters.Length < 4) return new CommandOutput("Not enough parameters, please check usage via /removeTileArea!", CommandStatus.Error);

            var translation = API.Server.World.EntityManager.GetComponentData<Translation>(player);
            
            int2 startPos = CommandUtil.ParsePos(parameters, parameters.Length - 3, translation.Value, out var commandOutput);
            if (commandOutput != null)
                return commandOutput.Value.AppendAtStart("Start pos");

            int2 endPos = CommandUtil.ParsePos(parameters, parameters.Length - 1, translation.Value, out var commandOutput1);
            if (commandOutput1 != null)
                return commandOutput1.Value.AppendAtStart("End pos");

            int leftArgs = parameters.Length - 4;

            if (leftArgs == 1)
                if (Enum.TryParse(parameters[0], true, out TileType tileType))
                {
                    RemoveTileArea(tileType, startPos, endPos);
                    return "Tile area removed";
                }

            string fullName = parameters.Take(leftArgs).Join(null, " ");
            CommandOutput output = CommandUtil.ParseItemName(fullName, out ObjectID objectID);
            if (objectID == ObjectID.None)
                return output;

            TileCD tileData = PlaceTileCommand.GetTileData(objectID, out var commandOutput2);
            if (commandOutput2 != null)
                return commandOutput2.Value;

            RemoveTileArea(tileData.tileType, startPos, endPos);
            return "Tile area removed";
        }

        public string GetDescription()
        {
            return "Use /removeTileArea to remove tile area from the world" +
                   "\n /removeTileArea {tileType} {sX} {sY} {eX} {eY}" +
                   "\n /removeTileArea {itemName} {sX} {sY} {eX} {eY}" +
                   "\nFirst pair is rect start position, second is rect end position" +
                   "\nPosition can be relative, if '~' is added to beginning" +
                   "\nTileType defines the kind of a tile: ground, wall, rail, etc.";
        }

        public string[] GetTriggerNames()
        {
            return new[] { "removeTileArea" };
        }

        public static void RemoveTileArea(TileType tileType, int2 startPos, int2 endPos)
        {
            int2 newStart = math.min(startPos, endPos);
            int2 newEnd = math.max(startPos, endPos);

            for (int x = newStart.x; x <= newEnd.x; x++)
            for (int y = newStart.y; y <= newEnd.y; y++)
                RemoveTileCommand.RemoveTile(new int2(x, y), tileType);
        }
    }
}