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
    public class PlaceTileAreaCommand : IServerCommandHandler
    {
        public CommandOutput Execute(string[] parameters, Entity sender)
        {
            Entity player = sender.GetPlayerEntity();
            if (player == Entity.Null) return new CommandOutput("Internal error", CommandStatus.Error);

            if (parameters.Length < 5) return new CommandOutput("Not enough parameters, please check usage via /help placeTileArea!", CommandStatus.Error);

            var translation = API.Server.World.EntityManager.GetComponentData<Translation>(player);
            
            int2 startPos = CommandUtil.ParsePos(parameters, parameters.Length - 3, translation.Value, out var commandOutput);
            if (commandOutput != null)
                return commandOutput.Value.AppendAtStart("Start pos");

            int2 endPos = CommandUtil.ParsePos(parameters, parameters.Length - 1, translation.Value, out var commandOutput1);
            if (commandOutput1 != null)
                return commandOutput1.Value.AppendAtStart("End pos");

            int leftArgs = parameters.Length - 4;

            if (leftArgs == 2)
                if (Enum.TryParse(parameters[0], true, out Tileset tileset) &&
                    Enum.TryParse(parameters[1], true, out TileType tileType))
                {
                    PlaceTileArea((int)tileset, tileType, startPos, endPos);
                    return "Tile area placed.";
                }

            string fullName = parameters.Take(leftArgs).Join(null, " ");
            CommandOutput output = CommandUtil.ParseItemName(fullName, out ObjectID objectID);
            if (objectID == ObjectID.None)
                return output;

            TileCD tileData = PlaceTileCommand.GetTileData(objectID, out var commandOutput2);
            if (commandOutput2 != null)
                return commandOutput2.Value;

            return PlaceTileArea(tileData.tileset, tileData.tileType, startPos, endPos);
        }

        public string GetDescription()
        {
            return "Use /placeTileArea to place tile area into world" +
                   "\n /placeTileArea {tileset} {tileType} {sX} {sY} {eX} {eY}" +
                   "\n /placeTileArea {itemName} {sX} {sY} {eX} {eY}" +
                   "\nFirst pair is rect start position, second is rect end position" +
                   "\nPosition can be relative, if '~' is added to beginning" +
                   "\nTileset defines set of tiles (Most of the time its a biome)" +
                   "\nTileType defines the kind of a tile: ground, wall, rail, etc.";
        }

        public string[] GetTriggerNames()
        {
            return new[] { "placeTileArea" };
        }

        public static CommandOutput PlaceTileArea(int tileset, TileType tileType, int2 startPos, int2 endPos)
        {
            int2 newStart = math.min(startPos, endPos);
            int2 newEnd = math.max(startPos, endPos);

            for (int x = newStart.x; x <= newEnd.x; x++)
            for (int y = newStart.y; y <= newEnd.y; y++)
            {
                CommandOutput result = PlaceTileCommand.TryPlaceTile(tileset, tileType, new int2(x, y));
                if (result.status == CommandStatus.Error) return result;
            }

            return "Tile area placed.";
        }
    }
}