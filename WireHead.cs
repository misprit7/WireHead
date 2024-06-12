using Terraria.ModLoader;
using Terraria.IO;
using System;
using System.Collections.Concurrent;
using Terraria;
using Microsoft.Xna.Framework;
using Terraria.DataStructures;
using Terraria.ID;

namespace WireHead
{
	public class WireHead : Mod
    {

        public static bool vanillaWiring = false;
        public static bool useTerracc = false;
        public static ConcurrentQueue<Action> toExec = new ConcurrentQueue<Action>();

        private static void UpdateConnectedClients(Terraria.On_Netplay.orig_UpdateConnectedClients orig)
        {
            // Forces update even if no clients connected by making it think there are always clients
            orig();
            Netplay.HasClients = true;
        }

        /*
         * The On.Terraria api comes from monomod
         * https://github.com/MonoMod/MonoMod
         * From my understanding it works at compile time so doesn't suffer the performance penalty of reflection
         */

        /*
         * Override vanilla wiring implementation
         */
        public static void AddEvents()
        {
            vanillaWiring = false;
            WorldFile.OnWorldLoad += Accelerator.Preprocess;

            Terraria.On_Wiring.SetCurrentUser += Events.SetCurrentUser;
            //On.Terraria.Wiring.Initialize += Events.Initialize;
            Terraria.On_Wiring.UpdateMech += Events.UpdateMech;
            Terraria.On_Wiring.HitSwitch += Events.HitSwitch;
            Terraria.On_Wiring.PokeLogicGate += Events.PokeLogicGate;
            Terraria.On_Wiring.Actuate += Events.Actuate;
            Terraria.On_Wiring.ActuateForced += Events.ActuateForced;
            //On.Terraria.Wiring.MassWireOperation += Events.MassWireOperation;
            Terraria.On_Wiring.GetProjectileSource += Events.GetProjectileSource;
            Terraria.On_Wiring.GetNPCSource += Events.GetNPCSource;
            Terraria.On_Wiring.GetItemSource += Events.GetItemSource;
            Terraria.On_Wiring.ToggleHolidayLight += Events.ToggleHolidayLight;
            Terraria.On_Wiring.ToggleHangingLantern += Events.ToggleHangingLantern;
            Terraria.On_Wiring.Toggle2x2Light += Events.Toggle2x2Light;
            Terraria.On_Wiring.ToggleLampPost += Events.ToggleLampPost;
            Terraria.On_Wiring.ToggleTorch += Events.ToggleTorch;
            Terraria.On_Wiring.ToggleLamp += Events.ToggleLamp;
            Terraria.On_Wiring.ToggleChandelier += Events.ToggleChandelier;
            Terraria.On_Wiring.ToggleCampFire += Events.ToggleCampFire;
            Terraria.On_Wiring.ToggleFirePlace += Events.ToggleFirePlace;
            WiringWrapper.Initialize();
        }

        /*
         * Go back to old wiring implementation
         */
        public static void RemoveEvents()
        {
            vanillaWiring = true;
            WorldFile.OnWorldLoad -= Accelerator.Preprocess;

            Terraria.On_Wiring.SetCurrentUser -= Events.SetCurrentUser;
            //On.Terraria.Wiring.Initialize -= Events.Initialize;
            Terraria.On_Wiring.UpdateMech -= Events.UpdateMech;
            Terraria.On_Wiring.HitSwitch -= Events.HitSwitch;
            Terraria.On_Wiring.PokeLogicGate -= Events.PokeLogicGate;
            Terraria.On_Wiring.Actuate -= Events.Actuate;
            Terraria.On_Wiring.ActuateForced -= Events.ActuateForced;
            //On.Terraria.Wiring.MassWireOperation -= Events.MassWireOperation;
            Terraria.On_Wiring.GetProjectileSource -= Events.GetProjectileSource;
            Terraria.On_Wiring.GetNPCSource -= Events.GetNPCSource;
            Terraria.On_Wiring.GetItemSource -= Events.GetItemSource;
            Terraria.On_Wiring.ToggleHolidayLight -= Events.ToggleHolidayLight;
            Terraria.On_Wiring.ToggleHangingLantern -= Events.ToggleHangingLantern;
            Terraria.On_Wiring.Toggle2x2Light -= Events.Toggle2x2Light;
            Terraria.On_Wiring.ToggleLampPost -= Events.ToggleLampPost;
            Terraria.On_Wiring.ToggleTorch -= Events.ToggleTorch;
            Terraria.On_Wiring.ToggleLamp -= Events.ToggleLamp;
            Terraria.On_Wiring.ToggleChandelier -= Events.ToggleChandelier;
            Terraria.On_Wiring.ToggleCampFire -= Events.ToggleCampFire;
            Terraria.On_Wiring.ToggleFirePlace -= Events.ToggleFirePlace;
            Wiring.Initialize();
        }

