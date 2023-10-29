using System;
using System.Linq;
using CoreLib.Commands;
using CoreLib.Commands.Communication;
using HarmonyLib;
using PugMod;
using PugTilemap;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace ChatCommands.Chat.Commands
{
    public class RemoveTileCommand : IServerCommandHandler
    {
        public CommandOutput Execute(string[] parameters, Entity sender)
        {
            Entity player = sender.GetPlayerEntity();
            if (player == Entity.Null) return new CommandOutput("Internal error", CommandStatus.Error);

            if (parameters.Length < 3) return new CommandOutput("Not enough parameters, please check usage via /help removeTile!", CommandStatus.Error);

            var translation = API.Server.World.EntityManager.GetComponentData<Translation>(player);
            
            int2 pos = CommandUtil.ParsePos(parameters, parameters.Length - 1, translation.Value, out var commandOutput);
            if (commandOutput != null)
                return commandOutput.Value;

            int leftArgs = parameters.Length - 2;

            if (leftArgs == 1)
                if (Enum.TryParse(parameters[0], true, out TileType tileType))
                {
                    RemoveTile(pos, tileType);
                    return "Tile removed";
                }

            string fullName = parameters.Take(leftArgs).Join(null, " ");
            CommandOutput output = CommandUtil.ParseItemName(fullName, out ObjectID objectID);
            if (objectID == ObjectID.None)
                return output;

            TileCD tileData = PlaceTileCommand.GetTileData(objectID, out var commandOutput2);
            if (commandOutput2 != null)
                return commandOutput2.Value;

            RemoveTile(pos, tileData.tileType);
            return "Tile removed";
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

        public static void RemoveTile(int2 pos, TileType tileType)
        {
            EntityManager entityManager = API.Server.World.EntityManager;
            Entity tileUpdateEntity = entityManager.CreateEntityQuery(typeof(TileUpdateBuffer)).GetSingletonEntity();
            var updateBuffer = entityManager.GetBuffer<TileUpdateBuffer>(tileUpdateEntity);

            updateBuffer.Add(new TileUpdateBuffer
            {
                command = TileUpdateBuffer.Command.Remove,
                position = pos,
                tile = new TileCD
                {
                    tileType = tileType
                }
            });

            var tiles = PugTile.Get(pos, Allocator.Temp, entityManager.World);
            var neededTiles = new NativeList<TileType>(4, Allocator.Temp);
            for (int k = 0; k < tiles.Length; k++)
            {
                neededTiles.Clear();
                tiles[k].tileType.GetNeededTile(ref neededTiles);
                for (int l = 0; l < neededTiles.Length; l++)
                    if (neededTiles[l] == tileType)
                    {
                        updateBuffer.Add(new TileUpdateBuffer
                        {
                            command = TileUpdateBuffer.Command.Remove,
                            position = pos,
                            tile = new TileCD
                            {
                                tileType = tiles[k].tileType
                            }
                        });
                        break;
                    }
            }

            if (tileType is TileType.ground or TileType.water)
                updateBuffer.Add(new TileUpdateBuffer
                {
                    command = TileUpdateBuffer.Command.Add,
                    position = pos,
                    tile = new TileCD
                    {
                        tileType = TileType.pit
                    }
                });

            neededTiles.Dispose();
            tiles.Dispose();
        }
    }
}