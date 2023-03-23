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
    public class RemoveTileAreaCommand : IChatCommandHandler
    {
        public CommandOutput Execute(string[] parameters)
        {
            PlayerController player = GameManagers.GetMainManager().player;
            if (player == null) return new CommandOutput("Internal error", Color.red);
            
            if (parameters.Length < 4)
            {
                return new CommandOutput("Not enough parameters, please check usage via /removeTileArea!", Color.red);
            }

            int2 startPos = CommandUtil.ParsePos(parameters, parameters.Length - 3, player, out CommandOutput? commandOutput);
            if (commandOutput != null)
                return commandOutput.Value.AppendAtStart("Start pos");
            
            int2 endPos = CommandUtil.ParsePos(parameters, parameters.Length - 1, player, out CommandOutput? commandOutput1);
            if (commandOutput1 != null)
                return commandOutput1.Value.AppendAtStart("End pos");

            int leftArgs = parameters.Length - 4;

            if (leftArgs == 1)
            {
                if (Enum.TryParse(parameters[0], true, out TileType tileType))
                {
                    RemoveTileArea(tileType, player, startPos, endPos);
                    return "Tile area removed";
                }
            }

            string fullName = parameters.Take(leftArgs).Join(null, " ");
            CommandOutput output = CommandUtil.ParseItemName(fullName, out ObjectID objectID);
            if (objectID == ObjectID.None)
                return output;

            TileCD tileData = PlaceTileCommand.GetTileData(objectID, out CommandOutput? commandOutput2);
            if (commandOutput2 != null)
                return commandOutput2.Value;
            
            RemoveTileArea(tileData.tileType, player, startPos, endPos);
            return "Tile area removed";
        }
        
        public static void RemoveTileArea(TileType tileType, PlayerController player, int2 startPos, int2 endPos)
        {
            int2 newStart = math.min(startPos, endPos);
            int2 newEnd = math.max(startPos, endPos);

            for (int x = newStart.x; x <= newEnd.x; x++)
            {
                for (int y = newStart.y; y <= newEnd.y; y++)
                {
                    RemoveTileCommand.RemoveTileAt(new int2(x, y), tileType, player);
                }
            }
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
    }
}