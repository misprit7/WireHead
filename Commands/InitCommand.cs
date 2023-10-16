using Terraria.ModLoader;
using Terraria.DataStructures;
using Terraria.GameContent.Tile_Entities;

namespace WireHead.Commands
{
    public class InitCommand : ModCommand
    {
        public override CommandType Type => CommandType.Console;

        public override string Command => "init";

        public override string Description
            => "Initializes target dummies in multiplayer server without gui";


        public override void Action(CommandCaller caller, string input, string[] args)
        {
            foreach(var kv in TileEntity.ByID)
            {
                if(kv.Value is TETrainingDummy)
                {
                    TETrainingDummy te = kv.Value as TETrainingDummy;
                    if(te.npc == -1)
                    {
                        te.Activate();
                    }
                }
            }
        }
    }
}
