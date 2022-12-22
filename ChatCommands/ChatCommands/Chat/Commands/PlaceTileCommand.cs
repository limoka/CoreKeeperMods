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

            string xPosStr = parameters[^2];
            string zPosStr = parameters[^1];

            int xPos;
            int zPos;

            try
            {
                xPos = ParsePos(xPosStr, player.pugMapPosX);
                zPos = ParsePos(zPosStr, player.pugMapPosZ);
            }
            catch (Exception)
            {
                return new CommandOutput("Failed to parse position parameters!", Color.red);
            }

            int leftArgs = parameters.Length - 2;

            if (leftArgs == 2)
            {
                if (int.TryParse(parameters[0], out int tileset) && 
                    Enum.TryParse(parameters[1], out TileType tileType))
                {
                    player.playerCommandSystem.AddTile(new int2(xPos, zPos), tileset, tileType);
                    return "Tile placed.";
                }
            }

            string fullName = parameters.Take(leftArgs).Join(null, " ");
            CommandOutput output = GiveCommandHandler.ParseItemName(fullName, out ObjectID objectID);
            if (objectID == ObjectID.None)
                return output;

            return PlaceObjectID(objectID, player, xPos, zPos);
        }

        private static CommandOutput PlaceObjectID(ObjectID objectID, PlayerController player, int xPos, int zPos)
        {
            if (PugDatabase.HasComponent<TileCD>(objectID))
            {
                TileCD tileCd = PugDatabase.GetComponent<TileCD>(objectID);

                player.playerCommandSystem.AddTile(new int2(xPos, zPos), tileCd.tileset, tileCd.tileType);
                return "Tile placed.";
            }

            return new CommandOutput("This object is not a tile!", Color.red);
        }

        private int ParsePos(string posText, int playerPos)
        {
            if (posText[0] == '~')
            {
                int value = int.Parse(posText[1..]);
                return playerPos + value;
            }

            return int.Parse(posText[1..]);
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