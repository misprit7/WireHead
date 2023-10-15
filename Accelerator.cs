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

namespace WireHead;

// This lightly inspired by https://github.com/RussDev7/WireShark
// There are some questionable software engineering choices made there though so I wanted to rewrite it
internal static class Accelerator
{

    /**********************************************************************
     * Wire Caching Variables
     *********************************************************************/

    // Which group each tile belongs to
    // -1 is no group, 0 is junction box with two groups
    // Format is x,y,color
    // For preprocessing it's more efficient to have color,x,y, but for run time performance the bottleneck
    // is checking the same tile so for data locality it makes sense to do it this way
    public static int[,,] wireGroup;

    // Copy of which wires are at each tile in a more usable form
    // Entries are bitmasks of 1<<color
    // i.e. red = 1, blue = 2, green = 4, yellow = 8
    public static int[,] wireCache;

    // Lookup array of "Standard" faulty lamp arrangments
    // Used to be hash set but hash set lookup was performance bottleneck so flat array should be faster
    public static bool[,] standardLamps;
    // Mapping from group -> list of standard lamps this is top of
    public static List<uint>[] groupStandardLamps;

    // Set of groups that were triggered in this iteration
    // Used for pixel boxes
    // List.Clear is O(n) for idiotic reasons so I'm recreating it as an array
    public static int[] groupsTriggered;
    public static int numGroupsTriggered = 0;

    // Toggleable/triggerable tiles attached to each point
    // First index is group, second is list of tiles
    // uint is a bit concatenation of both 16 bit x,y coordinates
    public static uint[][] toggleable;
    public static uint[][] triggerable;

    // For each group, contains all other groups that could make a pixel box trigger
    // Assumes that each pair of 2 groups only attaches to one pixel box
    // If you wanted to remove this assumtion it should be a list of (int, uints) instead
    public static Dictionary<int, uint>[] pixelBoxes;
    // Conversion between pixel box ids and coordinates
    public static List<uint> pbId2Coord;
    public static Dictionary<uint, int> pbCoord2Id;
    // Number of pixel boxes
    public static int numPb = 0;

    // Number of total groups
    // All arrays indexed by groups are guaranteed to be of this size
    public static int numGroups = 0;
    // State of a group, indexed by group
    public static bool[] groupState;
    // Groups that are out of sync with the world, value is original state of group
    public static bool[] groupOutOfSync;

    // Terracc wire activation summary
    // Guaranteed to be of size [maxTriggers, colors]
    public static int[,] toHit;
    // Number of new groups in toHit
    public static int numToHit = 0;

    
    /**********************************************************************
     * Monitor Variables
     *********************************************************************/
    
    public static int clockGroup = -1;
    public static int clockCount = 0;
    public static int clockMax = int.MaxValue;

    /**********************************************************************
     * Construction Variables
     *********************************************************************/

    // Variables to help in the construction of toggleable/triggerable
    public static Dictionary<int, HashSet<Point16>> toggleableDict = new Dictionary<int, HashSet<Point16>>();
    public static Dictionary<int, HashSet<Point16>> triggerableDict = new Dictionary<int, HashSet<Point16>>();


    /**********************************************************************
     * Constants
     *********************************************************************/

    public static readonly int maxTriggers = 10000; 

