using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent.Events;
using Terraria.GameContent.UI;
using Terraria.ID;
using Terraria.Localization;
using WiringUtils;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;

/*
 * This is a wrapper for decompiled wiring code with some small changes to make it compatible with tModLoader
 * I really, really would like to do it a different way but I simply can't get the performance I want without
 * Overriding the default wiring implementation inside the mod. 
 * 
 * I tried for a long time with reflection to access internals (e.g. _wireSkip), but given this happens at
 * time sensitive points I can't take the performance hit doing that. 
 * 
 * ReLogic if you ever read this although I doubt it: If you really don't like me posting this snippet of
 * decompiled code then I guess I can remove it, although it's required for this mod to work. Plus what's
 * shown in the tModLoader repo in the diffs is more than this anyways
 * 
 */

namespace Terraria
{
    public static class WiringWrapper
    {
        public static bool blockPlayerTeleportationForOneIteration;
        public static bool running;
        public static Dictionary<Point16, bool> _wireSkip;
        public static DoubleStack<Point16> _wireList;
        public static DoubleStack<byte> _wireDirectionList;
        public static Dictionary<Point16, byte> _toProcess;
        // Changed this to concurrent, should keep an eye to make sure this doesn't have penalty
        public static ConcurrentQueue<Point16> _GatesCurrent;
        public static ConcurrentQueue<Point16> _LampsToCheck;
        public static ConcurrentDictionary<Point16, bool> _GatesDone;
        public static Dictionary<Point16, byte> _PixelBoxTriggers;
        public static Vector2[] _teleport;
        public const int MaxPump = 20;
        public static int[] _inPumpX;
        public static int[] _inPumpY;
        public static int _numInPump;
        public static int[] _outPumpX;
        public static int[] _outPumpY;
        public static int _numOutPump;
        public const int MaxMech = 1000;
        public static int[] _mechX;
        public static int[] _mechY;
        public static int _numMechs;
        public static int[] _mechTime;
        public static int _currentWireColor;
        public static int CurrentUser = 255;

        public static void SetCurrentUser(int plr = -1)
        {
            if (plr < 0 || plr > 255)
                plr = 255;

            if (Main.netMode == 0)
                plr = Main.myPlayer;

            CurrentUser = plr;
        }

        public static void Initialize()
        {
            _wireSkip = new Dictionary<Point16, bool>();
            _wireList = new DoubleStack<Point16>();
            _wireDirectionList = new DoubleStack<byte>();
            _toProcess = new Dictionary<Point16, byte>();
            _GatesCurrent = new ConcurrentQueue<Point16>();
            _GatesDone = new ConcurrentDictionary<Point16, bool>();
            _LampsToCheck = new ConcurrentQueue<Point16>();
            _PixelBoxTriggers = new Dictionary<Point16, byte>();
            _inPumpX = new int[20];
            _inPumpY = new int[20];
            _outPumpX = new int[20];
            _outPumpY = new int[20];
            _teleport = new Vector2[2];
            _mechX = new int[1000];
            _mechY = new int[1000];
            _mechTime = new int[1000];
        }

        public static void SkipWire(int x, int y)
        {
            //_wireSkip[new Point16(x, y)] = true;
        }

        public static void SkipWire(Point16 point)
        {
            //_wireSkip[point] = true;
        }

        public static void UpdateMech()
        {
            SetCurrentUser();
            for (int num = _numMechs - 1; num >= 0; num--)
            {
                _mechTime[num]--;
                if (!Main.tile[_mechX[num], _mechY[num]].IsActuated && Main.tile[_mechX[num], _mechY[num]].TileType == 144)
                {
                    if (Main.tile[_mechX[num], _mechY[num]].TileFrameY == 0)
                    {
                        _mechTime[num] = 0;
                    }
                    else
                    {
                        int num2 = Main.tile[_mechX[num], _mechY[num]].TileFrameX / 18;
                        switch (num2)
                        {
                            case 0:
                                num2 = 60;
                                break;
                            case 1:
                                num2 = 180;
                                break;
                            case 2:
                                num2 = 300;
                                break;
                            case 3:
                                num2 = 30;
                                break;
                            case 4:
                                num2 = 15;
                                break;
                        }

                        if (Math.IEEERemainder(_mechTime[num], num2) == 0.0)
                        {
                            _mechTime[num] = 18000;
                            TripWire(_mechX[num], _mechY[num], 1, 1);
                        }
                    }
                }

                if (_mechTime[num] <= 0)
                {
                    if (!Main.tile[_mechX[num], _mechY[num]].IsActuated && Main.tile[_mechX[num], _mechY[num]].TileType == 144)
                    {
                        Main.tile[_mechX[num], _mechY[num]].TileFrameY = 0;
                        NetMessage.SendTileSquare(-1, _mechX[num], _mechY[num]);
                    }

                    if (!Main.tile[_mechX[num], _mechY[num]].IsActuated && Main.tile[_mechX[num], _mechY[num]].TileType == 411)
                    {
                        Tile tile = Main.tile[_mechX[num], _mechY[num]];
                        int num3 = tile.TileFrameX % 36 / 18;
                        int num4 = tile.TileFrameY % 36 / 18;
                        int num5 = _mechX[num] - num3;
                        int num6 = _mechY[num] - num4;
                        int num7 = 36;
                        if (Main.tile[num5, num6].TileFrameX >= 36)
                            num7 = -36;

                        for (int i = num5; i < num5 + 2; i++)
                        {
                            for (int j = num6; j < num6 + 2; j++)
                            {
                                Main.tile[i, j].TileFrameX = (short)(Main.tile[i, j].TileFrameX + num7);
                            }
                        }

                        NetMessage.SendTileSquare(-1, num5, num6, 2, 2);
                    }

                    for (int k = num; k < _numMechs; k++)
                    {
                        _mechX[k] = _mechX[k + 1];
                        _mechY[k] = _mechY[k + 1];
                        _mechTime[k] = _mechTime[k + 1];
                    }

                    _numMechs--;
                }
            }
        }

        public static void HitSwitch(int i, int j)
        {
            if (!WorldGen.InWorld(i, j) || Main.tile[i, j] == null)
                return;

            if (Main.tile[i, j].TileType == 135 || Main.tile[i, j].TileType == 314 || Main.tile[i, j].TileType == 423 || Main.tile[i, j].TileType == 428 || Main.tile[i, j].TileType == 442 || Main.tile[i, j].TileType == 476)
            {
                //SoundEngine.PlaySound(28, i * 16, j * 16, 0);
                TripWire(i, j, 1, 1);
            }
            else if (Main.tile[i, j].TileType == 440)
            {
                //SoundEngine.PlaySound(28, i * 16 + 16, j * 16 + 16, 0);
                TripWire(i, j, 3, 3);
            }
            else if (Main.tile[i, j].TileType == 136)
            {
                if (Main.tile[i, j].TileFrameY == 0)
                    Main.tile[i, j].TileFrameY = 18;
                else
                    Main.tile[i, j].TileFrameY = 0;

                //SoundEngine.PlaySound(28, i * 16, j * 16, 0);
                TripWire(i, j, 1, 1);
            }
            else if (Main.tile[i, j].TileType == 443)
            {
                GeyserTrap(i, j);
            }
            else if (Main.tile[i, j].TileType == 144)
            {
                if (Main.tile[i, j].TileFrameY == 0)
                {
                    Main.tile[i, j].TileFrameY = 18;
                    if (Main.netMode != 1)
                        CheckMech(i, j, 18000);
                }
                else
                {
                    Main.tile[i, j].TileFrameY = 0;
                }

                //SoundEngine.PlaySound(28, i * 16, j * 16, 0);
            }
            else if (Main.tile[i, j].TileType == 441 || Main.tile[i, j].TileType == 468)
            {
                int num = Main.tile[i, j].TileFrameX / 18 * -1;
                int num2 = Main.tile[i, j].TileFrameY / 18 * -1;
                num %= 4;
                if (num < -1)
                    num += 2;

                num += i;
                num2 += j;
                //SoundEngine.PlaySound(28, i * 16, j * 16, 0);
                TripWire(num, num2, 2, 2);
            }
            else if (Main.tile[i, j].TileType == 467)
            {
                if (Main.tile[i, j].TileFrameX / 36 == 4)
                {
                    int num3 = Main.tile[i, j].TileFrameX / 18 * -1;
                    int num4 = Main.tile[i, j].TileFrameY / 18 * -1;
                    num3 %= 4;
                    if (num3 < -1)
                        num3 += 2;

                    num3 += i;
                    num4 += j;
                    //SoundEngine.PlaySound(28, i * 16, j * 16, 0);
                    TripWire(num3, num4, 2, 2);
                }
            }
            else
            {
                if (Main.tile[i, j].TileType != 132 && Main.tile[i, j].TileType != 411)
                    return;

                short num5 = 36;
                int num6 = Main.tile[i, j].TileFrameX / 18 * -1;
                int num7 = Main.tile[i, j].TileFrameY / 18 * -1;
                num6 %= 4;
                if (num6 < -1)
                {
                    num6 += 2;
                    num5 = -36;
                }

                num6 += i;
                num7 += j;
                if (Main.netMode != 1 && Main.tile[num6, num7].TileType == 411)
                    CheckMech(num6, num7, 60);

                for (int k = num6; k < num6 + 2; k++)
                {
                    for (int l = num7; l < num7 + 2; l++)
                    {
                        if (Main.tile[k, l].TileType == 132 || Main.tile[k, l].TileType == 411)
                            Main.tile[k, l].TileFrameX += num5;
                    }
                }

                WorldGen.TileFrame(num6, num7);
                //SoundEngine.PlaySound(28, i * 16, j * 16, 0);
                TripWire(num6, num7, 2, 2);
            }
        }

        public static void PokeLogicGate(int lampX, int lampY)
        {
            if (Main.netMode != 1)
            {
                _LampsToCheck.Enqueue(new Point16(lampX, lampY));
                LogicGatePass();
            }
        }

        public static bool Actuate(int i, int j)
        {
            Tile tile = Main.tile[i, j];
            if (!tile.HasActuator)
                return false;

            if (tile.IsActuated)
                ReActive(i, j);
            else
                DeActive(i, j);

            return true;
        }

        public static void ActuateForced(int i, int j)
        {
            if (Main.tile[i, j].IsActuated)
                ReActive(i, j);
            else
                DeActive(i, j);
        }

        public static void MassWireOperation(Point ps, Point pe, Player master)
        {
            int wireCount = 0;
            int actuatorCount = 0;
            for (int i = 0; i < 58; i++)
            {
                if (master.inventory[i].type == 530)
                    wireCount += master.inventory[i].stack;

                if (master.inventory[i].type == 849)
                    actuatorCount += master.inventory[i].stack;
            }

            int num = wireCount;
            int num2 = actuatorCount;
            MassWireOperationInner(master, ps, pe, master.Center, master.direction == 1, ref wireCount, ref actuatorCount);
            int num3 = num - wireCount;
            int num4 = num2 - actuatorCount;
            if (Main.netMode == 2)
            {
                NetMessage.SendData(110, master.whoAmI, -1, null, 530, num3, master.whoAmI);
                NetMessage.SendData(110, master.whoAmI, -1, null, 849, num4, master.whoAmI);
                return;
            }

            for (int j = 0; j < num3; j++)
            {
                master.ConsumeItem(530);
            }

            for (int k = 0; k < num4; k++)
            {
                master.ConsumeItem(849);
            }
        }

        private static bool CheckMech(int i, int j, int time)
        {
            for (int k = 0; k < _numMechs; k++)
            {
                if (_mechX[k] == i && _mechY[k] == j)
                    return false;
            }

            if (_numMechs < 999)
            {
                _mechX[_numMechs] = i;
                _mechY[_numMechs] = j;
                _mechTime[_numMechs] = time;
                _numMechs++;
                return true;
            }

            return false;
        }

