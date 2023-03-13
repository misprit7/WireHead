
using System;
using Terraria;
using Terraria.ModLoader;

namespace WireHead.Commands
{
    public class WriteCommand : ModCommand
    {
        public override CommandType Type => CommandType.Console;

        public override string Command => "write";

        public override string Description
            => "Write value to the given coordinate";


        public override void Action(CommandCaller caller, string input, string[] args)
        {
            if (args.Length != 3)
                throw new UsageException("Wrong number of arguments");
            int x, y;
            short bit;
            try
            {

                x = int.Parse(args[0]);
                y = int.Parse(args[1]);
                bit = short.Parse(args[2]);
            }
            catch
            {
                throw new UsageException("Failed to parse arguments");
            }
            if (Main.tile[x, y].TileType != 419)
                throw new UsageException($"Tried to write to something that wasn't a gate, x: {x}, y: {y}");
            if (bit < 0 || bit > 1)
                throw new UsageException("Can only set gate to 0 or 1");
            Main.tile[x, y].TileFrameX = (short)(bit * 18);
            NetMessage.SendTileSquare(-1, x, y);
        }
    }
}