        public override void Load()
        {
            base.Load();
            Terraria.On_Netplay.UpdateConnectedClients += UpdateConnectedClients;
            //Array.Resize(ref Main.npc, 1000);
            //for (int i = 201; i < Main.npc.Length; ++i)
            //{
            //    Main.npc[i] = new NPC();
            //    Main.npc[i].whoAmI = i;
            //}

            Terraria.On_NPC.NewNPC += NewNPC;

            AddEvents();

        }

        public override void Unload()
        {
            base.Unload();
            Terraria.On_Netplay.UpdateConnectedClients -= UpdateConnectedClients;
            Terraria.On_NPC.NewNPC -= NewNPC;

            RemoveEvents();
        }

        public static int NewNPC(Terraria.On_NPC.orig_NewNPC orig,
            IEntitySource source, int X, int Y, int Type, int Start = 0, float ai0 = 0f, float ai1 = 0f,
            float ai2 = 0f, float ai3 = 0f, int Target = 255)
        {
            if(Type != 488)
                return orig(source, X, Y, Type, Start, ai0, ai1, ai2, ai3, Target);

            int num = -1;
            for (int i = 5; i < Main.npc.Length; i++)
            {
                if (!Main.npc[i].active)
                {
                    num = i;
                    break;
                }
            }
            if (num >= 0)
            {
                Main.npc[num] = new NPC();
                Main.npc[num].SetDefaults(Type);
                Main.npc[num].whoAmI = num;
                Main.npc[num].position.X = X - Main.npc[num].width / 2;
                Main.npc[num].position.Y = Y - Main.npc[num].height;
                Main.npc[num].active = true;
                Main.npc[num].timeLeft = (int)((double)NPC.activeTime * 1.25);
                Main.npc[num].wet = Collision.WetCollision(Main.npc[num].position, Main.npc[num].width, Main.npc[num].height);
                Main.npc[num].ai[0] = ai0;
                Main.npc[num].ai[1] = ai1;
                Main.npc[num].ai[2] = ai2;
                Main.npc[num].ai[3] = ai3;
                Main.npc[num].target = Target;
                
                return num;
            }
            throw new Exception("Failed to create new npc");
        }

        /*
         * This is obviously super ugly, I wish there was a cleaner way to do it
         * Might be a way with lambdas or something but I couldn't figure anything out in a reasonable amount of time
         */
        private class Events
        {
            public static void SetCurrentUser(Terraria.On_Wiring.orig_SetCurrentUser orig, int plr) { 
                WiringWrapper.SetCurrentUser(plr); 
            }
            //public static void Initialize(On.Terraria.Wiring.orig_Initialize orig)
            //{
            //    WiringWrapper.Initialize();
            //}
            public static void UpdateMech(Terraria.On_Wiring.orig_UpdateMech orig)
            {
                WiringWrapper.UpdateMech();
            }
            public static void HitSwitch(Terraria.On_Wiring.orig_HitSwitch orig, int i, int j)
            {
                WiringWrapper.HitSwitch(i, j);
            }
            public static void PokeLogicGate(Terraria.On_Wiring.orig_PokeLogicGate orig, int x, int y)
            {
                WiringWrapper.PokeLogicGate(x, y);
            }
            public static bool Actuate(Terraria.On_Wiring.orig_Actuate orig, int i, int j)
            {
                return WiringWrapper.Actuate(i, j);
            }
            public static void ActuateForced(Terraria.On_Wiring.orig_ActuateForced orig, int i, int j)
            {
                WiringWrapper.ActuateForced(i, j);
            }
            public static void MassWireOperation(Terraria.On_Wiring.orig_MassWireOperation orig, Point ps, Point pe, Player master)
            {
                WiringWrapper.MassWireOperation(ps, pe, master);
            }
            public static IEntitySource GetProjectileSource(Terraria.On_Wiring.orig_GetProjectileSource orig, int x, int y)
            {
                return WiringWrapper.GetProjectileSource(x, y);
            }
            public static IEntitySource GetNPCSource(Terraria.On_Wiring.orig_GetNPCSource orig, int x, int y)
            {
                return WiringWrapper.GetNPCSource(x, y);
            }
            public static IEntitySource GetItemSource(Terraria.On_Wiring.orig_GetItemSource orig, int x, int y)
            {
                return WiringWrapper.GetItemSource(x, y);
            }
            public static void ToggleHolidayLight(Terraria.On_Wiring.orig_ToggleHolidayLight orig, int i, int j, Tile tileCache, bool? f)
            {
                WiringWrapper.ToggleHolidayLight(i, j, tileCache, f);
            }
            public static void ToggleHangingLantern(Terraria.On_Wiring.orig_ToggleHangingLantern orig, int i, int j, Tile tileCache, bool? f, bool d)
            {
                WiringWrapper.ToggleHangingLantern(i, j, tileCache, f, d);
            }
            public static void Toggle2x2Light(Terraria.On_Wiring.orig_Toggle2x2Light orig, int i, int j, Tile tileCache, bool? f, bool d)
            {
                WiringWrapper.Toggle2x2Light(i, j, tileCache, f, d);
            }
            public static void ToggleLampPost(Terraria.On_Wiring.orig_ToggleLampPost orig, int i, int j, Tile tileCache, bool? f, bool d)
            {
                WiringWrapper.ToggleLampPost(i, j, tileCache, f, d);
            }
            public static void ToggleTorch(Terraria.On_Wiring.orig_ToggleTorch orig, int i, int j, Tile tileCache, bool? f)
            {
                WiringWrapper.ToggleTorch(i, j, tileCache, f);
            }
            public static void ToggleLamp(Terraria.On_Wiring.orig_ToggleLamp orig, int i, int j, Tile tileCache, bool? f, bool d)
            {
                WiringWrapper.ToggleLamp(i, j, tileCache, f, d);
            }
            public static void ToggleChandelier(Terraria.On_Wiring.orig_ToggleChandelier orig, int i, int j, Tile tileCache, bool? f, bool d)
            {
                WiringWrapper.ToggleChandelier(i, j, tileCache, f, d);
            }
            public static void ToggleCampFire(Terraria.On_Wiring.orig_ToggleCampFire orig, int i, int j, Tile tileCache, bool? f, bool d)
            {
                WiringWrapper.ToggleCampFire(i, j, tileCache, f, d);
            }
            public static void ToggleFirePlace(Terraria.On_Wiring.orig_ToggleFirePlace orig, int i, int j, Tile tileCache, bool? f, bool d)
            {
                WiringWrapper.ToggleFirePlace(i, j, tileCache, f, d);
            }
        }
    }