        private static void XferWater()
        {
            for (int i = 0; i < _numInPump; i++)
            {
                int num = _inPumpX[i];
                int num2 = _inPumpY[i];
                int liquid = Main.tile[num, num2].LiquidType;
                if (liquid <= 0)
                    continue;

                bool flag = Main.tile[num, num2].LiquidType == LiquidID.Lava;
                bool flag2 = Main.tile[num, num2].LiquidType == LiquidID.Honey;
                for (int j = 0; j < _numOutPump; j++)
                {
                    int num3 = _outPumpX[j];
                    int num4 = _outPumpY[j];
                    int liquid2 = Main.tile[num3, num4].LiquidType;
                    if (liquid2 >= 255)
                        continue;

                    bool flag3 = Main.tile[num3, num4].LiquidType == LiquidID.Lava;
                    bool flag4 = Main.tile[num3, num4].LiquidType == LiquidID.Honey;
                    if (liquid2 == 0)
                    {
                        flag3 = flag;
                        flag4 = flag2;
                    }

                    if (flag == flag3 && flag2 == flag4)
                    {
                        int num5 = liquid;
                        if (num5 + liquid2 > 255)
                            num5 = 255 - liquid2;

                        // Pumps are weird with new api, don't feel like figuring them out
                        //Main.tile[num3, num4].LiquidType += (byte)num5;
                        //Main.tile[num, num2].LiquidType -= (byte)num5;
                        liquid = Main.tile[num, num2].LiquidType;
                        //Main.tile[num3, num4].lava(flag);
                        //Main.tile[num3, num4].honey(flag2);
                        WorldGen.SquareTileFrame(num3, num4);
                        if (Main.tile[num, num2].LiquidType == 0)
                        {
                            //Main.tile[num, num2].lava(lava: false);
                            WorldGen.SquareTileFrame(num, num2);
                            break;
                        }
                    }
                }

                WorldGen.SquareTileFrame(num, num2);
            }
        }

        private static void TripWire(int left, int top, int width, int height)
        {
            if (Main.netMode == 1)
                return;

            running = true;
            if (_wireList.Count != 0)
                _wireList.Clear(quickClear: true);

            if (_wireDirectionList.Count != 0)
                _wireDirectionList.Clear(quickClear: true);

            Vector2[] array = new Vector2[8];
            int num = 0;
            for (int i = left; i < left + width; i++)
            {
                for (int j = top; j < top + height; j++)
                {
                    Point16 back = new Point16(i, j);
                    Tile tile = Main.tile[i, j];
                    if (tile != null && tile.RedWire)
                        _wireList.PushBack(back);
                }
            }

            _teleport[0].X = -1f;
            _teleport[0].Y = -1f;
            _teleport[1].X = -1f;
            _teleport[1].Y = -1f;
            if (_wireList.Count > 0)
            {
                _numInPump = 0;
                _numOutPump = 0;
                HitWire(_wireList, 1);
                if (_numInPump > 0 && _numOutPump > 0)
                    XferWater();
            }

            array[num++] = _teleport[0];
            array[num++] = _teleport[1];
            for (int k = left; k < left + width; k++)
            {
                for (int l = top; l < top + height; l++)
                {
                    Point16 back = new Point16(k, l);
                    Tile tile2 = Main.tile[k, l];
                    if (tile2 != null && tile2.BlueWire)
                        _wireList.PushBack(back);
                }
            }

            _teleport[0].X = -1f;
            _teleport[0].Y = -1f;
            _teleport[1].X = -1f;
            _teleport[1].Y = -1f;
            if (_wireList.Count > 0)
            {
                _numInPump = 0;
                _numOutPump = 0;
                HitWire(_wireList, 2);
                if (_numInPump > 0 && _numOutPump > 0)
                    XferWater();
            }

            array[num++] = _teleport[0];
            array[num++] = _teleport[1];
            _teleport[0].X = -1f;
            _teleport[0].Y = -1f;
            _teleport[1].X = -1f;
            _teleport[1].Y = -1f;
            for (int m = left; m < left + width; m++)
            {
                for (int n = top; n < top + height; n++)
                {
                    Point16 back = new Point16(m, n);
                    Tile tile3 = Main.tile[m, n];
                    if (tile3 != null && tile3.GreenWire)
                        _wireList.PushBack(back);
                }
            }

            if (_wireList.Count > 0)
            {
                _numInPump = 0;
                _numOutPump = 0;
                HitWire(_wireList, 3);
                if (_numInPump > 0 && _numOutPump > 0)
                    XferWater();
            }

            array[num++] = _teleport[0];
            array[num++] = _teleport[1];
            _teleport[0].X = -1f;
            _teleport[0].Y = -1f;
            _teleport[1].X = -1f;
            _teleport[1].Y = -1f;
            for (int num2 = left; num2 < left + width; num2++)
            {
                for (int num3 = top; num3 < top + height; num3++)
                {
                    Point16 back = new Point16(num2, num3);
                    Tile tile4 = Main.tile[num2, num3];
                    if (tile4 != null && tile4.YellowWire)
                        _wireList.PushBack(back);
                }
            }

            if (_wireList.Count > 0)
            {
                _numInPump = 0;
                _numOutPump = 0;
                HitWire(_wireList, 4);
                if (_numInPump > 0 && _numOutPump > 0)
                    XferWater();
            }

            array[num++] = _teleport[0];
            array[num++] = _teleport[1];
            running = false;
            for (int num4 = 0; num4 < 8; num4 += 2)
            {
                _teleport[0] = array[num4];
                _teleport[1] = array[num4 + 1];
                if (_teleport[0].X >= 0f && _teleport[1].X >= 0f)
                    Teleport();
            }

            PixelBoxPass();
            LogicGatePass();
        }

        private static void PixelBoxPass()
        {
            foreach (KeyValuePair<Point16, byte> pixelBoxTrigger in _PixelBoxTriggers)
            {
                if (pixelBoxTrigger.Value == 2)
                    continue;

                if (pixelBoxTrigger.Value == 1)
                {
                    if (Main.tile[pixelBoxTrigger.Key.X, pixelBoxTrigger.Key.Y].TileFrameX != 0)
                    {
                        Main.tile[pixelBoxTrigger.Key.X, pixelBoxTrigger.Key.Y].TileFrameX = 0;
                        NetMessage.SendTileSquare(-1, pixelBoxTrigger.Key.X, pixelBoxTrigger.Key.Y);
                    }
                }
                else if (pixelBoxTrigger.Value == 3 && Main.tile[pixelBoxTrigger.Key.X, pixelBoxTrigger.Key.Y].TileFrameX != 18)
                {
                    Main.tile[pixelBoxTrigger.Key.X, pixelBoxTrigger.Key.Y].TileFrameX = 18;
                    NetMessage.SendTileSquare(-1, pixelBoxTrigger.Key.X, pixelBoxTrigger.Key.Y);
                }
            }

            _PixelBoxTriggers.Clear();
        }

        // Heavily modified from vanilla to accomodate threading
        private static void LogicGatePass()
        {
            if (_GatesCurrent.Count != 0)
                return;

            _GatesDone.Clear();
            while (_LampsToCheck.Count > 0)
            {
                if (Accelerator.threading)
                {
                    Parallel.ForEach(_LampsToCheck, lamp =>
                    {
                        CheckLogicGate(lamp.X, lamp.Y);
                    });
                    // Leaves capacity as per 
                    // https://learn.microsoft.com/en-us/dotnet/api/system.collections.queue.clear?view=net-8.0#system-collections-queue-clear
                }
                else
                {
                    foreach (Point16 point in _LampsToCheck)
                    {
                        CheckLogicGate(point.X, point.Y);
                    }
                }
                _LampsToCheck.Clear();

                // Only have to iterate once over gatescurrent since TripWire should only enqueue to _LampsToCheck
                if (Accelerator.threading)
                {
                    Parallel.ForEach(_GatesCurrent, point =>
                    {
                        _GatesDone[point] = true;
                        Accelerator.HitPoint(point);
                    });
                }
                else
                {
                    foreach (Point16 point in _GatesCurrent)
                    {
                        _GatesDone[point] = true;
                        // TODO: fix teleportation
                        Accelerator.HitPoint(point);
                    }
                }
                _GatesCurrent.Clear();
            }

            _GatesDone.Clear();


            if (blockPlayerTeleportationForOneIteration)
            {
                // Other files check this so we need to make sure it stays in sync
                blockPlayerTeleportationForOneIteration = false;
                Wiring.blockPlayerTeleportationForOneIteration = false;
            }
        }

        private static void CheckLogicGate(int lampX, int lampY)
        {
            /*
             * Custom addition with optimized faulty logic gate handling
             */
            if (Accelerator.standardLamps[lampX, lampY])
            {
                Accelerator.CheckFaultyGate(lampX, lampY);
                return;
            }

            if (!WorldGen.InWorld(lampX, lampY, 1))
                return;

            int gateY = lampY;
            Tile gateTile;

            while (true)
            {
                if (gateY < Main.maxTilesY)
                {
                    gateTile = Main.tile[lampX, gateY];
                    if (gateTile.IsActuated)
                        return;

                    if (gateTile.TileType == 420)
                        break;

                    if (gateTile.TileType != 419)
                        return;

                    gateY++;
                    continue;
                }

                return;
            }

            _GatesDone.TryGetValue(new Point16(lampX, gateY), out bool value);
            int gateType = gateTile.TileFrameY / 18;
            bool gateOn = gateTile.TileFrameX == 18;
            bool gateFaulty = gateTile.TileFrameX == 36;
            if (gateType < 0)
                return;

            int numLamps = 0;
            int numOn = 0;
            bool lampFaulty = false;
            for (int lampIterBack = gateY - 1; lampIterBack > 0; lampIterBack--)
            {
                Tile curLamp = Main.tile[lampX, lampIterBack];
                if (curLamp.IsActuated || curLamp.TileType != 419)
                    break;

                if (curLamp.TileFrameX == 36)
                {
                    lampFaulty = true;
                    break;
                }

                numLamps++;
                numOn += (curLamp.TileFrameX == 18).ToInt();
            }

            bool conditionFulfilled = false;
            switch (gateType)
            {
                default:
                    return;
                case 0:
                    conditionFulfilled = (numLamps == numOn);
                    break;
                case 2:
                    conditionFulfilled = (numLamps != numOn);
                    break;
                case 1:
                    conditionFulfilled = (numOn > 0);
                    break;
                case 3:
                    conditionFulfilled = (numOn == 0);
                    break;
                case 4:
                    conditionFulfilled = (numOn == 1);
                    break;
                case 5:
                    conditionFulfilled = (numOn != 1);
                    break;
            }

            bool faultyStateWrong = !lampFaulty && gateFaulty;
            bool faultyLampTriggered = false;
            if (lampFaulty && Framing.GetTileSafely(lampX, lampY).TileFrameX == 36)
                faultyLampTriggered = true;

            if (!(conditionFulfilled != gateOn || faultyStateWrong || faultyLampTriggered))
                return;

            _ = gateTile.TileFrameX % 18 / 18;
            gateTile.TileFrameX = (short)(18 * conditionFulfilled.ToInt());
            if (lampFaulty)
                gateTile.TileFrameX = 36;

            SkipWire(lampX, gateY);
            WorldGen.SquareTileFrame(lampX, gateY); //3182, 216
            NetMessage.SendTileSquare(-1, lampX, gateY);
            bool flag7 = !lampFaulty || faultyLampTriggered;
            if (faultyLampTriggered)
            {
                if (numOn == 0 || numLamps == 0)
                    flag7 = false;

                flag7 = (Main.rand.NextFloat() < (float)numOn / (float)numLamps);
            }

            if (faultyStateWrong)
                flag7 = false;

            if (flag7)
            {
                if (!value)
                {
                    // Changed from _GatesNext to _GatesCurrent since the duplication isn't necessary
                    _GatesCurrent.Enqueue(new Point16(lampX, gateY));
                    return;
                }

                Vector2 position = new Vector2(lampX, gateY) * 16f - new Vector2(10f);
                Utils.PoofOfSmoke(position);
                NetMessage.SendData(106, -1, -1, null, (int)position.X, position.Y);
            }
        }

