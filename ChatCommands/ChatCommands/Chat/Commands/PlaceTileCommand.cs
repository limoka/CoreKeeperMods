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
    public class PlaceTileCommand : IChatCommandHandler
    {
        public CommandOutput Execute(string[] parameters)
        {
            PlayerController player = GameManagers.GetMainManager().player;
            if (player == null) return new CommandOutput("Internal error", Color.red);

            if (parameters.Length < 3)
            {
                return new CommandOutput("Not enough parameters, please check usage!", Color.red);
            }

            int2 pos = ParsePos(parameters, parameters.Length - 1, player, out CommandOutput? commandOutput);
            if (commandOutput != null)
                return commandOutput.Value;

            int leftArgs = parameters.Length - 2;

            if (leftArgs == 2)
            {
                if (Enum.TryParse(parameters[0], true, out Tileset tileset) && 
                    Enum.TryParse(parameters[1], true, out TileType tileType))
                {
                    player.playerCommandSystem.AddTile(pos, (int)tileset, tileType);
                    return "Tile placed.";
                }
            }

            string fullName = parameters.Take(leftArgs).Join(null, " ");
            CommandOutput output = GiveCommandHandler.ParseItemName(fullName, out ObjectID objectID);
            if (objectID == ObjectID.None)
                return output;

            return PlaceObjectID(objectID, player, pos);
        }

        public static int2 ParsePos(string[] parameters, int startIndex, PlayerController player, out CommandOutput? commandOutput)
        {
            string xPosStr = parameters[startIndex - 1];
            string zPosStr = parameters[startIndex];

            int xPos;
            int zPos;

            int2 playerPos = player.WorldPosition.RoundToInt2();
            
            try
            {
                xPos = ParsePosAxis(xPosStr, -playerPos.x);
                zPos = ParsePosAxis(zPosStr, -playerPos.y);
            }
            catch (Exception)
            {
                commandOutput = new CommandOutput("Failed to parse position parameters!", Color.red);
                return int2.zero;
            }

            commandOutput = null;
            return new int2(xPos, zPos);
        }
        
        private static int ParsePosAxis(string posText, int playerPos)
        {
            if (posText[0] == '~')
            {
                return int.Parse(posText[1..]);
            }

            return playerPos + int.Parse(posText);
        }
        
        public static TileCD GetTileData(ObjectID objectID, out CommandOutput? commandOutput)
        {
            if (PugDatabase.HasComponent<TileCD>(objectID))
            {
                commandOutput = null;
                return PugDatabase.GetComponent<TileCD>(objectID);
            }

            commandOutput = new CommandOutput("This object is not a tile!", Color.red);
            return default;
        }

        public static CommandOutput PlaceObjectID(ObjectID objectID, PlayerController player, int2 pos)
        {
            TileCD tileData = GetTileData(objectID, out CommandOutput? commandOutput2);
            if (commandOutput2 != null)
                return commandOutput2.Value;
            
            player.playerCommandSystem.AddTile(pos, tileData.tileset, tileData.tileType);
            return "Tile placed.";
        }

        public string GetDescription()
        {
            return "Use /placeTile to place tiles into world" +
                   "\n /placeTile {tileset} {tileType} {x} {y}" +
                   "\n /placeTile {itemName} {x} {y}" +
                   "\nPosition can be relative, if '~' is added to beginning" +
                   "\nTileset defines set of tiles (Most of the time its a biome)" +
                   "\nTileType defines the kind of a tile: ground, wall, rail, etc.";
        }

        public string[] GetTriggerNames()
        {
            return new[] { "placeTile" };
        }
    }
}