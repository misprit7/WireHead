using Microsoft.CodeAnalysis;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.ID;
using Terraria.ModLoader;

namespace WiringUtils
{
    // This is heavily based off of https://github.com/RussDev7/WireShark
    // There are some questionable software engineering choices made there though so I wanted to rewrite it
    internal static class Accelerator
    {

        /**********************************************************************
         * Wire Caching Variables
         *********************************************************************/

        // Which group each tile belongs to
        // -1 is no group, 0 is junction box with two groups
        public static Dictionary<Color, int[,]> wireGroup;
        // Copy of which wires are at each tile in a more usable form
        public static Color[,] wireCache;
        // Set of lamps/faultylamps attached to each point
        public static Dictionary<int, HashSet<Point16>> toggleable;
        public static Dictionary<int, HashSet<Point16>> triggerable;
        // True, and if so state is whether wire is on or not, not present if faulty logic gate present
        public static Dictionary<int, bool> groupState;
        // Groups that are out of sync with the world, value is original state of group
        public static Dictionary<int, bool> groupsOutOfSync;



        /**********************************************************************
         * Constants
         *********************************************************************/
        // const data
        [Flags]
        public enum Color
        {
            None = 0,
            Yellow = 1,
            Green = 2,
            Blue = 4,
            Red = 8,
        }
        // List of actual (non-None) colors to loop over
        public static readonly HashSet<Color> Colors = new HashSet<Color>
        {
            Color.Yellow, Color.Green, Color.Blue, Color.Red,
        };
        // Corresponds to internal Terraria wiring representations
        public static readonly Dictionary<int, Color> intToColor = new Dictionary<int, Color>
        {
            { 1, Color.Red },
            { 2, Color.Blue },
            { 3, Color.Green },
            { 4, Color.Yellow },
        };

        public static readonly HashSet<int> triggeredIDs = new HashSet<int>
        {
            TileID.LogicGateLamp, // treated specially
            TileID.ClosedDoor,
            TileID.OpenDoor,
            TileID.TrapdoorClosed,
            TileID.TrapdoorOpen,
            TileID.TallGateClosed,
            TileID.TallGateOpen,
            TileID.InletPump,
            TileID.OutletPump,
            TileID.Traps,
            TileID.GeyserTrap,
            TileID.Explosives,
            TileID.VolcanoLarge,
            TileID.VolcanoSmall,
            TileID.Toilets,
            TileID.Cannon,
            TileID.MusicBoxes,
            TileID.Statues,
            TileID.Chimney,
            TileID.Teleporter,
            TileID.Firework,
            TileID.FireworkFountain,
            TileID.FireworksBox,
            TileID.Confetti,
            TileID.BubbleMachine,
            TileID.SillyBalloonMachine,
            TileID.LandMine,
            TileID.Campfire,
            TileID.AnnouncementBox,
            TileID.FogMachine,
        };

        // Purposefully excludes multi-block toggleable tiles like fireplaces
        // Also no actuators since they might result in multiple triggers on one tile
        public static readonly HashSet<int> toggleableIDs = new HashSet<int>
        {
            TileID.LogicGateLamp, // treated specially
            TileID.Candles,
            TileID.Torches,
            TileID.WireBulb,
            TileID.Timers,
            TileID.ActiveStoneBlock,
            TileID.InactiveStoneBlock,
            TileID.Grate,
            TileID.AmethystGemspark,
            TileID.TopazGemspark,
            TileID.SapphireGemspark,
            TileID.EmeraldGemspark,
            TileID.RubyGemspark,
            TileID.DiamondGemspark,
            TileID.AmberGemspark,
        };

        /**********************************************************************
         * Private Functions
         *********************************************************************/

        /*
         * Register a tile as part of a group
         */
        private static void RegisterTile(int x, int y, Color c, int group)
        {
            Tile tile = Main.tile[x, y];

            // Junction boxes always have id -1
            // To avoid an infinite loop we ensure we pass through them one way
            // Since each junction box has exactly one entrance per channel and one exit this works
            if (tile.TileType != TileID.WirePipe)
                wireGroup[c][x, y] = group;

            // Treat logic lamps specially since they mean different things based on frame
            if (tile.TileType == TileID.LogicGateLamp)
            {
                // Faulty
                if (tile.TileFrameX == 36)
                {
                    triggerable[group].Add(new Point16(x, y));
                    groupState.Remove(group);
                }
                // On/off
                else
                {
                    toggleable[group].Add(new Point16(x, y));
                }
            }
            else if (triggeredIDs.Contains(tile.TileType))
            {
                triggerable[group].Add(new Point16(x, y));
                groupState.Remove(group);
            }
            else if (toggleableIDs.Contains(tile.TileType))
            {
                toggleable[group].Add(new Point16(x, y));
            }
        }