        private static void HitWire(DoubleStack<Point16> next, int wireType)
        {

            // Changed to wrapper
            Accelerator.HitWire(next, wireType);
        }

        public static IEntitySource GetProjectileSource(int sourceTileX, int sourceTileY) => new EntitySource_Wiring(sourceTileX, sourceTileY);
        public static IEntitySource GetNPCSource(int sourceTileX, int sourceTileY) => new EntitySource_Wiring(sourceTileX, sourceTileY);
        public static IEntitySource GetItemSource(int sourceTileX, int sourceTileY) => new EntitySource_Wiring(sourceTileX, sourceTileY);

        public static void HitWireSingle(int i, int j)
        {
            Tile tile = Main.tile[i, j];
            bool? forcedStateWhereTrueIsOn = null;
            bool doSkipWires = true;
            int type = tile.TileType;
            if (tile.HasActuator)
                ActuateForced(i, j);

            if (tile.IsActuated)
                return;

            switch (type)
            {
                case 144:
                    HitSwitch(i, j);
                    WorldGen.SquareTileFrame(i, j);
                    NetMessage.SendTileSquare(-1, i, j);
                    break;
                case 421:
                    if (!tile.HasActuator)
                    {
                        tile.TileType = 422;
                        WorldGen.SquareTileFrame(i, j);
                        NetMessage.SendTileSquare(-1, i, j);
                    }
                    break;
                case 422:
                    if (!tile.HasActuator)
                    {
                        tile.TileType = 421;
                        WorldGen.SquareTileFrame(i, j);
                        NetMessage.SendTileSquare(-1, i, j);
                    }
                    break;
            }

            if (type >= 255 && type <= 268)
            {
                if (!tile.HasActuator)
                {
                    if (type >= 262)
                        tile.TileType -= 7;
                    else
                        tile.TileType += 7;

                    WorldGen.SquareTileFrame(i, j);
                    NetMessage.SendTileSquare(-1, i, j);
                }

                return;
            }

            if (type == 419)
            {
                int num = 18;
                if (tile.TileFrameX >= num)
                    num = -num;

                if (tile.TileFrameX == 36)
                    num = 0;

                SkipWire(i, j);
                tile.TileFrameX = (short)(tile.TileFrameX + num);
                WorldGen.SquareTileFrame(i, j);
                NetMessage.SendTileSquare(-1, i, j);
                _LampsToCheck.Enqueue(new Point16(i, j));
                return;
            }

            if (type == 406)
            {
                int num2 = tile.TileFrameX % 54 / 18;
                int num3 = tile.TileFrameY % 54 / 18;
                int num4 = i - num2;
                int num5 = j - num3;
                int num6 = 54;
                if (Main.tile[num4, num5].TileFrameY >= 108)
                    num6 = -108;

                for (int k = num4; k < num4 + 3; k++)
                {
                    for (int l = num5; l < num5 + 3; l++)
                    {
                        SkipWire(k, l);
                        Main.tile[k, l].TileFrameY = (short)(Main.tile[k, l].TileFrameY + num6);
                    }
                }

                NetMessage.SendTileSquare(-1, num4 + 1, num5 + 1, 3);
                return;
            }

            if (type == 452)
            {
                int num7 = tile.TileFrameX % 54 / 18;
                int num8 = tile.TileFrameY % 54 / 18;
                int num9 = i - num7;
                int num10 = j - num8;
                int num11 = 54;
                if (Main.tile[num9, num10].TileFrameX >= 54)
                    num11 = -54;

                for (int m = num9; m < num9 + 3; m++)
                {
                    for (int n = num10; n < num10 + 3; n++)
                    {
                        SkipWire(m, n);
                        Main.tile[m, n].TileFrameX = (short)(Main.tile[m, n].TileFrameX + num11);
                    }
                }

                NetMessage.SendTileSquare(-1, num9 + 1, num10 + 1, 3);
                return;
            }

            if (type == 411)
            {
                int num12 = tile.TileFrameX % 36 / 18;
                int num13 = tile.TileFrameY % 36 / 18;
                int num14 = i - num12;
                int num15 = j - num13;
                int num16 = 36;
                if (Main.tile[num14, num15].TileFrameX >= 36)
                    num16 = -36;

                for (int num17 = num14; num17 < num14 + 2; num17++)
                {
                    for (int num18 = num15; num18 < num15 + 2; num18++)
                    {
                        SkipWire(num17, num18);
                        Main.tile[num17, num18].TileFrameX = (short)(Main.tile[num17, num18].TileFrameX + num16);
                    }
                }

                NetMessage.SendTileSquare(-1, num14, num15, 2, 2);
                return;
            }

            if (type == 425)
            {
                int num19 = tile.TileFrameX % 36 / 18;
                int num20 = tile.TileFrameY % 36 / 18;
                int num21 = i - num19;
                int num22 = j - num20;
                for (int num23 = num21; num23 < num21 + 2; num23++)
                {
                    for (int num24 = num22; num24 < num22 + 2; num24++)
                    {
                        SkipWire(num23, num24);
                    }
                }

                if (Main.AnnouncementBoxDisabled)
                    return;

                Color pink = Color.Pink;
                int num25 = Sign.ReadSign(num21, num22, CreateIfMissing: false);
                if (num25 == -1 || Main.sign[num25] == null || string.IsNullOrWhiteSpace(Main.sign[num25].text))
                    return;

                if (Main.AnnouncementBoxRange == -1)
                {
                    if (Main.netMode == 0)
                        Main.NewTextMultiline(Main.sign[num25].text, force: false, pink, 460);
                    else if (Main.netMode == 2)
                        NetMessage.SendData(107, -1, -1, NetworkText.FromLiteral(Main.sign[num25].text), 255, (int)pink.R, (int)pink.G, (int)pink.B, 460);
                }
                else if (Main.netMode == 0)
                {
                    if (Main.player[Main.myPlayer].Distance(new Vector2(num21 * 16 + 16, num22 * 16 + 16)) <= (float)Main.AnnouncementBoxRange)
                        Main.NewTextMultiline(Main.sign[num25].text, force: false, pink, 460);
                }
                else
                {
                    if (Main.netMode != 2)
                        return;

                    for (int num26 = 0; num26 < 255; num26++)
                    {
                        if (Main.player[num26].active && Main.player[num26].Distance(new Vector2(num21 * 16 + 16, num22 * 16 + 16)) <= (float)Main.AnnouncementBoxRange)
                            NetMessage.SendData(107, num26, -1, NetworkText.FromLiteral(Main.sign[num25].text), 255, (int)pink.R, (int)pink.G, (int)pink.B, 460);
                    }
                }

                return;
            }

            if (type == 405)
            {
                ToggleFirePlace(i, j, tile, forcedStateWhereTrueIsOn, doSkipWires);
                return;
            }

            if (type == 209)
            {
                int num27 = tile.TileFrameX % 72 / 18;
                int num28 = tile.TileFrameY % 54 / 18;
                int num29 = i - num27;
                int num30 = j - num28;
                int num31 = tile.TileFrameY / 54;
                int num32 = tile.TileFrameX / 72;
                int num33 = -1;
                if (num27 == 1 || num27 == 2)
                    num33 = num28;

                int num34 = 0;
                if (num27 == 3)
                    num34 = -54;

                if (num27 == 0)
                    num34 = 54;

                if (num31 >= 8 && num34 > 0)
                    num34 = 0;

                if (num31 == 0 && num34 < 0)
                    num34 = 0;

                bool flag = false;
                if (num34 != 0)
                {
                    for (int num35 = num29; num35 < num29 + 4; num35++)
                    {
                        for (int num36 = num30; num36 < num30 + 3; num36++)
                        {
                            SkipWire(num35, num36);
                            Main.tile[num35, num36].TileFrameY = (short)(Main.tile[num35, num36].TileFrameY + num34);
                        }
                    }

                    flag = true;
                }

                if ((num32 == 3 || num32 == 4) && (num33 == 0 || num33 == 1))
                {
                    num34 = ((num32 == 3) ? 72 : (-72));
                    for (int num37 = num29; num37 < num29 + 4; num37++)
                    {
                        for (int num38 = num30; num38 < num30 + 3; num38++)
                        {
                            SkipWire(num37, num38);
                            Main.tile[num37, num38].TileFrameX = (short)(Main.tile[num37, num38].TileFrameX + num34);
                        }
                    }

                    flag = true;
                }

                if (flag)
                    NetMessage.SendTileSquare(-1, num29, num30, 4, 3);

                if (num33 != -1)
                {
                    bool flag2 = true;
                    if ((num32 == 3 || num32 == 4) && num33 < 2)
                        flag2 = false;

                    if (CheckMech(num29, num30, 30) && flag2)
                        WorldGen.ShootFromCannon(num29, num30, num31, num32 + 1, 0, 0f, CurrentUser, fromWire: true);
                }

                return;
            }

            if (type == 212)
            {
                int num39 = tile.TileFrameX % 54 / 18;
                int num40 = tile.TileFrameY % 54 / 18;
                int num41 = i - num39;
                int num42 = j - num40;
                int num43 = tile.TileFrameX / 54;
                int num44 = -1;
                if (num39 == 1)
                    num44 = num40;

                int num45 = 0;
                if (num39 == 0)
                    num45 = -54;

                if (num39 == 2)
                    num45 = 54;

                if (num43 >= 1 && num45 > 0)
                    num45 = 0;

                if (num43 == 0 && num45 < 0)
                    num45 = 0;

                bool flag3 = false;
                if (num45 != 0)
                {
                    for (int num46 = num41; num46 < num41 + 3; num46++)
                    {
                        for (int num47 = num42; num47 < num42 + 3; num47++)
                        {
                            SkipWire(num46, num47);
                            Main.tile[num46, num47].TileFrameX = (short)(Main.tile[num46, num47].TileFrameX + num45);
                        }
                    }

                    flag3 = true;
                }

                if (flag3)
                    NetMessage.SendTileSquare(-1, num41, num42, 3, 3);

                if (num44 != -1 && CheckMech(num41, num42, 10))
                {
                    float num48 = 12f + (float)Main.rand.Next(450) * 0.01f;
                    float num49 = Main.rand.Next(85, 105);
                    float num50 = Main.rand.Next(-35, 11);
                    int type2 = 166;
                    int damage = 0;
                    float knockBack = 0f;
                    Vector2 vector = new Vector2((num41 + 2) * 16 - 8, (num42 + 2) * 16 - 8);
                    if (tile.TileFrameX / 54 == 0)
                    {
                        num49 *= -1f;
                        vector.X -= 12f;
                    }
                    else
                    {
                        vector.X += 12f;
                    }

                    float num51 = num49;
                    float num52 = num50;
                    float num53 = (float)Math.Sqrt(num51 * num51 + num52 * num52);
                    num53 = num48 / num53;
                    num51 *= num53;
                    num52 *= num53;
                    Projectile.NewProjectile(GetProjectileSource(num41, num42), vector.X, vector.Y, num51, num52, type2, damage, knockBack, CurrentUser);
                }

                return;
            }

            if (type == 215)
            {
                ToggleCampFire(i, j, tile, forcedStateWhereTrueIsOn, doSkipWires);
                return;
            }

            if (type == 130)
            {
                if (Main.tile[i, j - 1] == null || Main.tile[i, j - 1].IsActuated || (!TileID.Sets.BasicChest[Main.tile[i, j - 1].TileType] && !TileID.Sets.BasicChestFake[Main.tile[i, j - 1].TileType] && Main.tile[i, j - 1].TileType != 88))
                {
                    tile.TileType = 131;
                    WorldGen.SquareTileFrame(i, j);
                    NetMessage.SendTileSquare(-1, i, j);
                }

                return;
            }

            if (type == 131)
            {
                tile.TileType = 130;
                WorldGen.SquareTileFrame(i, j);
                NetMessage.SendTileSquare(-1, i, j);
                return;
            }

            if (type == 387 || type == 386)
            {
                bool value = type == 387;
                int num54 = WorldGen.ShiftTrapdoor(i, j, playerAbove: true).ToInt();
                if (num54 == 0)
                    num54 = -WorldGen.ShiftTrapdoor(i, j, playerAbove: false).ToInt();

                if (num54 != 0)
                    NetMessage.SendData(19, -1, -1, null, 3 - value.ToInt(), i, j, num54);

                return;
            }

            if (type == 389 || type == 388)
            {
                bool flag4 = type == 389;
                WorldGen.ShiftTallGate(i, j, flag4);
                NetMessage.SendData(19, -1, -1, null, 4 + flag4.ToInt(), i, j);
                return;
            }

            if (type == 11)
            {
                if (WorldGen.CloseDoor(i, j, forced: true))
                    NetMessage.SendData(19, -1, -1, null, 1, i, j);

                return;
            }

            if (type == 10)
            {
                int num55 = 1;
                if (Main.rand.Next(2) == 0)
                    num55 = -1;

                if (!WorldGen.OpenDoor(i, j, num55))
                {
                    if (WorldGen.OpenDoor(i, j, -num55))
                        NetMessage.SendData(19, -1, -1, null, 0, i, j, -num55);
                }
                else
                {
                    NetMessage.SendData(19, -1, -1, null, 0, i, j, num55);
                }

                return;
            }

            if (type == 216)
            {
                WorldGen.LaunchRocket(i, j, fromWiring: true);
                SkipWire(i, j);
                return;
            }

            if (type == 497 || (type == 15 && tile.TileFrameY / 40 == 1) || (type == 15 && tile.TileFrameY / 40 == 20))
            {
                int num56 = j - tile.TileFrameY % 40 / 18;
                SkipWire(i, num56);
                SkipWire(i, num56 + 1);
                if (CheckMech(i, num56, 60))
                    Projectile.NewProjectile(GetProjectileSource(i, num56), i * 16 + 8, num56 * 16 + 12, 0f, 0f, 733, 0, 0f, Main.myPlayer);

                return;
            }

            switch (type)
            {
                case 335:
                    {
                        int num144 = j - tile.TileFrameY / 18;
                        int num145 = i - tile.TileFrameX / 18;
                        SkipWire(num145, num144);
                        SkipWire(num145, num144 + 1);
                        SkipWire(num145 + 1, num144);
                        SkipWire(num145 + 1, num144 + 1);
                        if (CheckMech(num145, num144, 30))
                            WorldGen.LaunchRocketSmall(num145, num144, fromWiring: true);

                        break;
                    }
                case 338:
                    {
                        int num63 = j - tile.TileFrameY / 18;
                        int num64 = i - tile.TileFrameX / 18;
                        SkipWire(num64, num63);
                        SkipWire(num64, num63 + 1);
                        if (!CheckMech(num64, num63, 30))
                            break;

                        bool flag5 = false;
                        for (int num65 = 0; num65 < 1000; num65++)
                        {
                            if (Main.projectile[num65].active && Main.projectile[num65].aiStyle == 73 && Main.projectile[num65].ai[0] == (float)num64 && Main.projectile[num65].ai[1] == (float)num63)
                            {
                                flag5 = true;
                                break;
                            }
                        }

                        if (!flag5)
                        {
                            int type3 = 419 + Main.rand.Next(4);
                            Projectile.NewProjectile(GetProjectileSource(num64, num63), num64 * 16 + 8, num63 * 16 + 2, 0f, 0f, type3, 0, 0f, Main.myPlayer, num64, num63);
                        }

                        break;
                    }
                case 235:
                    {
                        int num95 = i - tile.TileFrameX / 18;
                        if (tile.WallType == 87 && (double)j > Main.worldSurface && !NPC.downedPlantBoss)
                            break;

                        if (_teleport[0].X == -1f)
                        {
                            _teleport[0].X = num95;
                            _teleport[0].Y = j;
                            if (tile.IsHalfBlock)
                                _teleport[0].Y += 0.5f;
                        }
                        else if (_teleport[0].X != (float)num95 || _teleport[0].Y != (float)j)
                        {
                            _teleport[1].X = num95;
                            _teleport[1].Y = j;
                            if (tile.IsHalfBlock)
                                _teleport[1].Y += 0.5f;
                        }

                        break;
                    }
                case 4:
                    ToggleTorch(i, j, tile, forcedStateWhereTrueIsOn);
                    break;
                case 429:
                    {
                        int num66 = Main.tile[i, j].TileFrameX / 18;
                        bool flag6 = num66 % 2 >= 1;
                        bool flag7 = num66 % 4 >= 2;
                        bool flag8 = num66 % 8 >= 4;
                        bool flag9 = num66 % 16 >= 8;
                        bool flag10 = false;
                        short num67 = 0;
                        switch (_currentWireColor)
                        {
                            case 1:
                                num67 = 18;
                                flag10 = !flag6;
                                break;
                            case 2:
                                num67 = 72;
                                flag10 = !flag8;
                                break;
                            case 3:
                                num67 = 36;
                                flag10 = !flag7;
                                break;
                            case 4:
                                num67 = 144;
                                flag10 = !flag9;
                                break;
                        }

                        if (flag10)
                            tile.TileFrameX += num67;
                        else
                            tile.TileFrameX -= num67;

                        NetMessage.SendTileSquare(-1, i, j);
                        break;
                    }
                case 149:
                    ToggleHolidayLight(i, j, tile, forcedStateWhereTrueIsOn);
                    break;
                case 244:
                    {
                        int num119;
                        for (num119 = tile.TileFrameX / 18; num119 >= 3; num119 -= 3)
                        {
                        }

                        int num120;
                        for (num120 = tile.TileFrameY / 18; num120 >= 3; num120 -= 3)
                        {
                        }

                        int num121 = i - num119;
                        int num122 = j - num120;
                        int num123 = 54;
                        if (Main.tile[num121, num122].TileFrameX >= 54)
                            num123 = -54;

                        for (int num124 = num121; num124 < num121 + 3; num124++)
                        {
                            for (int num125 = num122; num125 < num122 + 2; num125++)
                            {
                                SkipWire(num124, num125);
                                Main.tile[num124, num125].TileFrameX = (short)(Main.tile[num124, num125].TileFrameX + num123);
                            }
                        }

                        NetMessage.SendTileSquare(-1, num121, num122, 3, 2);
                        break;
                    }
                case 565:
                    {
                        int num86;
                        for (num86 = tile.TileFrameX / 18; num86 >= 2; num86 -= 2)
                        {
                        }

                        int num87;
                        for (num87 = tile.TileFrameY / 18; num87 >= 2; num87 -= 2)
                        {
                        }

                        int num88 = i - num86;
                        int num89 = j - num87;
                        int num90 = 36;
                        if (Main.tile[num88, num89].TileFrameX >= 36)
                            num90 = -36;

                        for (int num91 = num88; num91 < num88 + 2; num91++)
                        {
                            for (int num92 = num89; num92 < num89 + 2; num92++)
                            {
                                SkipWire(num91, num92);
                                Main.tile[num91, num92].TileFrameX = (short)(Main.tile[num91, num92].TileFrameX + num90);
                            }
                        }

                        NetMessage.SendTileSquare(-1, num88, num89, 2, 2);
                        break;
                    }
                case 42:
                    ToggleHangingLantern(i, j, tile, forcedStateWhereTrueIsOn, doSkipWires);
                    break;
                case 93:
                    ToggleLamp(i, j, tile, forcedStateWhereTrueIsOn, doSkipWires);
                    break;
                case 95:
                case 100:
                case 126:
                case 173:
                case 564:
                    Toggle2x2Light(i, j, tile, forcedStateWhereTrueIsOn, doSkipWires);
                    break;
                case 593:
                    {
                        SkipWire(i, j);
                        short num93 = (short)((Main.tile[i, j].TileFrameX != 0) ? (-18) : 18);
                        Main.tile[i, j].TileFrameX += num93;
                        if (Main.netMode == 2)
                            NetMessage.SendTileSquare(-1, i, j, 1, 1);

                        int num94 = (num93 > 0) ? 4 : 3;
                        Animation.NewTemporaryAnimation(num94, 593, i, j);
                        NetMessage.SendTemporaryAnimation(-1, num94, 593, i, j);
                        break;
                    }
                case 594:
                    {
                        int num68;
                        for (num68 = tile.TileFrameY / 18; num68 >= 2; num68 -= 2)
                        {
                        }

                        num68 = j - num68;
                        int num69 = tile.TileFrameX / 18;
                        if (num69 > 1)
                            num69 -= 2;

                        num69 = i - num69;
                        SkipWire(num69, num68);
                        SkipWire(num69, num68 + 1);
                        SkipWire(num69 + 1, num68);
                        SkipWire(num69 + 1, num68 + 1);
                        short num70 = (short)((Main.tile[num69, num68].TileFrameX != 0) ? (-36) : 36);
                        for (int num71 = 0; num71 < 2; num71++)
                        {
                            for (int num72 = 0; num72 < 2; num72++)
                            {
                                Main.tile[num69 + num71, num68 + num72].TileFrameX += num70;
                            }
                        }

                        if (Main.netMode == 2)
                            NetMessage.SendTileSquare(-1, num69, num68, 2, 2);

                        int num73 = (num70 > 0) ? 4 : 3;
                        Animation.NewTemporaryAnimation(num73, 594, num69, num68);
                        NetMessage.SendTemporaryAnimation(-1, num73, 594, num69, num68);
                        break;
                    }
                case 34:
                    ToggleChandelier(i, j, tile, forcedStateWhereTrueIsOn, doSkipWires);
                    break;
                case 314:
                    if (CheckMech(i, j, 5))
                        Minecart.FlipSwitchTrack(i, j);
                    break;
                case 33:
                case 49:
                case 174:
                case 372:
                    ToggleCandle(i, j, tile, forcedStateWhereTrueIsOn);
                    break;
                case 92:
                    ToggleLampPost(i, j, tile, forcedStateWhereTrueIsOn, doSkipWires);
                    break;
                case 137:
                    {
                        int num126 = tile.TileFrameY / 18;
                        Vector2 vector3 = Vector2.Zero;
                        float speedX = 0f;
                        float speedY = 0f;
                        int num127 = 0;
                        int damage3 = 0;
                        switch (num126)
                        {
                            case 0:
                            case 1:
                            case 2:
                                if (CheckMech(i, j, 200))
                                {
                                    int num135 = (tile.TileFrameX == 0) ? (-1) : ((tile.TileFrameX == 18) ? 1 : 0);
                                    int num136 = (tile.TileFrameX >= 36) ? ((tile.TileFrameX >= 72) ? 1 : (-1)) : 0;
                                    vector3 = new Vector2(i * 16 + 8 + 10 * num135, j * 16 + 8 + 10 * num136);
                                    float num137 = 3f;
                                    if (num126 == 0)
                                    {
                                        num127 = 98;
                                        damage3 = 20;
                                        num137 = 12f;
                                    }

                                    if (num126 == 1)
                                    {
                                        num127 = 184;
                                        damage3 = 40;
                                        num137 = 12f;
                                    }

                                    if (num126 == 2)
                                    {
                                        num127 = 187;
                                        damage3 = 40;
                                        num137 = 5f;
                                    }

                                    speedX = (float)num135 * num137;
                                    speedY = (float)num136 * num137;
                                }
                                break;
                            case 3:
                                {
                                    if (!CheckMech(i, j, 300))
                                        break;

                                    int num130 = 200;
                                    for (int num131 = 0; num131 < 1000; num131++)
                                    {
                                        if (Main.projectile[num131].active && Main.projectile[num131].type == num127)
                                        {
                                            float num132 = (new Vector2(i * 16 + 8, j * 18 + 8) - Main.projectile[num131].Center).Length();
                                            num130 = ((!(num132 < 50f)) ? ((!(num132 < 100f)) ? ((!(num132 < 200f)) ? ((!(num132 < 300f)) ? ((!(num132 < 400f)) ? ((!(num132 < 500f)) ? ((!(num132 < 700f)) ? ((!(num132 < 900f)) ? ((!(num132 < 1200f)) ? (num130 - 1) : (num130 - 2)) : (num130 - 3)) : (num130 - 4)) : (num130 - 5)) : (num130 - 6)) : (num130 - 8)) : (num130 - 10)) : (num130 - 15)) : (num130 - 50));
                                        }
                                    }

                                    if (num130 > 0)
                                    {
                                        num127 = 185;
                                        damage3 = 40;
                                        int num133 = 0;
                                        int num134 = 0;
                                        switch (tile.TileFrameX / 18)
                                        {
                                            case 0:
                                            case 1:
                                                num133 = 0;
                                                num134 = 1;
                                                break;
                                            case 2:
                                                num133 = 0;
                                                num134 = -1;
                                                break;
                                            case 3:
                                                num133 = -1;
                                                num134 = 0;
                                                break;
                                            case 4:
                                                num133 = 1;
                                                num134 = 0;
                                                break;
                                        }

                                        speedX = (float)(4 * num133) + (float)Main.rand.Next(-20 + ((num133 == 1) ? 20 : 0), 21 - ((num133 == -1) ? 20 : 0)) * 0.05f;
                                        speedY = (float)(4 * num134) + (float)Main.rand.Next(-20 + ((num134 == 1) ? 20 : 0), 21 - ((num134 == -1) ? 20 : 0)) * 0.05f;
                                        vector3 = new Vector2(i * 16 + 8 + 14 * num133, j * 16 + 8 + 14 * num134);
                                    }

                                    break;
                                }
                            case 4:
                                if (CheckMech(i, j, 90))
                                {
                                    int num128 = 0;
                                    int num129 = 0;
                                    switch (tile.TileFrameX / 18)
                                    {
                                        case 0:
                                        case 1:
                                            num128 = 0;
                                            num129 = 1;
                                            break;
                                        case 2:
                                            num128 = 0;
                                            num129 = -1;
                                            break;
                                        case 3:
                                            num128 = -1;
                                            num129 = 0;
                                            break;
                                        case 4:
                                            num128 = 1;
                                            num129 = 0;
                                            break;
                                    }

                                    speedX = 8 * num128;
                                    speedY = 8 * num129;
                                    damage3 = 60;
                                    num127 = 186;
                                    vector3 = new Vector2(i * 16 + 8 + 18 * num128, j * 16 + 8 + 18 * num129);
                                }
                                break;
                        }

                        switch (num126)
                        {
                            case -10:
                                if (CheckMech(i, j, 200))
                                {
                                    int num142 = -1;
                                    if (tile.TileFrameX != 0)
                                        num142 = 1;

                                    speedX = 12 * num142;
                                    damage3 = 20;
                                    num127 = 98;
                                    vector3 = new Vector2(i * 16 + 8, j * 16 + 7);
                                    vector3.X += 10 * num142;
                                    vector3.Y += 2f;
                                }
                                break;
                            case -9:
                                if (CheckMech(i, j, 200))
                                {
                                    int num138 = -1;
                                    if (tile.TileFrameX != 0)
                                        num138 = 1;

                                    speedX = 12 * num138;
                                    damage3 = 40;
                                    num127 = 184;
                                    vector3 = new Vector2(i * 16 + 8, j * 16 + 7);
                                    vector3.X += 10 * num138;
                                    vector3.Y += 2f;
                                }
                                break;
                            case -8:
                                if (CheckMech(i, j, 200))
                                {
                                    int num143 = -1;
                                    if (tile.TileFrameX != 0)
                                        num143 = 1;

                                    speedX = 5 * num143;
                                    damage3 = 40;
                                    num127 = 187;
                                    vector3 = new Vector2(i * 16 + 8, j * 16 + 7);
                                    vector3.X += 10 * num143;
                                    vector3.Y += 2f;
                                }
                                break;
                            case -7:
                                {
                                    if (!CheckMech(i, j, 300))
                                        break;

                                    num127 = 185;
                                    int num139 = 200;
                                    for (int num140 = 0; num140 < 1000; num140++)
                                    {
                                        if (Main.projectile[num140].active && Main.projectile[num140].type == num127)
                                        {
                                            float num141 = (new Vector2(i * 16 + 8, j * 18 + 8) - Main.projectile[num140].Center).Length();
                                            num139 = ((!(num141 < 50f)) ? ((!(num141 < 100f)) ? ((!(num141 < 200f)) ? ((!(num141 < 300f)) ? ((!(num141 < 400f)) ? ((!(num141 < 500f)) ? ((!(num141 < 700f)) ? ((!(num141 < 900f)) ? ((!(num141 < 1200f)) ? (num139 - 1) : (num139 - 2)) : (num139 - 3)) : (num139 - 4)) : (num139 - 5)) : (num139 - 6)) : (num139 - 8)) : (num139 - 10)) : (num139 - 15)) : (num139 - 50));
                                        }
                                    }

                                    if (num139 > 0)
                                    {
                                        speedX = (float)Main.rand.Next(-20, 21) * 0.05f;
                                        speedY = 4f + (float)Main.rand.Next(0, 21) * 0.05f;
                                        damage3 = 40;
                                        vector3 = new Vector2(i * 16 + 8, j * 16 + 16);
                                        vector3.Y += 6f;
                                        Projectile.NewProjectile(GetProjectileSource(i, j), (int)vector3.X, (int)vector3.Y, speedX, speedY, num127, damage3, 2f, Main.myPlayer);
                                    }

                                    break;
                                }
                            case -6:
                                if (CheckMech(i, j, 90))
                                {
                                    speedX = 0f;
                                    speedY = 8f;
                                    damage3 = 60;
                                    num127 = 186;
                                    vector3 = new Vector2(i * 16 + 8, j * 16 + 16);
                                    vector3.Y += 10f;
                                }
                                break;
                        }

                        if (num127 != 0)
                            Projectile.NewProjectile(GetProjectileSource(i, j), (int)vector3.X, (int)vector3.Y, speedX, speedY, num127, damage3, 2f, Main.myPlayer);

                        break;
                    }
                case 443:
                    GeyserTrap(i, j);
                    break;
                case 531:
                    {
                        int num114 = tile.TileFrameX / 36;
                        int num115 = tile.TileFrameY / 54;
                        int num116 = i - (tile.TileFrameX - num114 * 36) / 18;
                        int num117 = j - (tile.TileFrameY - num115 * 54) / 18;
                        if (CheckMech(num116, num117, 900))
                        {
                            Vector2 vector2 = new Vector2(num116 + 1, num117) * 16f;
                            vector2.Y += 28f;
                            int num118 = 99;
                            int damage2 = 70;
                            float knockBack2 = 10f;
                            if (num118 != 0)
                                Projectile.NewProjectile(GetProjectileSource(num116, num117), (int)vector2.X, (int)vector2.Y, 0f, 0f, num118, damage2, knockBack2, Main.myPlayer);
                        }

                        break;
                    }
                case 35:
                case 139:
                    WorldGen.SwitchMB(i, j);
                    break;
                case 207:
                    WorldGen.SwitchFountain(i, j);
                    break;
                case 410:
                case 480:
                case 509:
                    WorldGen.SwitchMonolith(i, j);
                    break;
                case 455:
                    BirthdayParty.ToggleManualParty();
                    break;
                case 141:
                    WorldGen.KillTile(i, j, fail: false, effectOnly: false, noItem: true);
                    NetMessage.SendTileSquare(-1, i, j);
                    Projectile.NewProjectile(GetProjectileSource(i, j), i * 16 + 8, j * 16 + 8, 0f, 0f, 108, 500, 10f, Main.myPlayer);
                    break;
                case 210:
                    WorldGen.ExplodeMine(i, j, fromWiring: true);
                    break;
                case 142:
                case 143:
                    {
                        int num80 = j - tile.TileFrameY / 18;
                        int num81 = tile.TileFrameX / 18;
                        if (num81 > 1)
                            num81 -= 2;

                        num81 = i - num81;
                        SkipWire(num81, num80);
                        SkipWire(num81, num80 + 1);
                        SkipWire(num81 + 1, num80);
                        SkipWire(num81 + 1, num80 + 1);
                        if (type == 142)
                        {
                            for (int num82 = 0; num82 < 4; num82++)
                            {
                                if (_numInPump >= 19)
                                    break;

                                int num83;
                                int num84;
                                switch (num82)
                                {
                                    case 0:
                                        num83 = num81;
                                        num84 = num80 + 1;
                                        break;
                                    case 1:
                                        num83 = num81 + 1;
                                        num84 = num80 + 1;
                                        break;
                                    case 2:
                                        num83 = num81;
                                        num84 = num80;
                                        break;
                                    default:
                                        num83 = num81 + 1;
                                        num84 = num80;
                                        break;
                                }

                                _inPumpX[_numInPump] = num83;
                                _inPumpY[_numInPump] = num84;
                                _numInPump++;
                            }

                            break;
                        }

                        for (int num85 = 0; num85 < 4; num85++)
                        {
                            if (_numOutPump >= 19)
                                break;

                            int num83;
                            int num84;
                            switch (num85)
                            {
                                case 0:
                                    num83 = num81;
                                    num84 = num80 + 1;
                                    break;
                                case 1:
                                    num83 = num81 + 1;
                                    num84 = num80 + 1;
                                    break;
                                case 2:
                                    num83 = num81;
                                    num84 = num80;
                                    break;
                                default:
                                    num83 = num81 + 1;
                                    num84 = num80;
                                    break;
                            }

                            _outPumpX[_numOutPump] = num83;
                            _outPumpY[_numOutPump] = num84;
                            _numOutPump++;
                        }

                        break;
                    }
                case 105:
                    {
                        int num96 = j - tile.TileFrameY / 18;
                        int num97 = tile.TileFrameX / 18;
                        int num98 = 0;
                        while (num97 >= 2)
                        {
                            num97 -= 2;
                            num98++;
                        }

                        num97 = i - num97;
                        num97 = i - tile.TileFrameX % 36 / 18;
                        num96 = j - tile.TileFrameY % 54 / 18;
                        int num99 = tile.TileFrameY / 54;
                        num99 %= 3;
                        num98 = tile.TileFrameX / 36 + num99 * 55;
                        SkipWire(num97, num96);
                        SkipWire(num97, num96 + 1);
                        SkipWire(num97, num96 + 2);
                        SkipWire(num97 + 1, num96);
                        SkipWire(num97 + 1, num96 + 1);
                        SkipWire(num97 + 1, num96 + 2);
                        int num100 = num97 * 16 + 16;
                        int num101 = (num96 + 3) * 16;
                        int num102 = -1;
                        int num103 = -1;
                        bool flag11 = true;
                        bool flag12 = false;
                        switch (num98)
                        {
                            case 5:
                                num103 = 73;
                                break;
                            case 13:
                                num103 = 24;
                                break;
                            case 30:
                                num103 = 6;
                                break;
                            case 35:
                                num103 = 2;
                                break;
                            case 51:
                                num103 = Utils.SelectRandom(Main.rand, new short[2] {
                                    299,
                                    538
                                });
                                break;
                            case 52:
                                num103 = 356;
                                break;
                            case 53:
                                num103 = 357;
                                break;
                            case 54:
                                num103 = Utils.SelectRandom(Main.rand, new short[2] {
                                    355,
                                    358
                                });
                                break;
                            case 55:
                                num103 = Utils.SelectRandom(Main.rand, new short[2] {
                                    367,
                                    366
                                });
                                break;
                            case 56:
                                num103 = Utils.SelectRandom(Main.rand, new short[5] {
                                    359,
                                    359,
                                    359,
                                    359,
                                    360
                                });
                                break;
                            case 57:
                                num103 = 377;
                                break;
                            case 58:
                                num103 = 300;
                                break;
                            case 59:
                                num103 = Utils.SelectRandom(Main.rand, new short[2] {
                                    364,
                                    362
                                });
                                break;
                            case 60:
                                num103 = 148;
                                break;
                            case 61:
                                num103 = 361;
                                break;
                            case 62:
                                num103 = Utils.SelectRandom(Main.rand, new short[3] {
                                    487,
                                    486,
                                    485
                                });
                                break;
                            case 63:
                                num103 = 164;
                                flag11 &= NPC.MechSpawn(num100, num101, 165);
                                break;
                            case 64:
                                num103 = 86;
                                flag12 = true;
                                break;
                            case 65:
                                num103 = 490;
                                break;
                            case 66:
                                num103 = 82;
                                break;
                            case 67:
                                num103 = 449;
                                break;
                            case 68:
                                num103 = 167;
                                break;
                            case 69:
                                num103 = 480;
                                break;
                            case 70:
                                num103 = 48;
                                break;
                            case 71:
                                num103 = Utils.SelectRandom(Main.rand, new short[3] {
                                    170,
                                    180,
                                    171
                                });
                                flag12 = true;
                                break;
                            case 72:
                                num103 = 481;
                                break;
                            case 73:
                                num103 = 482;
                                break;
                            case 74:
                                num103 = 430;
                                break;
                            case 75:
                                num103 = 489;
                                break;
                            case 76:
                                num103 = 611;
                                break;
                            case 77:
                                num103 = 602;
                                break;
                            case 78:
                                num103 = Utils.SelectRandom(Main.rand, new short[6] {
                                    595,
                                    596,
                                    599,
                                    597,
                                    600,
                                    598
                                });
                                break;
                            case 79:
                                num103 = Utils.SelectRandom(Main.rand, new short[2] {
                                    616,
                                    617
                                });
                                break;
                        }

                        if (num103 != -1 && CheckMech(num97, num96, 30) && NPC.MechSpawn(num100, num101, num103) && flag11)
                        {
                            if (!flag12 || !Collision.SolidTiles(num97 - 2, num97 + 3, num96, num96 + 2))
                            {
                                num102 = NPC.NewNPC(GetNPCSource(num97, num96), num100, num101, num103);
                            }
                            else
                            {
                                Vector2 position = new Vector2(num100 - 4, num101 - 22) - new Vector2(10f);
                                Utils.PoofOfSmoke(position);
                                NetMessage.SendData(106, -1, -1, null, (int)position.X, position.Y);
                            }
                        }

                        if (num102 <= -1)
                        {
                            switch (num98)
                            {
                                case 4:
                                    if (CheckMech(num97, num96, 30) && NPC.MechSpawn(num100, num101, 1))
                                        num102 = NPC.NewNPC(GetNPCSource(num97, num96), num100, num101 - 12, 1);
                                    break;
                                case 7:
                                    if (CheckMech(num97, num96, 30) && NPC.MechSpawn(num100, num101, 49))
                                        num102 = NPC.NewNPC(GetNPCSource(num97, num96), num100 - 4, num101 - 6, 49);
                                    break;
                                case 8:
                                    if (CheckMech(num97, num96, 30) && NPC.MechSpawn(num100, num101, 55))
                                        num102 = NPC.NewNPC(GetNPCSource(num97, num96), num100, num101 - 12, 55);
                                    break;
                                case 9:
                                    {
                                        int type4 = 46;
                                        if (BirthdayParty.PartyIsUp)
                                            type4 = 540;

                                        if (CheckMech(num97, num96, 30) && NPC.MechSpawn(num100, num101, type4))
                                            num102 = NPC.NewNPC(GetNPCSource(num97, num96), num100, num101 - 12, type4);

                                        break;
                                    }
                                case 10:
                                    if (CheckMech(num97, num96, 30) && NPC.MechSpawn(num100, num101, 21))
                                        num102 = NPC.NewNPC(GetNPCSource(num97, num96), num100, num101, 21);
                                    break;
                                case 16:
                                    if (CheckMech(num97, num96, 30) && NPC.MechSpawn(num100, num101, 42))
                                    {
                                        if (!Collision.SolidTiles(num97 - 1, num97 + 1, num96, num96 + 1))
                                        {
                                            num102 = NPC.NewNPC(GetNPCSource(num97, num96), num100, num101 - 12, 42);
                                            break;
                                        }

                                        Vector2 position3 = new Vector2(num100 - 4, num101 - 22) - new Vector2(10f);
                                        Utils.PoofOfSmoke(position3);
                                        NetMessage.SendData(106, -1, -1, null, (int)position3.X, position3.Y);
                                    }
                                    break;
                                case 18:
                                    if (CheckMech(num97, num96, 30) && NPC.MechSpawn(num100, num101, 67))
                                        num102 = NPC.NewNPC(GetNPCSource(num97, num96), num100, num101 - 12, 67);
                                    break;
                                case 23:
                                    if (CheckMech(num97, num96, 30) && NPC.MechSpawn(num100, num101, 63))
                                        num102 = NPC.NewNPC(GetNPCSource(num97, num96), num100, num101 - 12, 63);
                                    break;
                                case 27:
                                    if (CheckMech(num97, num96, 30) && NPC.MechSpawn(num100, num101, 85))
                                        num102 = NPC.NewNPC(GetNPCSource(num97, num96), num100 - 9, num101, 85);
                                    break;
                                case 28:
                                    if (CheckMech(num97, num96, 30) && NPC.MechSpawn(num100, num101, 74))
                                    {
                                        num102 = NPC.NewNPC(GetNPCSource(num97, num96), num100, num101 - 12, Utils.SelectRandom(Main.rand, new short[3] {
                                            74,
                                            297,
                                            298
                                        }));
                                    }
                                    break;
                                case 34:
                                    {
                                        for (int num112 = 0; num112 < 2; num112++)
                                        {
                                            for (int num113 = 0; num113 < 3; num113++)
                                            {
                                                Tile tile2 = Main.tile[num97 + num112, num96 + num113];
                                                tile2.TileType = 349;
                                                tile2.TileFrameX = (short)(num112 * 18 + 216);
                                                tile2.TileFrameY = (short)(num113 * 18);
                                            }
                                        }

                                        Animation.NewTemporaryAnimation(0, 349, num97, num96);
                                        if (Main.netMode == 2)
                                            NetMessage.SendTileSquare(-1, num97, num96, 2, 3);

                                        break;
                                    }
                                case 42:
                                    if (CheckMech(num97, num96, 30) && NPC.MechSpawn(num100, num101, 58))
                                        num102 = NPC.NewNPC(GetNPCSource(num97, num96), num100, num101 - 12, 58);
                                    break;
                                case 37:
                                    if (CheckMech(num97, num96, 600) && Item.MechSpawn(num100, num101, 58) && Item.MechSpawn(num100, num101, 1734) && Item.MechSpawn(num100, num101, 1867))
                                        Item.NewItem(GetItemSource(num100, num101), num100, num101 - 16, 0, 0, 58);
                                    break;
                                case 50:
                                    if (CheckMech(num97, num96, 30) && NPC.MechSpawn(num100, num101, 65))
                                    {
                                        if (!Collision.SolidTiles(num97 - 2, num97 + 3, num96, num96 + 2))
                                        {
                                            num102 = NPC.NewNPC(GetNPCSource(num97, num96), num100, num101 - 12, 65);
                                            break;
                                        }

                                        Vector2 position2 = new Vector2(num100 - 4, num101 - 22) - new Vector2(10f);
                                        Utils.PoofOfSmoke(position2);
                                        NetMessage.SendData(106, -1, -1, null, (int)position2.X, position2.Y);
                                    }
                                    break;
                                case 2:
                                    if (CheckMech(num97, num96, 600) && Item.MechSpawn(num100, num101, 184) && Item.MechSpawn(num100, num101, 1735) && Item.MechSpawn(num100, num101, 1868))
                                        Item.NewItem(GetItemSource(num100, num101), num100, num101 - 16, 0, 0, 184);
                                    break;
                                case 17:
                                    if (CheckMech(num97, num96, 600) && Item.MechSpawn(num100, num101, 166))
                                        Item.NewItem(GetItemSource(num100, num101), num100, num101 - 20, 0, 0, 166);
                                    break;
                                case 40:
                                    {
                                        if (!CheckMech(num97, num96, 300))
                                            break;

                                        int num108 = 50;
                                        int[] array2 = new int[num108];
                                        int num109 = 0;
                                        for (int num110 = 0; num110 < 200; num110++)
                                        {
                                            if (Main.npc[num110].active && (Main.npc[num110].type == 17 || Main.npc[num110].type == 19 || Main.npc[num110].type == 22 || Main.npc[num110].type == 38 || Main.npc[num110].type == 54 || Main.npc[num110].type == 107 || Main.npc[num110].type == 108 || Main.npc[num110].type == 142 || Main.npc[num110].type == 160 || Main.npc[num110].type == 207 || Main.npc[num110].type == 209 || Main.npc[num110].type == 227 || Main.npc[num110].type == 228 || Main.npc[num110].type == 229 || Main.npc[num110].type == 368 || Main.npc[num110].type == 369 || Main.npc[num110].type == 550 || Main.npc[num110].type == 441 || Main.npc[num110].type == 588))
                                            {
                                                array2[num109] = num110;
                                                num109++;
                                                if (num109 >= num108)
                                                    break;
                                            }
                                        }

                                        if (num109 > 0)
                                        {
                                            int num111 = array2[Main.rand.Next(num109)];
                                            Main.npc[num111].position.X = num100 - Main.npc[num111].width / 2;
                                            Main.npc[num111].position.Y = num101 - Main.npc[num111].height - 1;
                                            NetMessage.SendData(23, -1, -1, null, num111);
                                        }

                                        break;
                                    }
                                case 41:
                                    {
                                        if (!CheckMech(num97, num96, 300))
                                            break;

                                        int num104 = 50;
                                        int[] array = new int[num104];
                                        int num105 = 0;
                                        for (int num106 = 0; num106 < 200; num106++)
                                        {
                                            if (Main.npc[num106].active && (Main.npc[num106].type == 18 || Main.npc[num106].type == 20 || Main.npc[num106].type == 124 || Main.npc[num106].type == 178 || Main.npc[num106].type == 208 || Main.npc[num106].type == 353 || Main.npc[num106].type == 633 || Main.npc[num106].type == 663))
                                            {
                                                array[num105] = num106;
                                                num105++;
                                                if (num105 >= num104)
                                                    break;
                                            }
                                        }

                                        if (num105 > 0)
                                        {
                                            int num107 = array[Main.rand.Next(num105)];
                                            Main.npc[num107].position.X = num100 - Main.npc[num107].width / 2;
                                            Main.npc[num107].position.Y = num101 - Main.npc[num107].height - 1;
                                            NetMessage.SendData(23, -1, -1, null, num107);
                                        }

                                        break;
                                    }
                            }
                        }

                        if (num102 >= 0)
                        {
                            Main.npc[num102].value = 0f;
                            Main.npc[num102].npcSlots = 0f;
                            Main.npc[num102].SpawnedFromStatue = true;
                        }

                        break;
                    }
                case 349:
                    {
                        int num74 = tile.TileFrameY / 18;
                        num74 %= 3;
                        int num75 = j - num74;
                        int num76;
                        for (num76 = tile.TileFrameX / 18; num76 >= 2; num76 -= 2)
                        {
                        }

                        num76 = i - num76;
                        SkipWire(num76, num75);
                        SkipWire(num76, num75 + 1);
                        SkipWire(num76, num75 + 2);
                        SkipWire(num76 + 1, num75);
                        SkipWire(num76 + 1, num75 + 1);
                        SkipWire(num76 + 1, num75 + 2);
                        short num77 = (short)((Main.tile[num76, num75].TileFrameX != 0) ? (-216) : 216);
                        for (int num78 = 0; num78 < 2; num78++)
                        {
                            for (int num79 = 0; num79 < 3; num79++)
                            {
                                Main.tile[num76 + num78, num75 + num79].TileFrameX += num77;
                            }
                        }

                        if (Main.netMode == 2)
                            NetMessage.SendTileSquare(-1, num76, num75, 2, 3);

                        Animation.NewTemporaryAnimation((num77 <= 0) ? 1 : 0, 349, num76, num75);
                        break;
                    }
                case 506:
                    {
                        int num57 = tile.TileFrameY / 18;
                        num57 %= 3;
                        int num58 = j - num57;
                        int num59;
                        for (num59 = tile.TileFrameX / 18; num59 >= 2; num59 -= 2)
                        {
                        }

                        num59 = i - num59;
                        SkipWire(num59, num58);
                        SkipWire(num59, num58 + 1);
                        SkipWire(num59, num58 + 2);
                        SkipWire(num59 + 1, num58);
                        SkipWire(num59 + 1, num58 + 1);
                        SkipWire(num59 + 1, num58 + 2);
                        short num60 = (short)((Main.tile[num59, num58].TileFrameX >= 72) ? (-72) : 72);
                        for (int num61 = 0; num61 < 2; num61++)
                        {
                            for (int num62 = 0; num62 < 3; num62++)
                            {
                                Main.tile[num59 + num61, num58 + num62].TileFrameX += num60;
                            }
                        }

                        if (Main.netMode == 2)
                            NetMessage.SendTileSquare(-1, num59, num58, 2, 3);

                        break;
                    }
                case 546:
                    tile.TileType = 557;
                    WorldGen.SquareTileFrame(i, j);
                    NetMessage.SendTileSquare(-1, i, j);
                    break;
                case 557:
                    tile.TileType = 546;
                    WorldGen.SquareTileFrame(i, j);
                    NetMessage.SendTileSquare(-1, i, j);
                    break;
            }
        }

