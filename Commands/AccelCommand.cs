using IL.Terraria;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria.ModLoader;

namespace WiringUtils.Commands
{
    internal class AccelShared
    {
        public static void Exec(string[] args)
        {
            switch(args[0])
            {
                // WARNING: Not thread safe if you call preprocess, enable or disable while wiring is occuring
                case "preprocess":
                case "p":
                    Accelerator.Preprocess();
                    Console.WriteLine("Preprocessing completed");
                    break;
                case "sync":
                case "s":
                    Accelerator.shouldSync = true;
                    break;
                case "enable":
                case "e":
                    if (WiringUtils.vanillaWiring)
                    {
                        WiringUtils.AddEvents();
                        Accelerator.Preprocess();
                    }
                    break;
                case "disable":
                case "d":
                    if (!WiringUtils.vanillaWiring)
                    {
                        Accelerator.BringInSync();
                        WiringUtils.RemoveEvents();
                    }
                    break;
                default:
                    throw new UsageException("cmd not recognized");
            }
        }
    }

    internal class AccelCommand
    {
        private static string usage = "accel [cmd]" +
                "\n cmd - command to execute";
        private static string description = "Change wiring acceleration settings";
        public class AccelCommandGame : ModCommand
        {

            public override CommandType Type => CommandType.World;

            public override string Command => "accel";

            public override string Usage => usage;
            public override string Description => description;



            public override void Action(CommandCaller caller, string input, string[] args)
            {
                AccelShared.Exec(args);
            }
        }
        public class AccelCommandGameShort : ModCommand
        {

            public override CommandType Type => CommandType.World;

            public override string Command => "a";

            public override string Usage => usage;
            public override string Description => description;



            public override void Action(CommandCaller caller, string input, string[] args)
            {
                AccelShared.Exec(args);
            }
        }

        public class AccelCommandServer : ModCommand
        {
            public override CommandType Type => CommandType.Console;

            public override string Command => "accel";

            public override string Usage => usage;
            public override string Description => description;


            public override void Action(CommandCaller caller, string input, string[] args)
            {
                AccelShared.Exec(args);
            }
        }
    }
}
