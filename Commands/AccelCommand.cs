using System;
using Terraria;
using Terraria.ModLoader;

namespace WireHead.Commands
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
                    WireHead.toExec.Enqueue(() => {
                        Accelerator.Preprocess();
                        Console.WriteLine("Preprocessing complete");
                        Main.NewText("Preprocessed");
                    });
                    break;
                case "sync":
                case "s":
                    // Print to console later once sync is actually finished
                    WireHead.toExec.Enqueue(() => {
                        Accelerator.BringInSync(true);
                        Console.WriteLine("Sync complete");
                        Main.NewText("Synced");
                    });
                    break;
                case "enable":
                case "e":
                    WireHead.toExec.Enqueue(() => {
                        if (WireHead.vanillaWiring)
                        {
                            WireHead.AddEvents();
                            Accelerator.Preprocess();
                            Accelerator.convertPb(WireHead.colorPb);
                            Main.NewText("WireHead enabled");
                        }
                        TerraCC.disable();
                        Accelerator.convertPb(WireHead.colorPb);
                        Console.WriteLine("Traditional accelerator enabled");
                    });
                    break;
                case "disable":
                case "d":
                    WireHead.toExec.Enqueue(() => {
                        if (!WireHead.vanillaWiring)
                        {
                            Accelerator.BringInSync();
                            WireHead.RemoveEvents();
                            TerraCC.disable();
                            Accelerator.convertPb(false);
                            Main.NewText("WireHead disabled");
                        }
                        Console.WriteLine("Accelerator disabled");
                    });
                    break;
                case "compile":
                case "terracc":
                case "c":
                    Console.WriteLine("Received compile command");
                    WireHead.toExec.Enqueue(() => {
                        Console.WriteLine("Starting toExec");
                        if (WireHead.vanillaWiring){
                            WireHead.AddEvents();
                            Accelerator.Preprocess();
                        }
                        // Lazy switch
                        if(args.Length <= 1 || (args[1] != "l" && args[1] != "lazy")){
                            TerraCC.transpile();
                            TerraCC.compile();
                        }
                        TerraCC.enable();
                        Main.NewText("World compiled");
                    });
                    break;
                case "color":
                case "col":
                    WireHead.toExec.Enqueue(() => {
                        if(args.Length >= 1 && args[1] == "t" || args[1] == "true" || args[1] == "e" || args[1] == "enable"){
                            WireHead.colorPb = true;
                        } else if(args.Length >= 1 && args[1] == "f" || args[1] == "false" || args[1] == "d" || args[1] == "disable"){
                            WireHead.colorPb = false;
                        } else {
                            WireHead.colorPb = !WireHead.colorPb;
                        }
                        if(!WireHead.vanillaWiring){
                            Accelerator.convertPb(WireHead.colorPb);
                        }
                        Main.NewText((WireHead.colorPb ? "Enabled" : "Disabled") + " colored pixel boxes");
                    });
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