        public static void ToggleHolidayLight(int i, int j, Tile tileCache, bool? forcedStateWhereTrueIsOn)
        {
            bool flag = tileCache.TileFrameX >= 54;
            if (!forcedStateWhereTrueIsOn.HasValue || !forcedStateWhereTrueIsOn.Value != flag)
            {
                if (tileCache.TileFrameX < 54)
                    tileCache.TileFrameX += 54;
                else
                    tileCache.TileFrameX -= 54;

                NetMessage.SendTileSquare(-1, i, j);
            }
        }

        public static void ToggleHangingLantern(int i, int j, Tile tileCache, bool? forcedStateWhereTrueIsOn, bool doSkipWires)
        {
            int num;
            for (num = tileCache.TileFrameY / 18; num >= 2; num -= 2)
            {
            }

            int num2 = j - num;
            short num3 = 18;
            if (tileCache.TileFrameX > 0)
                num3 = -18;

            bool flag = tileCache.TileFrameX > 0;
            if (!forcedStateWhereTrueIsOn.HasValue || !forcedStateWhereTrueIsOn.Value != flag)
            {
                Main.tile[i, num2].TileFrameX += num3;
                Main.tile[i, num2 + 1].TileFrameX += num3;
                if (doSkipWires)
                {
                    SkipWire(i, num2);
                    SkipWire(i, num2 + 1);
                }

                NetMessage.SendTileSquare(-1, i, j, 1, 2);
            }
        }

