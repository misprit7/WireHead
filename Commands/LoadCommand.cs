using Terraria.ModLoader;


namespace binary_loader.Commands
{
    public class LoadCommandGame : ModCommand
    {

        public override CommandType Type => CommandType.World;

        public override string Command => "bin";

        public override string Usage
            => "bin <cmd> [args]" +
            "\n cmd - command to execute: read, write, config";
        public override string Description
            => "Read/write binary image into logic gates";



        public override void Action(CommandCaller caller, string input, string[] args)
        {
            LoadShared.Exec(args);
        }
    }

    public class LoadCommandServer : ModCommand
    {
        public override CommandType Type => CommandType.Console;

        public override string Command => "bin";

        public override string Description
            => "Read/write binary image into logic gates";


        public override void Action(CommandCaller caller, string input, string[] args)
        {
            LoadShared.Exec(args);
        }
    }

}