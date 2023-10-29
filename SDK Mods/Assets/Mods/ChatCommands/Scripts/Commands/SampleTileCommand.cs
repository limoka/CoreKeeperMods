using System.Text;
using CoreLib.Commands;
using CoreLib.Commands.Communication;
using CoreLib.Util;
using PugTilemap;
using Unity.Collections;
using Unity.Mathematics;

namespace ChatCommands.Chat.Commands
{
    public class SampleTileCommand : IClientCommandHandler
    {
        public CommandOutput Execute(string[] parameters)
        {
            PlayerController player = Players.GetCurrentPlayer();
            if (player == null) return new CommandOutput("Internal error", CommandStatus.Error);

            if (parameters.Length < 2) return new CommandOutput("Not enough parameters, please check usage via /help sampleTile!", CommandStatus.Error);

            int2 pos = CommandUtil.ParsePos(parameters, parameters.Length - 1, player.WorldPosition, out var commandOutput);
            if (commandOutput != null)
                return commandOutput.Value;

            SinglePugMap pugMap = Manager.multiMap;
            var tileInfos = pugMap.GetTileTypesAt(pos, Allocator.Temp);
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append($"Tile at {pos} contains:");

            for (int i = 0; i < tileInfos.Length; i++)
            {
                TileInfo tileInfo = tileInfos[i];
                if (i > 0)
                    stringBuilder.Append(", ");
                stringBuilder.Append($"{tileInfo.tileset.ToString()} {tileInfo.tileType.ToString()}");
            }

            return stringBuilder.ToString();
        }

        public string GetDescription()
        {
            return "Sample a tile";
        }

        public string[] GetTriggerNames()
        {
            return new[] { "sampleTile" };
        }
    }
}