        public static void Toggle2x2Light(int i, int j, Tile tileCache, bool? forcedStateWhereTrueIsOn, bool doSkipWires)
        {
            int num;
            for (num = tileCache.TileFrameY / 18; num >= 2; num -= 2)
            {
            }

            num = j - num;
            int num2 = tileCache.TileFrameX / 18;
            if (num2 > 1)
                num2 -= 2;

            num2 = i - num2;
            short num3 = 36;
            if (Main.tile[num2, num].TileFrameX > 0)
                num3 = -36;

            bool flag = Main.tile[num2, num].TileFrameX > 0;
            if (!forcedStateWhereTrueIsOn.HasValue || !forcedStateWhereTrueIsOn.Value != flag)
            {
                Main.tile[num2, num].TileFrameX += num3;
                Main.tile[num2, num + 1].TileFrameX += num3;
                Main.tile[num2 + 1, num].TileFrameX += num3;
                Main.tile[num2 + 1, num + 1].TileFrameX += num3;
                if (doSkipWires)
                {
                    SkipWire(num2, num);
                    SkipWire(num2 + 1, num);
                    SkipWire(num2, num + 1);
                    SkipWire(num2 + 1, num + 1);
                }

                NetMessage.SendTileSquare(-1, num2, num, 2, 2);
            }
        }

