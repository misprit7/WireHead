using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Terraria.ModLoader;
using Terraria;

namespace WireHead.Commands;

public class Monitor
{
    internal class MonitorShared
    {
        public static void Exec(string[] args)
        {
            // Checking input Arguments
            if (args.Length == 0)
                throw new UsageException("At least one argument was expected");

            for (int i = 0; i < args.Length; ++i)
            {
                switch (args[i])
                {
                    // Format: clock x y max or clock count
                    case "clock" or "c":
                        if (args.Length <= i+1)
                            throw new UsageException("Too few arguments");
                        if (args[i + 1] == "count" || args[i + 1] == "c")
                        {
                            Console.WriteLine(Accelerator.clockCount);
                            ++i;
                            break;
                        }
                        if (args.Length <= i+3)
                            throw new UsageException("Too few arguments");
                        
                        int group = -1;
                        
                        int x = int.Parse(args[i + 1]);
                        int y = int.Parse(args[i + 2]);
                        int max = int.Parse(args[i + 3]);
                        for (int c = 0; c < Accelerator.colors; ++c)
                        {
                            int g = Accelerator.wireGroup[x, y, c];
                            if (g != -1)
                            {
                                group = g;
                                break;
                            }
                        }

                        Accelerator.clockGroup = group;
                        if (max > 0)
                            Accelerator.clockMax = max;
                        i += 3;
                        Console.WriteLine("Clock count started");
                        break;
                    default:
                        throw new UsageException("cmd not recognized");
                }
            }
        }
    }

    internal class MonitorCommand
    {
        private static readonly string usage = "monitor [cmd]" +
                "\n cmd - command to execute";
        private static readonly string description = "Monitor tile";
        public class MonitorCommandGame : ModCommand
        {

            public override CommandType Type => CommandType.World;

            public override string Command => "monitor";

            public override string Usage => usage;
            public override string Description => description;



            public override void Action(CommandCaller caller, string input, string[] args)
            {
                MonitorShared.Exec(args);
            }
        }
        public class MonitorCommandServer : ModCommand
        {
            public override CommandType Type => CommandType.Console;

            public override string Command => "Monitor";

            public override string Usage => usage;
            public override string Description => description;


            public override void Action(CommandCaller caller, string input, string[] args)
            {
                MonitorShared.Exec(args);
            }
        }
    }
}