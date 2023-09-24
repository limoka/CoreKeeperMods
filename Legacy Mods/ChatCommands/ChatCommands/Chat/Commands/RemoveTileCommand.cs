using System;
using System.Linq;
using CoreLib;
using CoreLib.Submodules.ChatCommands;
using HarmonyLib;
using PugTilemap;
using Unity.Mathematics;
using UnityEngine;

namespace ChatCommands.Chat.Commands
{
    public class RemoveTileCommand : IChatCommandHandler
    {
        public CommandOutput Execute(string[] parameters)
        {
            PlayerController player = GameManagers.GetMainManager().player;
            if (player == null) return new CommandOutput("Internal error", Color.red);

            if (parameters.Length < 3)
            {
                return new CommandOutput("Not enough parameters, please check usage via /help removeTile!", Color.red);
            }

            int2 pos = CommandUtil.ParsePos(parameters, parameters.Length - 1, player, out CommandOutput? commandOutput);
            if (commandOutput != null)
                return commandOutput.Value;

            int leftArgs = parameters.Length - 2;

            if (leftArgs == 1)
            {
                if (Enum.TryParse(parameters[0], true, out TileType tileType))
                {
                    return RemoveTileAt(pos, tileType, player);
                }
            }

            string fullName = parameters.Take(leftArgs).Join(null, " ");
            CommandOutput output = CommandUtil.ParseItemName(fullName, out ObjectID objectID);
            if (objectID == ObjectID.None)
                return output;
            
            TileCD tileData = PlaceTileCommand.GetTileData(objectID, out CommandOutput? commandOutput2);
            if (commandOutput2 != null)
                return commandOutput2.Value;

            return RemoveTileAt(pos, tileData.tileType, player);
        }

        public static CommandOutput RemoveTileAt(int2 pos, TileType tileType, PlayerController player)
        {
            if (Manager.multiMap.GetTileTypeAt(pos, tileType, out TileInfo tileInfo))
            {
                player.pugMapSystem.RemoveTileOverride(pos, tileType);
                player.playerCommandSystem.RemoveTile(pos, tileInfo.tileset, tileType);
                return "Tile removed";
            }

            return "Tile not found";
        }

        public string GetDescription()
        {
            return "Use /removeTile to removes tiles from the world" +
                   "\n /removeTile {tileType} {x} {y}" +
                   "\n /removeTile {itemName} {x} {y}" +
                   "\nPosition can be relative, if '~' is added to beginning" +
                   "\nTileType defines the kind of a tile: ground, wall, rail, etc.";
        }

        public string[] GetTriggerNames()
        {
            return new[] { "removeTile" };
        }
    }
}