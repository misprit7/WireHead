using IL.Terraria;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria;
using Terraria.ModLoader;

namespace WireHead.Commands
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
                WireHead.toExec.Enqueue(() =>
                {
                    if(WireHead.vanillaWiring)
                        Terraria.Wiring.TripWire(x, y, 1, 1);
                    else
                        WiringWrapper.TripWire(x, y, 1, 1);
                    Console.WriteLine("Trigger complete");
                });
            }
            catch
            {
                throw new UsageException("Failed to parse arguments");
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

        // dummy: trigger 3965 1100
        // clk: trigger 3969 1114
        // reset: trigger 3966 1112
        // zero db: trigger 4011 1182
        // zero mem: trigger 3962 1330
        // bin read C:\s\o
        // bin write C:\s\i
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