        public static void ToggleLampPost(int i, int j, Tile tileCache, bool? forcedStateWhereTrueIsOn, bool doSkipWires)
        {
            int num = j - tileCache.TileFrameY / 18;
            short num2 = 18;
            if (tileCache.TileFrameX > 0)
                num2 = -18;

            bool flag = tileCache.TileFrameX > 0;
            if (forcedStateWhereTrueIsOn.HasValue && !forcedStateWhereTrueIsOn.Value == flag)
                return;

            for (int k = num; k < num + 6; k++)
            {
                Main.tile[i, k].TileFrameX += num2;
                if (doSkipWires)
                    SkipWire(i, k);
            }

            NetMessage.SendTileSquare(-1, i, num, 1, 6);
        }

        public static void ToggleTorch(int i, int j, Tile tileCache, bool? forcedStateWhereTrueIsOn)
        {
            bool flag = tileCache.TileFrameX >= 66;
            if (!forcedStateWhereTrueIsOn.HasValue || !forcedStateWhereTrueIsOn.Value != flag)
            {
                if (tileCache.TileFrameX < 66)
                    tileCache.TileFrameX += 66;
                else
                    tileCache.TileFrameX -= 66;

                NetMessage.SendTileSquare(-1, i, j);
            }
        }

        public static void ToggleCandle(int i, int j, Tile tileCache, bool? forcedStateWhereTrueIsOn)
        {
            short num = 18;
            if (tileCache.TileFrameX > 0)
                num = -18;

            bool flag = tileCache.TileFrameX > 0;
            if (!forcedStateWhereTrueIsOn.HasValue || !forcedStateWhereTrueIsOn.Value != flag)
            {
                tileCache.TileFrameX += num;
                NetMessage.SendTileSquare(-1, i, j, 3);
            }
        }