    // Used to have enum, removed for efficiency
    // In array indexing, red=0, blue=1, green=2, yellow=3
    public const int colors = 4;

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
    private static void RegisterTile(int x, int y, int c, int group)
    {
        Tile tile = Main.tile[x, y];

        // Junction boxes always have id -1
        // To avoid an infinite loop we ensure we pass through them one way
        // Since each junction box has exactly one entrance per channel and one exit this works
        if (tile.TileType != TileID.WirePipe)
            wireGroup[x, y, c] = group;

        // Treat logic lamps specially since they mean different things based on frame
        if (tile.TileType == TileID.LogicGateLamp)
        {
            // Faulty
            if (tile.TileFrameX == 36)
            {
                triggerableDict[group].Add(new Point16(x, y));
            }
            // On/off
            else
            {
                toggleableDict[group].Add(new Point16(x, y));
            }
        }
        else if (tile.TileType == TileID.PixelBox)
        {
            for (int col = 0; col < colors; ++col)
            {
                if (col == c) continue;
                int g = wireGroup[x, y, col];
                if (g != -1)
                {
                    uint coord = xy2uint(x, y);
                    pixelBoxes[g][group] = coord;
                    pixelBoxes[group][g] = coord;
                    pbId2Coord.Add(coord);
                }
            }

        }
        else if (triggeredIDs.Contains(tile.TileType) || tile.HasActuator)
        {
            triggerableDict[group].Add(new Point16(x, y));
        }
        else if (toggleableIDs.Contains(tile.TileType))
        {
            toggleableDict[group].Add(new Point16(x, y));
        }
    }

