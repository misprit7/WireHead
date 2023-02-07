using System.Drawing;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace binary_loader.Common.Commands
{
	public class LoadCommand : ModCommand
	{
		private static int startX = 2112, startY = 283;
		private static int deltaX = 3, deltaY = 4;
		private static int nX = 3, nY = 2;
        // CommandType.Chat means that command can be used in Chat in SP and MP
        public override CommandType Type
			=> CommandType.Chat;

		// The desired text to trigger this command
		public override string Command
			=> "load";

		// A short usage explanation for this command
		public override string Usage
			=> "/load <cmd> [args]" +
			"\n cmd - command to execute: file";

		// A short description of this command
		public override string Description
			=> "Load binary image into logic gates";

		public override void Action(CommandCaller caller, string input, string[] args) {
			// Checking input Arguments
			if (args.Length == 0)
				throw new UsageException("At least one argument was expected.");

			if (args[0] == "file")
			{
                StreamReader sr = new StreamReader(args[1]);
				string line = sr.ReadLine();
				List<int> bytes = new List<int>();
				if (line != null)
				{
					foreach (string str in line.Split()) {
						bytes.Add(int.Parse(str, System.Globalization.NumberStyles.HexNumber));
					}
				}
                for (int i = 0; i < nX; i++)
				{
					for (int j = 0; j < nY; j++)
					{
						int x = startX + i * deltaX;
						int y = startY + j * deltaY;
						Tile t = Main.tile[x, y];
						t.BlockType = (Terraria.ID.BlockType)419;
						t.Slope = (Terraria.ID.SlopeType)0;
						int val = 0x1 & (bytes[i] >> j);
						t.TileFrameX = (short)(18 * val);
					}
				}

            }

		}
	}
}