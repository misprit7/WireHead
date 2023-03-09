using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System;
using Terraria.ModLoader;
using Terraria;


namespace WiringUtils.Commands
{
    internal class LoadShared
    {
        private static int offsetX, cellWidthX, cellGapX, cellsX, bankGapX, banksX;
        private static int offsetY, cellWidthY, cellGapY, cellsY, bankGapY, banksY;

        private static Regex configPattern = new(@"(?<offset>\d+)\+(?<cell_width>\d+)g(?<cell_gap>\d+)x(?<cells>\d+)g(?<bank_gap>\d+)x(?<banks>\d+)");
        private static string configX = "46+2g3x1024g3185x2";
        private static string configY = "421+1g4x32g156x12";

        private static void PrintSuccess(string msg)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(msg);
            Console.ResetColor();
        }

        private static void ParseConfig()
        {
            Match matchX = configPattern.Match(configX);
            Match matchY = configPattern.Match(configY);

            offsetX = int.Parse(matchX.Groups["offset"].Value);
            cellWidthX = int.Parse(matchX.Groups["cell_width"].Value);
            cellGapX = int.Parse(matchX.Groups["cell_gap"].Value);
            cellsX = int.Parse(matchX.Groups["cells"].Value);
            bankGapX = int.Parse(matchX.Groups["bank_gap"].Value);
            banksX = int.Parse(matchX.Groups["banks"].Value);

            offsetY = int.Parse(matchY.Groups["offset"].Value);
            cellWidthY = int.Parse(matchY.Groups["cell_width"].Value);
            cellGapY = int.Parse(matchY.Groups["cell_gap"].Value);
            cellsY = int.Parse(matchY.Groups["cells"].Value);
            bankGapY = int.Parse(matchY.Groups["bank_gap"].Value);
            banksY = int.Parse(matchY.Groups["banks"].Value);
        }

        private static void Traverse(bool rep, Action<int, int, int, int> f)
        {
            for (int by = 0; by < banksY; by++)
            {
                for (int bx = 0; bx < banksX; bx++)
                {
                    for (int cx = 0; cx < cellsX; cx++)
                    {
                        for (int cy = 0; cy < cellsY; cy++)
                        {
                            for (int w = 0; w < (rep ? cellWidthX : 1); w++)
                            {
                                for (int h = 0; h < (rep ? cellWidthY : 1); h++)
                                {
                                    int x = offsetX + cx * cellGapX + bx * bankGapX + w;
                                    int y = offsetY + (cellsY - 1 - cy) * cellGapY + by * bankGapY + h;

                                    f(x, y, bx * cellsX + by * cellsX * banksX + cx, cy);
                                }
                            }
                        }
                    }
                }
            }
        }


        private static void Write(string file)
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
                int index = i * cellsY / 8 + j / 8;
                int bit = index >= bytes.Count ? 0 : 0x1 & bytes[index] >> j % 8;

                // 18 is on, 0 is off
                t.TileFrameX = (short)(18 * bit);
            });
            sr.Close();
            PrintSuccess("Write complete");
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
                b |= isOn ? 0x1 << j % 8 : 0;
                if (j % 8 == 7)
                {
                    // Write in two digit hex
                    sw.Write(b.ToString("X2"));
                    // Ends up with a trailing space but I'm too lazy to fix that
                    sw.Write(" ");
                    b = 0;
                }
            });
            sw.Close();
            PrintSuccess("Read complete");
        }

        public static void Exec(string[] args)
        {
            // Checking input Arguments
            if (args.Length == 0)
                throw new UsageException("At least one argument was expected");
            ParseConfig();

            switch (args[0])
            {
                case "configx":
                case "cx":
                    configX = args[1];
                    break;
                case "configy":
                case "cy":
                    configY = args[1];
                    break;
                case "config":
                case "c":
                    configX = args[1];
                    configY = args[2];
                    break;
                case "write":
                case "w":
                    Write(args[1]);
                    break;
                case "read":
                case "r":
                    Read(args[1]);
                    break;
                default:
                    throw new UsageException("cmd not recognized");
            }
        }
    }

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