        public static void ToggleLamp(int i, int j, Tile tileCache, bool? forcedStateWhereTrueIsOn, bool doSkipWires)
        {
            int num;
            for (num = tileCache.TileFrameY / 18; num >= 3; num -= 3)
            {
            }

            num = j - num;
            short num2 = 18;
            if (tileCache.TileFrameX > 0)
                num2 = -18;

            bool flag = tileCache.TileFrameX > 0;
            if (!forcedStateWhereTrueIsOn.HasValue || !forcedStateWhereTrueIsOn.Value != flag)
            {
                Main.tile[i, num].TileFrameX += num2;
                Main.tile[i, num + 1].TileFrameX += num2;
                Main.tile[i, num + 2].TileFrameX += num2;
                if (doSkipWires)
                {
                    SkipWire(i, num);
                    SkipWire(i, num + 1);
                    SkipWire(i, num + 2);
                }

                NetMessage.SendTileSquare(-1, i, num, 1, 3);
            }
        }

        public static void ToggleChandelier(int i, int j, Tile tileCache, bool? forcedStateWhereTrueIsOn, bool doSkipWires)
        {
            int num;
            for (num = tileCache.TileFrameY / 18; num >= 3; num -= 3)
            {
            }

            int num2 = j - num;
            int num3 = tileCache.TileFrameX % 108 / 18;
            if (num3 > 2)
                num3 -= 3;

            num3 = i - num3;
            short num4 = 54;
            if (Main.tile[num3, num2].TileFrameX % 108 > 0)
                num4 = -54;

            bool flag = Main.tile[num3, num2].TileFrameX % 108 > 0;
            if (forcedStateWhereTrueIsOn.HasValue && !forcedStateWhereTrueIsOn.Value == flag)
                return;

            for (int k = num3; k < num3 + 3; k++)
            {
                for (int l = num2; l < num2 + 3; l++)
                {
                    Main.tile[k, l].TileFrameX += num4;
                    if (doSkipWires)
                        SkipWire(k, l);
                }
            }

            NetMessage.SendTileSquare(-1, num3 + 1, num2 + 1, 3);
        }

        public static void ToggleCampFire(int i, int j, Tile tileCache, bool? forcedStateWhereTrueIsOn, bool doSkipWires)
        {
            int num = tileCache.TileFrameX % 54 / 18;
            int num2 = tileCache.TileFrameY % 36 / 18;
            int num3 = i - num;
            int num4 = j - num2;
            bool flag = Main.tile[num3, num4].TileFrameY >= 36;
            if (forcedStateWhereTrueIsOn.HasValue && !forcedStateWhereTrueIsOn.Value == flag)
                return;

            int num5 = 36;
            if (Main.tile[num3, num4].TileFrameY >= 36)
                num5 = -36;

            for (int k = num3; k < num3 + 3; k++)
            {
                for (int l = num4; l < num4 + 2; l++)
                {
                    if (doSkipWires)
                        SkipWire(k, l);

                    Main.tile[k, l].TileFrameY = (short)(Main.tile[k, l].TileFrameY + num5);
                }
            }

            NetMessage.SendTileSquare(-1, num3, num4, 3, 2);
        }

        public static void ToggleFirePlace(int i, int j, Tile theBlock, bool? forcedStateWhereTrueIsOn, bool doSkipWires)
        {
            int num = theBlock.TileFrameX % 54 / 18;
            int num2 = theBlock.TileFrameY % 36 / 18;
            int num3 = i - num;
            int num4 = j - num2;
            bool flag = Main.tile[num3, num4].TileFrameX >= 54;
            if (forcedStateWhereTrueIsOn.HasValue && !forcedStateWhereTrueIsOn.Value == flag)
                return;

            int num5 = 54;
            if (Main.tile[num3, num4].TileFrameX >= 54)
                num5 = -54;

            for (int k = num3; k < num3 + 3; k++)
            {
                for (int l = num4; l < num4 + 2; l++)
                {
                    if (doSkipWires)
                        SkipWire(k, l);

                    Main.tile[k, l].TileFrameX = (short)(Main.tile[k, l].TileFrameX + num5);
                }
            }

            NetMessage.SendTileSquare(-1, num3, num4, 3, 2);
        }