    /*
     * Find each of the wire groups in the world recursively, used for preprocessing
     */
    private static void FindGroup(int x, int y, int prevX, int prevY, int c, int group)
    {
        if (!WorldGen.InWorld(x, y, 1)) return;
        if ((wireCache[x, y] & (1<<c)) == 0) return;

        Tile tile = Main.tile[x, y];

        // If we've already traversed this junction box with this group skip it
        // If the junction box already has two groups skip it
        // If it isn't a junction box skip it if it's been traversed before
        if (tile.TileType == TileID.WirePipe)
        {
            // Yes there's a logical simplification here with nested ifs but the logic is clearer this way
            if (wireGroup[x, y, c] == group || wireGroup[x, y, c] == 0) return;
        }
        else if (wireGroup[x, y, c] != -1) return;

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
     * Checks whether a tile is the top of a standard gate
     */
    public static bool IsStandardFaulty(int x, int y)
    {
        int faultyY = y, lampY = y + 1, gateY = y + 2;
        return gateY < Main.maxTilesY &&
            Main.tile[x, faultyY].TileType == TileID.LogicGateLamp &&
            Main.tile[x, faultyY].TileFrameX == 36 &&
            Main.tile[x, lampY].TileType == TileID.LogicGateLamp &&
            Main.tile[x, lampY].TileFrameX != 36 &&
            Main.tile[x, gateY].TileType == TileID.LogicGate &&
            Main.tile[x, gateY].TileFrameX == 36;
    }

    /*
     * Toggle a logic lamp
     */
    private static void ToggleLamp(int x, int y)
    {
        // Don't call WorldGen.SquareTileFrame and NetMessage.SendTileSquare
        // Also don't enqueue check since it isn't needed
        Tile tile = Main.tile[x, y];
        if (tile.TileFrameX == 0) tile.TileFrameX = 18;
        else if (tile.TileFrameX == 18) tile.TileFrameX = 0;
    }

    /*
     * Toggles a pixel box
     */
    private static void TogglePixelBox(int x, int y)
    {
        Tile tile = Main.tile[x, y];
        if (tile.TileFrameX == 0) tile.TileFrameX = 18;
        else if (tile.TileFrameX == 18) tile.TileFrameX = 0;
        if (Main.netMode == NetmodeID.Server)
        {
            NetMessage.SendTileSquare(-1, x, y);
        }
    }

    /*
     * Hit a single wire
     */
    private static void HitWireSingle(uint p)
    {
        // _wireSkip should 100% be a hashset, come on relogic
        //if (WiringWrapper._wireSkip.ContainsKey(p)) return;

        short x = (short)(p >> 16), y = (short)(p & 0xFFFF);

        if (standardLamps[x, y])
        {
            // Potential performance bottleneck
            WiringWrapper._LampsToCheck.Enqueue(new Point16(x, y));
        } 
        else if (standardLamps[x, y-1])
        {
            ToggleLamp(x, y);
        }
        else
        {
            WiringWrapper.HitWireSingle(x, y);
        }
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
        bool ret = false;
        //for (int c = 0; c < colors; ++c)
        //{
        //    int group = wireGroup[x, y, c];
        //    if (group != -1)
        //    {
        //        ret = ret != groupOutOfSync[group];
        //    }
        //}

        // Pretty ugly but want to make sure compiler isn't being dumb in above loop

        int group = wireGroup[x, y, 0];
        if (group != -1)
        {
            ret = ret != groupOutOfSync[group];
        }

        group = wireGroup[x, y, 1];
        if (group != -1)
        {
            ret = ret != groupOutOfSync[group];
        }

        group = wireGroup[x, y, 2];
        if (group != -1)
        {
            ret = ret != groupOutOfSync[group];
        }

        group = wireGroup[x, y, 3];
        if (group != -1)
        {
            ret = ret != groupOutOfSync[group];
        }

        return ret;

    }

    // Being smarter than this requires run time performance hit, so real time is optimized
    private static void SyncClients()
    {
        for (int x = 0; x < Main.maxTilesX; ++x)
        {
            for (int y = 0; y < Main.maxTilesY; ++y)
            {
                Tile tile = Main.tile[x, y];
                if (toggleableIDs.Contains(tile.TileType))
                {
                    NetMessage.SendTileSquare(-1, x, y);
                }
            }
        }
    }

    /*
     * Converts a point to/from a bitwise uint
     * Most significant 16 bits are x, least are y
     */
    internal static uint Point2uint(Point16 p)
    {
        uint x = ((uint)p.X) << 16;
        uint y = (uint)p.Y;
        return x | y;
    }

    internal static uint xy2uint(int x, int y)
    {
        uint x1 = ((uint)x) << 16;
        uint y1 = (uint)y;
        return x1 | y1;
    }
    internal static Point16 uint2Point(uint p)
    {
        return new Point16((short)(p >> 16), (short)(p & 0xFFFF));
    }

    /**********************************************************************
     * Vanilla WiringWrapper Overrides
     *********************************************************************/

    /*
     * Triggers a set of wires of a particular type
     */
    public static void HitWire(DoubleStack<Point16> next, int wireType)
    {
        if(WireHead.useTerracc) return;
        int c = wireType-1;
        HashSet<int> alreadyHit = new HashSet<int>();
        WiringWrapper._currentWireColor = wireType;
        while (next.Count != 0)
        {
            var p = next.PopFront();
            int group = wireGroup[p.X, p.Y, c];

            /* if(WireHead.useTerracc && triggerable[group].Length == groupStandardLamps[group].Count) continue; */

            if (alreadyHit.Contains(group)) continue;
            else alreadyHit.Add(group);

            if (group == -1) continue;

            groupsTriggered[numGroupsTriggered++] = group;
            
            if (group == clockGroup)
            {
                ++clockCount;
                if (clockCount > clockMax)
                {
                    Console.WriteLine("Clock finished");
                    clockCount = 0;
                    clockGroup = -1;
                    clockMax = int.MaxValue;
                }
            }

            // Keep track of syncing
            groupOutOfSync[group] = !groupOutOfSync[group];
            groupState[group] = !groupState[group];

            // If even one triggered tile then must trigger all of them
            // Supposedly using local variables disables bound checking although I'm doubtful
            // https://blog.tedd.no/2020/06/01/faster-c-array-access/
            /* var tog = toggleable[group]; */
            /* for (int i = 0; i < tog.Length; ++i) */
            /* { */
            /*     HitWireSingle(tog[i]); */
            /* } */
            var trig = triggerable[group];
            for (int i = 0; i < trig.Length; ++i)
            {
                HitWireSingle(trig[i]);
            }

            // Only bother checking pixel boxes if some are attached to this group
            if (pixelBoxes[group].Count != 0)
            {
                for (int i = 0; i < numGroupsTriggered; ++i)
                {
                    int g = groupsTriggered[i];
                    if (pixelBoxes[group].ContainsKey((g)))
                    {
                        Point16 point = uint2Point(pixelBoxes[group][g]);
                        TogglePixelBox(point.X, point.Y);
                    }
                }
            }
        }
        WiringWrapper.running = false;
        Wiring.running = false;
        //WiringWrapper._wireSkip.Clear();
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
     * Gets current true value of tile state
     * Only compatible with logic gates and candles
     */
    public static bool TileState(int x, int y)
    {
        Tile tile = Main.tile[x, y];
        bool ret = false;
        if (tile.TileType == TileID.LogicGateLamp)
        {
            ret = tile.TileFrameX != 0;
        } else if (tile.TileType == TileID.Candles)
        {
            ret = tile.TileFrameX == 0;
        }
        else
        {
            throw new UsageException("Attempted to read unsupported tile");
        }

        return ret != ShouldChange(x, y);
    }

    /*
     * Preprocess the world into logical wire groups to speed up processing
     */
    public static void Preprocess()
    {
        // Try and bring in sync before resetting everything
        BringInSync();
        
        standardLamps = new bool[Main.maxTilesX, Main.maxTilesY];
        wireCache = new int[Main.maxTilesX, Main.maxTilesY];
        wireGroup = new int[Main.maxTilesX, Main.maxTilesY, colors];

        toggleableDict = new Dictionary<int, HashSet<Point16>>();
        triggerableDict = new Dictionary<int, HashSet<Point16>>();
        pbId2Coord = new List<uint>();

        toHit = new int[maxTriggers, colors];

        for (int x = 0; x < Main.maxTilesX; ++x)
        {
            for (int y = 0; y < Main.maxTilesY; ++y)
            {
                var tile = Main.tile[x, y];
                int mask = 0;
                if (tile.YellowWire) mask |= 8;
                if (tile.GreenWire) mask |= 4;
                if (tile.BlueWire) mask |= 2;
                if (tile.RedWire) mask |= 1;
                wireCache[x, y] = mask;


                if (IsStandardFaulty(x, y)) standardLamps[x, y] = true;
                else standardLamps[x, y] = false;

                for (int c = 0; c < colors; ++c)
                {
                    wireGroup[x, y, c] = -1;
                }
            }
        }

        int group = 0;
        numGroups = 1 << 10;
        groupState = new bool[numGroups];
        groupOutOfSync = new bool[numGroups];
        pixelBoxes = new Dictionary<int, uint>[numGroups];
        for (int x = 0; x < Main.maxTilesX; ++x)
        {
            for (int y = 0; y < Main.maxTilesY; ++y)
            {
                for (int c = 0; c < colors; ++c)
                {
                    if (wireGroup[x, y,c] == -1 &&
                        (wireCache[x, y] & (1<<c)) != 0 &&
                        Main.tile[x, y].TileType != TileID.WirePipe)
                    {
                        if(group >= numGroups)
                        {
                            numGroups *= 2;
                            Array.Resize(ref groupState, numGroups);
                            Array.Resize(ref groupOutOfSync, numGroups);
                            Array.Resize(ref pixelBoxes, numGroups);
                        }
                        groupState[group] = false;
                        groupOutOfSync[group] = false;

                        toggleableDict[group] = new HashSet<Point16>();
                        triggerableDict[group] = new HashSet<Point16>();
                        pixelBoxes[group] = new Dictionary<int, uint>();
                        // Guaranteed not to start on junction box so don't care about prevX,Y
                        FindGroup(x, y, x, y, c, group);
                        ++group;
                    }
                }
            }
        }
        // Running resizing was an overestimate, switch back
        numGroups = group;

        // Convert toggleableDict/triggerableDict to arrays
        toggleable = new uint[numGroups][];
        triggerable = new uint[numGroups][];
        groupStandardLamps = new List<uint>[numGroups];
        for (int g = 0; g < numGroups; ++g)
        {
            toggleable[g] = new uint[toggleableDict[g].Count];
            triggerable[g] = new uint[triggerableDict[g].Count];
            groupStandardLamps[g] = new List<uint>();
            int i = 0;
            foreach (Point16 p in toggleableDict[g])
            {
                toggleable[g][i++] = Point2uint(p);
            }
            i = 0;
            foreach (Point16 p in triggerableDict[g])
            {
                triggerable[g][i++] = Point2uint(p);
                if (standardLamps[p.X, p.Y]){
                    groupStandardLamps[g].Add(Point2uint(p));
                }
            }
        }

        toggleableDict.Clear();
        triggerableDict.Clear();
        groupsTriggered = new int[numGroups];

        // Inverts pbId2Coord to a dictionary with inverse mapping
        pbCoord2Id = pbId2Coord.Select((s, i) => new { s, i }).ToDictionary(x => x.s, x => x.i);
        numPb = pbId2Coord.Count();

    }

    /*
     * Brings just pixel boxes into sync
     */
    public static void SyncPb()
    {
        if(WireHead.useTerracc){
            byte[] pb_states = new byte[numPb];
            TerraCC.read_pb(pb_states);
            for(int i = 0; i < numPb; ++i){
                if(pb_states[i] == 0) continue;
                Point16 p = uint2Point(pbId2Coord[i]);
                TogglePixelBox(p.X, p.Y);
            }
        }
    }

    /*
     * Brings back into sync after running efficiently
     */
    public static void BringInSync()
    {

        if(WireHead.useTerracc){
            byte[] states = new byte[numGroups];
            TerraCC.read_states(states);
            for(int i = 0; i < numGroups; ++i){
                /* if((states[i]==1) != groupState[i]){ */
                /*     Console.WriteLine($"State {i} new state: {states[i]}"); */
                /* } */
                groupOutOfSync[i] = (states[i]==1) != groupState[i];
                groupState[i] = states[i] == 1;

                // Do our best to handle triggered blocks, this will only work
                // if they've triggered at most once since the last sync though
                if(groupOutOfSync[i]){
                    // This might not be robust, I think if you make purposefully
                    // confusing chained teleporters this breaks.
                    //
                    // Don't do that.

                    WiringWrapper._teleport[0].X = -1f;
                    WiringWrapper._teleport[0].Y = -1f;
                    WiringWrapper._teleport[1].X = -1f;
                    WiringWrapper._teleport[1].Y = -1f;
                    foreach(uint tile in triggerable[i]){
                        Point16 p = uint2Point(tile);
                        if(standardLamps[p.X,p.Y]) break;
                        HitWireSingle(tile);
                        /* Console.WriteLine($"Triggering from sync group: {i}, x:{p.X}, y:{p.Y}"); */
                    }
                    if (WiringWrapper._teleport[0].X >= 0f && WiringWrapper._teleport[1].X >= 0f)
                        WiringWrapper.Teleport();
                }
            }
            SyncPb();
        }

        HashSet<uint> lampsVisited = new HashSet<uint>();

        for (int g = 0; g < numGroups; ++g)
        {
            if (groupOutOfSync[g] != true) continue;
            //foreach (Point16 toggleablePoint in toggleable[g])
            for (int i = 0; i < toggleable[g].Length; ++i)
            {
                uint p = toggleable[g][i];
                if (lampsVisited.Contains(p)) continue;

                lampsVisited.Add(p);

                Point16 point = uint2Point(p);

                if (ShouldChange(point.X, point.Y))
                {
                    HitWireSingle(p);
                }
            }
            groupOutOfSync[g] = false;
        }
        if (Main.netMode == NetmodeID.Server)
        {
            SyncClients();
            Console.WriteLine("Sync complete");
        }
    }

}
