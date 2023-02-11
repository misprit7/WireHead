using IL.Terraria;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria.ModLoader;

namespace binary_loader.Commands
{
    internal class TriggerShared
    {
        public static void Exec(string[] args)
        {
            if (args.Length != 2)
                throw new UsageException("Wrong number of arguments");
            try
            {
                int x = int.Parse(args[0]);
                int y = int.Parse(args[1]);
                Terraria.Wiring.TripWire(x, y, 1, 1);
            }
            catch
            {
                throw new UsageException("Wrong number of arguments");
            }
        }
    }

    internal class TriggerCommand
    {
        public class TriggerCommandGame : ModCommand
        {

            public override CommandType Type => CommandType.World;

            public override string Command => "trigger";

            public override string Usage
                => "trigger x y" +
                "\n x - x coordinate to trigger wire at" +
                "\n y - y coordinate to trigger wire at";
            public override string Description
                => "Trigger wire at given coordinates";



            public override void Action(CommandCaller caller, string input, string[] args)
            {
                TriggerShared.Exec(args);
            }
        }

        public class TriggerCommandServer : ModCommand
        {
            public override CommandType Type => CommandType.Console;

            public override string Command => "trigger";

            public override string Description
                => "Trigger wire at given coordinates";


            public override void Action(CommandCaller caller, string input, string[] args)
            {
                TriggerShared.Exec(args);
            }
        }
    }
}
