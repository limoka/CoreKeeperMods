using System;
using System.Linq;
using ChatCommands.Util;
using CoreLib;
using CoreLib.Submodules.ChatCommands;
using HarmonyLib;
using PugTilemap;
using Unity.Mathematics;
using UnityEngine;

namespace ChatCommands.Chat.Commands
{
    public class PlaceTileAreaCommand : IChatCommandHandler
    {
        public CommandOutput Execute(string[] parameters)
        {
            PlayerController player = GameManagers.GetMainManager().player;
            if (player == null) return new CommandOutput("Internal error", Color.red);
            
            if (parameters.Length < 5)
            {
                return new CommandOutput("Not enough parameters, please check usage via /help placeTileArea!", Color.red);
            }

            int2 startPos = CommandUtil.ParsePos(parameters, parameters.Length - 3, player, out CommandOutput? commandOutput);
            if (commandOutput != null)
                return commandOutput.Value.AppendAtStart("Start pos");
            
            int2 endPos = CommandUtil.ParsePos(parameters, parameters.Length - 1, player, out CommandOutput? commandOutput1);
            if (commandOutput1 != null)
                return commandOutput1.Value.AppendAtStart("End pos");

            int leftArgs = parameters.Length - 4;

            if (leftArgs == 2)
            {
                if (Enum.TryParse(parameters[0], true, out Tileset tileset) && 
                    Enum.TryParse(parameters[1], true, out TileType tileType))
                {
                    PlaceTileArea((int)tileset, tileType, player, startPos, endPos);
                    return "Tile area placed.";
                }
            }

            string fullName = parameters.Take(leftArgs).Join(null, " ");
            CommandOutput output = CommandUtil.ParseItemName(fullName, out ObjectID objectID);
            if (objectID == ObjectID.None)
                return output;

            TileCD tileData = PlaceTileCommand.GetTileData(objectID, out CommandOutput? commandOutput2);
            if (commandOutput2 != null)
                return commandOutput2.Value;
            
            PlaceTileArea(tileData.tileset, tileData.tileType, player, startPos, endPos);
            return "Tile area placed.";
        }

        public static void PlaceTileArea(int tileset, TileType tileType, PlayerController player, int2 startPos, int2 endPos)
        {
            int2 newStart = math.min(startPos, endPos);
            int2 newEnd = math.max(startPos, endPos);

            for (int x = newStart.x; x <= newEnd.x; x++)
            {
                for (int y = newStart.y; y <= newEnd.y; y++)
                {
                    player.playerCommandSystem.AddTile(new int2(x, y), tileset, tileType);
                }
            }
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
    }
}