        private static void GeyserTrap(int i, int j)
        {
            Tile tile = Main.tile[i, j];
            if (tile.TileType != 443)
                return;

            int num = tile.TileFrameX / 36;
            int num2 = i - (tile.TileFrameX - num * 36) / 18;
            if (CheckMech(num2, j, 200))
            {
                Vector2 zero = Vector2.Zero;
                Vector2 zero2 = Vector2.Zero;
                int num3 = 654;
                int damage = 20;
                if (num < 2)
                {
                    zero = new Vector2(num2 + 1, j) * 16f;
                    zero2 = new Vector2(0f, -8f);
                }
                else
                {
                    zero = new Vector2(num2 + 1, j + 1) * 16f;
                    zero2 = new Vector2(0f, 8f);
                }

                if (num3 != 0)
                    Projectile.NewProjectile(GetProjectileSource(num2, j), (int)zero.X, (int)zero.Y, zero2.X, zero2.Y, num3, damage, 2f, Main.myPlayer);
            }
        }

        private static void Teleport()
        {
            if (_teleport[0].X < _teleport[1].X + 3f && _teleport[0].X > _teleport[1].X - 3f && _teleport[0].Y > _teleport[1].Y - 3f && _teleport[0].Y < _teleport[1].Y)
                return;

            Rectangle[] array = new Rectangle[2];
            array[0].X = (int)(_teleport[0].X * 16f);
            array[0].Width = 48;
            array[0].Height = 48;
            array[0].Y = (int)(_teleport[0].Y * 16f - (float)array[0].Height);
            array[1].X = (int)(_teleport[1].X * 16f);
            array[1].Width = 48;
            array[1].Height = 48;
            array[1].Y = (int)(_teleport[1].Y * 16f - (float)array[1].Height);
            for (int i = 0; i < 2; i++)
            {
                Vector2 value = new Vector2(array[1].X - array[0].X, array[1].Y - array[0].Y);
                if (i == 1)
                    value = new Vector2(array[0].X - array[1].X, array[0].Y - array[1].Y);

                if (!blockPlayerTeleportationForOneIteration)
                {
                    for (int j = 0; j < 255; j++)
                    {
                        if (Main.player[j].active && !Main.player[j].dead && !Main.player[j].teleporting && TeleporterHitboxIntersects(array[i], Main.player[j].Hitbox))
                        {
                            Vector2 vector = Main.player[j].position + value;
                            Main.player[j].teleporting = true;
                            if (Main.netMode == 2)
                                RemoteClient.CheckSection(j, vector);

                            Main.player[j].Teleport(vector);
                            if (Main.netMode == 2)
                                NetMessage.SendData(65, -1, -1, null, 0, j, vector.X, vector.Y);
                        }
                    }
                }

                for (int k = 0; k < 200; k++)
                {
                    if (Main.npc[k].active && !Main.npc[k].teleporting && Main.npc[k].lifeMax > 5 && !Main.npc[k].boss && !Main.npc[k].noTileCollide)
                    {
                        int type = Main.npc[k].type;
                        if (!NPCID.Sets.TeleportationImmune[type] && TeleporterHitboxIntersects(array[i], Main.npc[k].Hitbox))
                        {
                            Main.npc[k].teleporting = true;
                            Main.npc[k].Teleport(Main.npc[k].position + value);
                        }
                    }
                }
            }

            for (int l = 0; l < 255; l++)
            {
                Main.player[l].teleporting = false;
            }

            for (int m = 0; m < 200; m++)
            {
                Main.npc[m].teleporting = false;
            }
        }

        private static bool TeleporterHitboxIntersects(Rectangle teleporter, Rectangle entity)
        {
            Rectangle rectangle = Rectangle.Union(teleporter, entity);
            if (rectangle.Width <= teleporter.Width + entity.Width)
                return rectangle.Height <= teleporter.Height + entity.Height;

            return false;
        }

        private static void DeActive(int i, int j)
        {
            if (Main.tile[i, j].IsActuated || (Main.tile[i, j].TileType == 226 && (double)j > Main.worldSurface && !NPC.downedPlantBoss))
                return;

            bool flag = Main.tileSolid[Main.tile[i, j].TileType] && !TileID.Sets.NotReallySolid[Main.tile[i, j].TileType];
            ushort type = Main.tile[i, j].TileType;
            if (type == 314 || (uint)(type - 386) <= 3u || type == 476)
                flag = false;

            if (flag && (Main.tile[i, j - 1].IsActuated || (!TileID.Sets.BasicChest[Main.tile[i, j - 1].TileType] && Main.tile[i, j - 1].TileType != 26 && Main.tile[i, j - 1].TileType != 77 && Main.tile[i, j - 1].TileType != 88 && Main.tile[i, j - 1].TileType != 470 && Main.tile[i, j - 1].TileType != 475 && Main.tile[i, j - 1].TileType != 237 && Main.tile[i, j - 1].TileType != 597 && WorldGen.CanKillTile(i, j - 1))))
            {
                Tile tile = Main.tile[i, j];
                tile.IsActuated = true;
                WorldGen.SquareTileFrame(i, j, resetFrame: false);
                if (Main.netMode != 1)
                    NetMessage.SendTileSquare(-1, i, j);
            }
        }

        private static void ReActive(int i, int j)
        {
            Tile tile = Main.tile[i, j];
            tile.IsActuated = false;
            
            WorldGen.SquareTileFrame(i, j, resetFrame: false);
            if (Main.netMode != 1)
                NetMessage.SendTileSquare(-1, i, j);
        }

        // Unused
        private static void MassWireOperationInner(Player user, Point ps, Point pe, Vector2 dropPoint, bool dir, ref int wireCount, ref int actuatorCount)
        {
            Math.Abs(ps.X - pe.X);
            Math.Abs(ps.Y - pe.Y);
            int num = Math.Sign(pe.X - ps.X);
            int num2 = Math.Sign(pe.Y - ps.Y);
            WiresUI.Settings.MultiToolMode toolMode = WiresUI.Settings.ToolMode;
            Point pt = default(Point);
            bool flag = false;
            Item.StartCachingType(530);
            Item.StartCachingType(849);
            bool flag2 = dir;
            int num3;
            int num4;
            int num5;
            if (flag2)
            {
                pt.X = ps.X;
                num3 = ps.Y;
                num4 = pe.Y;
                num5 = num2;
            }
            else
            {
                pt.Y = ps.Y;
                num3 = ps.X;
                num4 = pe.X;
                num5 = num;
            }

            for (int i = num3; i != num4; i += num5)
            {
                if (flag)
                    break;

                if (flag2)
                    pt.Y = i;
                else
                    pt.X = i;

                bool? flag3 = MassWireOperationStep(pt, toolMode, ref wireCount, ref actuatorCount);
                if (flag3.HasValue && !flag3.Value)
                {
                    flag = true;
                    break;
                }
            }

            if (flag2)
            {
                pt.Y = pe.Y;
                num3 = ps.X;
                num4 = pe.X;
                num5 = num;
            }
            else
            {
                pt.X = pe.X;
                num3 = ps.Y;
                num4 = pe.Y;
                num5 = num2;
            }

            for (int j = num3; j != num4; j += num5)
            {
                if (flag)
                    break;

                if (!flag2)
                    pt.Y = j;
                else
                    pt.X = j;

                bool? flag4 = MassWireOperationStep(pt, toolMode, ref wireCount, ref actuatorCount);
                if (flag4.HasValue && !flag4.Value)
                {
                    flag = true;
                    break;
                }
            }

            if (!flag)
                MassWireOperationStep(pe, toolMode, ref wireCount, ref actuatorCount);

            //IEntitySource reason = ;
            //Item.DropCache(reason, dropPoint, Vector2.Zero, 530);
            //Item.DropCache(reason, dropPoint, Vector2.Zero, 849);
        }

        private static bool? MassWireOperationStep(Point pt, WiresUI.Settings.MultiToolMode mode, ref int wiresLeftToConsume, ref int actuatorsLeftToConstume)
        {
            if (!WorldGen.InWorld(pt.X, pt.Y, 1))
                return null;

            Tile tile = Main.tile[pt.X, pt.Y];
            if (tile == null)
                return null;

            if (!mode.HasFlag(WiresUI.Settings.MultiToolMode.Cutter))
            {
                if (mode.HasFlag(WiresUI.Settings.MultiToolMode.Red) && !tile.RedWire)
                {
                    if (wiresLeftToConsume <= 0)
                        return false;

                    wiresLeftToConsume--;
                    WorldGen.PlaceWire(pt.X, pt.Y);
                    NetMessage.SendData(17, -1, -1, null, 5, pt.X, pt.Y);
                }

                if (mode.HasFlag(WiresUI.Settings.MultiToolMode.Green) && !tile.GreenWire)
                {
                    if (wiresLeftToConsume <= 0)
                        return false;

                    wiresLeftToConsume--;
                    WorldGen.PlaceWire3(pt.X, pt.Y);
                    NetMessage.SendData(17, -1, -1, null, 12, pt.X, pt.Y);
                }

                if (mode.HasFlag(WiresUI.Settings.MultiToolMode.Blue) && !tile.BlueWire)
                {
                    if (wiresLeftToConsume <= 0)
                        return false;

                    wiresLeftToConsume--;
                    WorldGen.PlaceWire2(pt.X, pt.Y);
                    NetMessage.SendData(17, -1, -1, null, 10, pt.X, pt.Y);
                }

                if (mode.HasFlag(WiresUI.Settings.MultiToolMode.Yellow) && !tile.YellowWire)
                {
                    if (wiresLeftToConsume <= 0)
                        return false;

                    wiresLeftToConsume--;
                    WorldGen.PlaceWire4(pt.X, pt.Y);
                    NetMessage.SendData(17, -1, -1, null, 16, pt.X, pt.Y);
                }

                if (mode.HasFlag(WiresUI.Settings.MultiToolMode.Actuator) && !tile.HasActuator)
                {
                    if (actuatorsLeftToConstume <= 0)
                        return false;

                    actuatorsLeftToConstume--;
                    WorldGen.PlaceActuator(pt.X, pt.Y);
                    NetMessage.SendData(17, -1, -1, null, 8, pt.X, pt.Y);
                }
            }

            if (mode.HasFlag(WiresUI.Settings.MultiToolMode.Cutter))
            {
                if (mode.HasFlag(WiresUI.Settings.MultiToolMode.Red) && tile.RedWire && WorldGen.KillWire(pt.X, pt.Y))
                    NetMessage.SendData(17, -1, -1, null, 6, pt.X, pt.Y);

                if (mode.HasFlag(WiresUI.Settings.MultiToolMode.Green) && tile.GreenWire && WorldGen.KillWire3(pt.X, pt.Y))
                    NetMessage.SendData(17, -1, -1, null, 13, pt.X, pt.Y);

                if (mode.HasFlag(WiresUI.Settings.MultiToolMode.Blue) && tile.BlueWire && WorldGen.KillWire2(pt.X, pt.Y))
                    NetMessage.SendData(17, -1, -1, null, 11, pt.X, pt.Y);

                if (mode.HasFlag(WiresUI.Settings.MultiToolMode.Yellow) && tile.YellowWire && WorldGen.KillWire4(pt.X, pt.Y))
                    NetMessage.SendData(17, -1, -1, null, 17, pt.X, pt.Y);

                if (mode.HasFlag(WiresUI.Settings.MultiToolMode.Actuator) && tile.HasActuator && WorldGen.KillActuator(pt.X, pt.Y))
                    NetMessage.SendData(17, -1, -1, null, 9, pt.X, pt.Y);
            }

            return true;
        }
    }
}
