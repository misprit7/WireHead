using Microsoft.CodeAnalysis;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.ID;

namespace WiringUtils
{
    // This is heavily based off of https://github.com/RussDev7/WireShark
    // There are some questionable software engineering choices made there though so I wanted to rewrite it
    internal class Accelerator
    {
        internal static Dictionary<Color, int[,]> wireGroup;
        internal static Color[,] wireCache;
        internal static Dictionary<int, Point16> lamps;
        internal static Dictionary<int, Point16> faulty_lamps;

        [Flags]
        public enum Color
        {
            None = 0,
            Yellow = 1,
            Green = 2,
            Blue = 4,
            Red = 8,
        }
        public static void Preprocess()
        {
            
            foreach (Color c in Enum.GetValues(typeof(Color)))
            {
                wireGroup[c] = new int[Main.maxTilesX, Main.maxTilesY];
                for (int x = 0; x < Main.maxTilesX; x++)
                {
                    for (int y = 0; y < Main.maxTilesY; y++)
                    {
                        wireGroup[c][x, y] = -1;

                        var tile = Main.tile[x, y];
                        Color mask = 0;
                        if (tile.YellowWire) mask |= Color.Yellow;
                        if (tile.GreenWire) mask |= Color.Green;
                        if (tile.BlueWire) mask |= Color.Blue;
                        if (tile.RedWire) mask |= Color.Red;
                        wireCache[x, y] = mask;
                    }
                }
            }

            int group = 0;
            foreach (Color c in Enum.GetValues(typeof(Color)))
            {
                for (int x = 0; x < Main.maxTilesX; x++)
                {
                    for (int y = 0; y < Main.maxTilesY; y++)
                    {
                        ++group;
                        FindGroup(x, y, c, group);
                    }
                }
            }
        }

        private static void FindGroup(int x, int y, Color c, int group)
        {
            if(x < 0 || y < 0 || x >= Main.maxTilesX || y >= Main.maxTilesY) return;
            if (wireGroup[c][x, y] != -1) return;
            if ((wireCache[x, y] & c) == Color.None) return;
            wireGroup[c][x, y] = group;
            Tile tile = Main.tile[x, y];
            if (tile.TileType == TileID.LogicGateLamp)
            {
                if (tile.TileFrameX == 36)
                    faulty_lamps[group] = new Point16(x, y);
                else
                    lamps[group] = new Point16(x, y);
            }
            FindGroup(x + 1, y, c, group);
            FindGroup(x - 1, y, c, group);
            FindGroup(x, y + 1, c, group);
            FindGroup(x, y - 1, c, group);
        }

        public static void TripWire(On.Terraria.Wiring.orig_TripWire orig, int left, int top, int width, int height)
        {

        }
    }
}
