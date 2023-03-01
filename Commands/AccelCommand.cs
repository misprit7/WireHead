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
                case "preprocess":
                case "p":
                    Accelerator.Preprocess();
                    break;
                case "sync":
                case "s":
                    Accelerator.BringInSync();
                    break;
                default:
                    throw new UsageException("cmd not recognized");
            }
        }
    }

    internal class AccelCommand
    {
        public class AccelCommandGame : ModCommand
        {

            public override CommandType Type => CommandType.World;

            public override string Command => "accel";

            public override string Usage
                => "accel [cmd]" +
                "\n cmd - command to execute";
            public override string Description
                => "Control wiring accelerator manually";



            public override void Action(CommandCaller caller, string input, string[] args)
            {
                AccelShared.Exec(args);
            }
        }

        public class AccelCommandServer : ModCommand
        {
            public override CommandType Type => CommandType.Console;

            public override string Command => "accel";

            public override string Description
                => "Control wiring accelerator manually";


            public override void Action(CommandCaller caller, string input, string[] args)
            {
                AccelShared.Exec(args);
            }
        }
    }
}
