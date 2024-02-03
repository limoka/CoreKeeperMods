using System;
using System.Linq;
using CoreLib.Commands;
using CoreLib.Commands.Communication;
using HarmonyLib;
using PugMod;
using PugTilemap;
using PugTilemap.Quads;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace ChatCommands.Chat.Commands
{
    public class PlaceTileCommand : IServerCommandHandler
    {
        public CommandOutput Execute(string[] parameters, Entity sender)
        {
            Entity player = sender.GetPlayerEntity();
            if (player == Entity.Null) return new CommandOutput("Internal error", CommandStatus.Error);

            if (parameters.Length < 3) return new CommandOutput("Not enough parameters, please check usage via /help placeTile!", CommandStatus.Error);

            var translation = API.Server.World.EntityManager.GetComponentData<LocalTransform>(player);
            
            int2 pos = CommandUtil.ParsePos(parameters, parameters.Length - 1, translation.Position, out var commandOutput);
            if (commandOutput != null)
                return commandOutput.Value;

            int leftArgs = parameters.Length - 2;

            if (leftArgs == 2)
                if (Enum.TryParse(parameters[0], true, out Tileset tileset) &&
                    Enum.TryParse(parameters[1], true, out TileType tileType))
                    return TryPlaceTile((int)tileset, tileType, pos);

            string fullName = parameters.Take(leftArgs).Join(null, " ");
            CommandOutput output = CommandUtil.ParseItemName(fullName, out ObjectID objectID);
            if (objectID == ObjectID.None)
                return output;

            return PlaceObjectID(objectID, pos);
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

        public static CommandOutput TryPlaceTile(int tileset, TileType tileType, int2 pos)
        {
            PugMapTileset tilesetData = TilesetTypeUtility.GetTileset(tileset);
            LayerName layerName = TileTypeToLayerName.GetLayerName(tileType);
            QuadGenerator quadGenerator = tilesetData.GetDef(layerName);
            if (quadGenerator == null) return new CommandOutput($"Tileset {tileset}, tileType: {tileType} does not exist!", CommandStatus.Error);

            PlaceTile(pos, tileset, tileType);
            return "Tile placed.";
        }

        private static void PlaceTile(int2 pos, int tileset, TileType tileType)
        {
            if (!Manager.saves.IsWorldModeEnabled(WorldMode.Creative) &&
                (tileset == 2 ||
                 math.all(pos == new int2(0, 0)) ||
                 math.all(pos == new int2(0, 1)) ||
                 math.all(pos == new int2(-1, 1)) ||
                 math.all(pos == new int2(1, 1))))
                return;

            TileCD tileCD = new TileCD
            {
                tileset = tileset,
                tileType = tileType
            };

            EntityManager entityManager = API.Server.World.EntityManager;
            Entity tileUpdateEntity = entityManager.CreateEntityQuery(typeof(TileUpdateBuffer)).GetSingletonEntity();
            var updateBuffer = entityManager.GetBuffer<TileUpdateBuffer>(tileUpdateEntity);

            updateBuffer.Add(new TileUpdateBuffer
            {
                command = TileUpdateBuffer.Command.Add,
                position = pos,
                tile = tileCD
            });

            if (tileCD.tileType == TileType.wall)
            {
                updateBuffer.Add(new TileUpdateBuffer
                {
                    command = TileUpdateBuffer.Command.Remove,
                    position = pos,
                    tile = new TileCD
                    {
                        tileType = TileType.roofHole
                    }
                });
                return;
            }

            if (tileCD.tileType == TileType.ground)
            {
                updateBuffer.Add(new TileUpdateBuffer
                {
                    command = TileUpdateBuffer.Command.Remove,
                    position = pos,
                    tile = new TileCD
                    {
                        tileType = TileType.pit
                    }
                });
                updateBuffer.Add(new TileUpdateBuffer
                {
                    command = TileUpdateBuffer.Command.Remove,
                    position = pos,
                    tile = new TileCD
                    {
                        tileType = TileType.water
                    }
                });
            }
        }

        public static TileCD GetTileData(ObjectID objectID, out CommandOutput? commandOutput)
        {
            if (PugDatabase.HasComponent<TileCD>(objectID))
            {
                commandOutput = null;
                return PugDatabase.GetComponent<TileCD>(objectID);
            }

            commandOutput = new CommandOutput("This object is not a tile!", CommandStatus.Error);
            return default;
        }

        public static CommandOutput PlaceObjectID(ObjectID objectID, int2 pos)
        {
            TileCD tileData = GetTileData(objectID, out var commandOutput2);
            if (commandOutput2 != null)
                return commandOutput2.Value;

            PlaceTile(pos, tileData.tileset, tileData.tileType);
            return "Tile placed.";
        }
    }
}