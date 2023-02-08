using System.Drawing;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using tModPorter;
using System.Text.RegularExpressions;
using Steamworks;
using System;

namespace binary_loader.Common.Commands
{
    public class LoadCommand : ModCommand
    {
        private static int startX, widthX, deltaX, nX;
        private static int startY, widthY, deltaY, nY;

        // format [start]+[width]g[gap]x[length]
        private static Regex configPattern = new(@"(?<start>\d+)\+(?<width>\d+)g(?<gap>\d+)x(?<reps>\d+)");
        private static string configX = "814+2g3x1024";
        private static string configY = "1377+1g4x32";

        public override CommandType Type => CommandType.Chat;

        // The desired text to trigger this command
        public override string Command => "bin";

        public override string Usage
            => "/bin <cmd> [args]" +
            "\n cmd - command to execute: file";
        public override string Description
            => "Load binary image into logic gates";

        private static void ParseConfig()
        {
            Match matchX = configPattern.Match(configX);
            Match matchY = configPattern.Match(configY);

            startX = int.Parse(matchX.Groups["start"].Value);
            widthX = int.Parse(matchX.Groups["width"].Value);
            deltaX = int.Parse(matchX.Groups["gap"].Value);
            nX = int.Parse(matchX.Groups["reps"].Value);

            startY = int.Parse(matchY.Groups["start"].Value);
            widthY = int.Parse(matchY.Groups["width"].Value);
            deltaY = int.Parse(matchY.Groups["gap"].Value);
            nY = int.Parse(matchY.Groups["reps"].Value);
        }

        private static void Traverse(bool rep, Action<int, int, int, int> f)
        {

            for (int i = 0; i < nX; i++)
            {
                for (int j = 0; j < nY; j++)
                {
                    for (int w = 0; w < (rep ? widthX : 1); w++)
                    {
                        for (int h = 0; h < (rep ? widthY : 1); h++)
                        {
                            int x = startX + i * deltaX + w;
                            int y = startY + (nY-1-j) * deltaY + h;

                            f(x, y, i, j);
                        }
                    }
                }
            }
        }

        private static void Store(string file)
        {
            StreamReader sr = new StreamReader(file);
            string line = sr.ReadLine();
            List<int> bytes = new List<int>();
            if (line != null)
            {
                foreach (string str in line.Split())
                {
                    bytes.Add(int.Parse(str, System.Globalization.NumberStyles.HexNumber));
                }
            }

            Traverse(true, (x, y, i, j) =>
            {
                if (Main.tile[x, y + 1].TileType != 420)
                    throw new UsageException($"Tried to place lamp not above a gate, x: {x}, y: {y}");
                Tile t = Main.tile[x, y];
                // Logic lamp
                t.TileType = 419;
                
                // Calculate which bit to grab
                int index = i * nY / 8 + j / 8;
                int bit = index >= bytes.Count ? 0 : 0x1 & (bytes[index] >> (j%8));
                
                // 18 is on, 0 is off
                t.TileFrameX = (short)(18 * bit);
            });
        }

        private static void Read(string file)
        {
            StreamWriter sw = new StreamWriter(file);

            int b = 0;
            Traverse(false, (x, y, i, j) =>
            {
                if (Main.tile[x, y].TileType != 419)
                    throw new UsageException($"Tried to read something that wasn't a gate, x: {x}, y: {y}");
                bool isOn = Main.tile[x, y].TileFrameX == 18;
                b |= isOn ? 0x1 << (j % 8) : 0;
                if(j%8 == 7)
                {
                    sw.Write(b.ToString("X2"));
                    sw.Write(" ");
                    b = 0;
                }
            });
        }

        public override void Action(CommandCaller caller, string input, string[] args) {
            // Checking input Arguments
            if(args.Length == 0)
                throw new UsageException("At least one argument was expected");
            ParseConfig();

            switch (args[0]) {
                case "configX": configX = args[1]; break;
                case "configY": configY = args[1]; break;
                case "config": configX = args[1]; configY = args[2]; break;
                case "store":
                    Store(args[1]);
                    break;
                case "read":
                    Read(args[1]);
                    break;
                default:
                    throw new UsageException("cmd not recognized");
            }
        }
    }
}