        /*
         * Find each of the wire groups in the world recursively, used for preprocessing
         */
        private static void FindGroup(int x, int y, int prevX, int prevY, Color c, int group)
        {
            if (!WorldGen.InWorld(x, y, 1)) return;
            if ((wireCache[x, y] & c) == Color.None) return;

            Tile tile = Main.tile[x, y];

            // If we've already traversed this junction box with this group skip it
            // If the junction box already has two groups skip it
            // If it isn't a junction box skip it if it's been traversed before
            if (tile.TileType == TileID.WirePipe)
            {
                // Yes there's a logical simplification here with nested ifs but the logic is clearer this way
                if (wireGroup[c][x, y] == group || wireGroup[c][x, y] == 0) return;
            }
            else if (wireGroup[c][x, y] != -1) return;

            // Handle all registration of this tile to caches
            RegisterTile(x, y, c, group);

            if (tile.TileType != TileID.WirePipe)
            {
                bool prevJunction = Main.tile[prevX, prevY].TileType == TileID.WirePipe;
                // What 30 minutes of stackoverflow searching for marginally cleaner syntax gets you
                foreach (var (dx, dy) in new (int, int)[] { (1, 0), (0, 1), (-1, 0), (0, -1) })
                {
                    // Prevents you from going backwards into a junction box you just left
                    // Important to prevent infinite loops
                    if (!(prevJunction && prevX == x + dx && prevY == y + dy))
                        FindGroup(x + dx, y + dy, x, y, c, group);
                }
            }
            else
            {
                int deltaX = 0, deltaY = 0;
                switch (tile.TileFrameX)
                {
                    case 0: // + shape
                        deltaX = (x - prevX);
                        deltaY = (y - prevY);
                        break;
                    case 18: // // shape
                        deltaX = -(y - prevY);
                        deltaY = -(x - prevX);
                        break;
                    case 36:// \\ shape
                        deltaX = (y - prevY);
                        deltaY = (x - prevX);
                        break;
                    default:
                        throw new UsageException("Junction box frame didn't line up to 0, 18 or 36");
                }

                FindGroup(x + deltaX, y + deltaY, x, y, c, group);
            }
        }

        /*
         * Hit a single wire
         */
        private static void HitWireSingle(Point16 p)
        {
            // _wireSkip should 100% be a hashset, come on relogic
            if (WiringWrapper._wireSkip.ContainsKey(p)) return;
            WiringWrapper.HitWireSingle(p.X, p.Y);
        }

        /*
         * Calculates whether a given x, y tile should toggle 
         */
        private static bool ShouldChange(int x, int y)
        {
            // oldVal is value before last sync, newVal is current value
            // We need to do it this way, otherwise we have to know how to individually toggle
            // each individual type of toggleable tile, most of which are sprite dependent (which
            // on a related note is really dumb, you shouldn't have to check tile.TileFrameX == 66
            // or whatever to see that a torch is on) 
            bool oldVal = false, newVal = false;
            foreach (Color c in Colors)
            {
                int colorGroup = wireGroup[c][x, y];
                if (colorGroup != -1 && groupState.ContainsKey(colorGroup))
                {
                    // Here != is used as logical xor
                    newVal = (newVal != groupState[colorGroup]);
                    if (groupsOutOfSync.ContainsKey(colorGroup))
                    {
                        oldVal = (oldVal != groupsOutOfSync[colorGroup]);
                    }
                    else
                    {
                        oldVal = (oldVal != groupState[colorGroup]);
                    }
                }
            }
            return oldVal != newVal;
        }

        /**********************************************************************
         * Vanilla WiringWrapper Overrides
         *********************************************************************/

