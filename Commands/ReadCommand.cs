using System;
using Terraria;
using Terraria.ModLoader;

namespace WireHead.Commands
{
    public class ReadCommand : ModCommand
    {
        public override CommandType Type => CommandType.Console;

        public override string Command => "read";

        public override string Description
            => "Trigger wire at given coordinates";


        public override void Action(CommandCaller caller, string input, string[] args)
        {
            if (args.Length != 2)
                throw new UsageException("Wrong number of arguments");
            int x, y;
            try
            {

                x = int.Parse(args[0]);
                y = int.Parse(args[1]);
            }
            catch
            {
                throw new UsageException("Failed to parse arguments");
            }
            if (Main.tile[x, y].TileType != 419)
                throw new UsageException($"Tried to read something that wasn't a gate, x: {x}, y: {y}");
            bool state = Accelerator.TileState(x, y);
            Console.WriteLine($"Read complete: {(state ? 1 : 0)}");
        }
    }
}