    public class WireHeadSystem : ModSystem
    {
        public override void OnWorldLoad()
        {
            base.OnWorldLoad();
        }

        public override void PreSaveAndQuit()
        {
            if (!WireHead.vanillaWiring)
                Accelerator.BringInSync();
            TerraCC.disable();
            base.PreSaveAndQuit();
        }

        // This used to be PostUpdateWorld, have no idea why it randomly broke
        public override void PostUpdateEverything()
        {
            foreach (var action in WireHead.toExec)
            {
                action();
            }
            WireHead.toExec.Clear();
            
            if(WireHead.useTerracc){
                if(Accelerator.numToHit > 0){
                    /* Console.WriteLine($"Triggering r:{Accelerator.toHit[0,0]}, b: {Accelerator.toHit[0,1]}, g: {Accelerator.toHit[0,1]}, y: {Accelerator.toHit[0,3]}"); */
                }
                TerraCC.trigger(Accelerator.toHit, Accelerator.numToHit);
                Accelerator.numToHit = 0;
                Accelerator.SyncPb();
                int cc = TerraCC.read_clock();
                // Only do even number of times, bringinsync handles last one if odd
                /* Console.WriteLine($"Diff: {(cc-Accelerator.clockCount)}, to trigger: {2*((cc-Accelerator.clockCount)/2)}, clockGroup: {Accelerator.clockGroup}, size: {Accelerator.triggerable[Accelerator.clockGroup].Length}"); */
                for(int i = 0; i < 2*((cc-Accelerator.clockCount)/2); ++i){
                    WiringWrapper._teleport[0].X = -1f;
                    WiringWrapper._teleport[0].Y = -1f;
                    WiringWrapper._teleport[1].X = -1f;
                    WiringWrapper._teleport[1].Y = -1f;
                    foreach(uint tile in Accelerator.triggerable[Accelerator.clockGroup]){
                        Point16 p = Accelerator.uint2Point(tile);
                        if(Accelerator.standardLamps[p.X,p.Y]) continue;
                        Accelerator.HitWireSingle(tile);
                        /* Console.WriteLine($"Triggering, x:{p.X}, y:{p.Y}"); */
                    }
                    if (WiringWrapper._teleport[0].X >= 0f && WiringWrapper._teleport[1].X >= 0f)
                        WiringWrapper.Teleport();
                }
                Accelerator.clockCount = cc;
                if(Accelerator.clockCount > Accelerator.clockMax){
                    Console.WriteLine("Clock finished");
                    Accelerator.clockCount = 0;
                    TerraCC.set_clock(-1);
                }
            }

            // Only fast refresh in single player to minimize network stuff
            if (Main.netMode == NetmodeID.SinglePlayer){
                Accelerator.BringInSync(false);
            }

            base.PostUpdateWorld();
        }
    }
}