        /*
         * Triggers a set of wires of a particular type
         */
        public static void HitWire(DoubleStack<Point16> next, int wireType)
        {
            Color c = intToColor[wireType];
            HashSet<int> alreadyHit = new HashSet<int>();
            WiringWrapper._currentWireColor = wireType;
            while (next.Count != 0)
            {
                var p = next.PopFront();
                int group = wireGroup[c][p.X, p.Y];

                if (alreadyHit.Contains(group)) continue;
                else alreadyHit.Add(group);

                if (group == -1) continue;

                if (groupState.ContainsKey(group))
                {
                    // Keep track of syncing
                    if (!groupsOutOfSync.ContainsKey(group))
                        groupsOutOfSync[group] = groupState[group];
                    // If all toggleable then no need to directly trigger them
                    groupState[group] = !groupState[group];
                }
                else
                {
                    // If even one triggered tile then must trigger all of them
                    foreach (var toggleableTile in toggleable[group])
                    {
                        HitWireSingle(toggleableTile);
                    }
                    foreach (var point in triggerable[group])
                    {
                        HitWireSingle(point);
                    }
                }
            }
            WiringWrapper.running = false;
            Wiring.running = false;
            WiringWrapper._wireSkip.Clear();
        }

        /*
         * Check a faulty gate and handle it from WiringWrapper.CheckLogicGate
         * Requires that X, faultyY are coordinates of a faulty logic lamp with one cell
         */
        public static void CheckFaultyGate(int X, int faultyY)
        {
            int lampY = faultyY + 1;
            int gateY = faultyY + 2;
            bool on = Main.tile[X, lampY].TileFrameX == 18;
            // boolean xor
            on = (on != ShouldChange(X, lampY));

            // From decompiled, not sure if this is needed
            WiringWrapper.SkipWire(X, gateY);

            if (on)
            {
                WiringWrapper._GatesDone.TryGetValue(new Point16(X, gateY), out bool alreadyDone);
                if (alreadyDone)
                {
                    // Taken from decompiled
                    Vector2 position = new Vector2(X, gateY) * 16f - new Vector2(10f);
                    Utils.PoofOfSmoke(position);
                    NetMessage.SendData(MessageID.PoofOfSmoke, -1, -1, null, (int)position.X, position.Y);
                    return;
                }
                WiringWrapper._GatesNext.Enqueue(new Point16(X, gateY));
            }
        }

        /**********************************************************************
         * Public Functions
         *********************************************************************/

        /*
         * Preprocess the world into logical wire groups to speed up processing
         */
        public static void Preprocess()
        {
            // Try and bring in sync before resetting everything
            if (wireGroup != null) BringInSync();
            wireGroup = new Dictionary<Color, int[,]>();
            toggleable = new Dictionary<int, HashSet<Point16>>();
            triggerable = new Dictionary<int, HashSet<Point16>>();
            groupState = new Dictionary<int, bool>();
            wireCache = new Color[Main.maxTilesX, Main.maxTilesY];
            groupsOutOfSync = new Dictionary<int, bool>();

            foreach (Color c in Colors)
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

            int group = 1;
            foreach (Color c in new HashSet<Color> { Color.Yellow, Color.Green, Color.Blue, Color.Red })
            {
                for (int x = 0; x < Main.maxTilesX; x++)
                {
                    for (int y = 0; y < Main.maxTilesY; y++)
                    {
                        if (wireGroup[c][x, y] == -1 &&
                            (wireCache[x, y] & c) != Color.None &&
                            Main.tile[x, y].TileType != TileID.WirePipe)
                        {
                            groupState[group] = false;
                            triggerable[group] = new HashSet<Point16>();
                            toggleable[group] = new HashSet<Point16>();
                            // Guaranteed not to start on junction box so don't care about prevX,Y
                            FindGroup(x, y, x, y, c, group);
                            ++group;
                        }
                    }
                }
            }
        }

        /*
         * Brings back into sync after running efficiently
         */
        public static void BringInSync()
        {
            HashSet<Point16> lampsVisited = new HashSet<Point16>();

            foreach (var (group, _) in groupsOutOfSync)
            {
                foreach (Point16 toggleablePoint in toggleable[group])
                {
                    if (lampsVisited.Contains(toggleablePoint)) continue;

                    lampsVisited.Add(toggleablePoint);
                    
                    if (ShouldChange(toggleablePoint.X, toggleablePoint.Y))
                    {
                        HitWireSingle(toggleablePoint);
                    }
                }
            }
            groupsOutOfSync.Clear();
        }
    }
}
