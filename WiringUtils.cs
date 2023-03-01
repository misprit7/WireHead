using Terraria.ModLoader;
using Terraria.IO;
using WiringUtils.Commands;
using System;
using Steamworks;
using Terraria;
using SteelSeries.GameSense;
using Microsoft.Xna.Framework;
using Terraria.DataStructures;

namespace WiringUtils
{
	public class WiringUtils : Mod
	{
        private static void UpdateConnectedClients(On.Terraria.Netplay.orig_UpdateConnectedClients orig)
        {
            // Forces update even if no clients connected by making it think there are always clients
            orig();
            Netplay.HasClients = true;
        }
        public override void Load()
        {
            base.Load();
            
            WorldFile.OnWorldLoad += Accelerator.Preprocess;

            On.Terraria.Wiring.SetCurrentUser += Events.SetCurrentUser;
            //On.Terraria.Wiring.Initialize += Events.Initialize;
            On.Terraria.Wiring.UpdateMech += Events.UpdateMech;
            On.Terraria.Wiring.HitSwitch += Events.HitSwitch;
            On.Terraria.Wiring.PokeLogicGate += Events.PokeLogicGate;
            On.Terraria.Wiring.Actuate += Events.Actuate;
            On.Terraria.Wiring.ActuateForced += Events.ActuateForced;
            //On.Terraria.Wiring.MassWireOperation += Events.MassWireOperation;
            On.Terraria.Wiring.GetProjectileSource += Events.GetProjectileSource;
            On.Terraria.Wiring.GetNPCSource += Events.GetNPCSource;
            On.Terraria.Wiring.GetItemSource += Events.GetItemSource;
            On.Terraria.Wiring.ToggleHolidayLight += Events.ToggleHolidayLight;
            On.Terraria.Wiring.ToggleHangingLantern += Events.ToggleHangingLantern;
            On.Terraria.Wiring.Toggle2x2Light += Events.Toggle2x2Light;
            On.Terraria.Wiring.ToggleLampPost += Events.ToggleLampPost;
            On.Terraria.Wiring.ToggleTorch += Events.ToggleTorch;
            On.Terraria.Wiring.ToggleLamp += Events.ToggleLamp;
            On.Terraria.Wiring.ToggleChandelier += Events.ToggleChandelier;
            On.Terraria.Wiring.ToggleCampFire += Events.ToggleCampFire;
            On.Terraria.Wiring.ToggleFirePlace += Events.ToggleFirePlace;

            On.Terraria.Netplay.UpdateConnectedClients += UpdateConnectedClients;

            WiringWrapper.Initialize();
        }

        public override void Unload()
        {
            base.Unload();

            WorldFile.OnWorldLoad -= Accelerator.Preprocess;

            On.Terraria.Wiring.SetCurrentUser -= Events.SetCurrentUser;
            //On.Terraria.Wiring.Initialize -= Events.Initialize;
            On.Terraria.Wiring.UpdateMech -= Events.UpdateMech;
            On.Terraria.Wiring.HitSwitch -= Events.HitSwitch;
            On.Terraria.Wiring.PokeLogicGate -= Events.PokeLogicGate;
            On.Terraria.Wiring.Actuate -= Events.Actuate;
            On.Terraria.Wiring.ActuateForced -= Events.ActuateForced;
            //On.Terraria.Wiring.MassWireOperation -= Events.MassWireOperation;
            On.Terraria.Wiring.GetProjectileSource -= Events.GetProjectileSource;
            On.Terraria.Wiring.GetNPCSource -= Events.GetNPCSource;
            On.Terraria.Wiring.GetItemSource -= Events.GetItemSource;
            On.Terraria.Wiring.ToggleHolidayLight -= Events.ToggleHolidayLight;
            On.Terraria.Wiring.ToggleHangingLantern -= Events.ToggleHangingLantern;
            On.Terraria.Wiring.Toggle2x2Light -= Events.Toggle2x2Light;
            On.Terraria.Wiring.ToggleLampPost -= Events.ToggleLampPost;
            On.Terraria.Wiring.ToggleTorch -= Events.ToggleTorch;
            On.Terraria.Wiring.ToggleLamp -= Events.ToggleLamp;
            On.Terraria.Wiring.ToggleChandelier -= Events.ToggleChandelier;
            On.Terraria.Wiring.ToggleCampFire -= Events.ToggleCampFire;
            On.Terraria.Wiring.ToggleFirePlace -= Events.ToggleFirePlace;

            On.Terraria.Netplay.UpdateConnectedClients -= UpdateConnectedClients;
        }

        private class Events
        {
            public static void SetCurrentUser(On.Terraria.Wiring.orig_SetCurrentUser orig, int plr) { 
                WiringWrapper.SetCurrentUser(plr); 
            }
            //public static void Initialize(On.Terraria.Wiring.orig_Initialize orig)
            //{
            //    WiringWrapper.Initialize();
            //}
            public static void UpdateMech(On.Terraria.Wiring.orig_UpdateMech orig)
            {
                WiringWrapper.UpdateMech();
            }
            public static void HitSwitch(On.Terraria.Wiring.orig_HitSwitch orig, int i, int j)
            {
                WiringWrapper.HitSwitch(i, j);
            }
            public static void PokeLogicGate(On.Terraria.Wiring.orig_PokeLogicGate orig, int x, int y)
            {
                WiringWrapper.PokeLogicGate(x, y);
            }
            public static bool Actuate(On.Terraria.Wiring.orig_Actuate orig, int i, int j)
            {
                return WiringWrapper.Actuate(i, j);
            }
            public static void ActuateForced(On.Terraria.Wiring.orig_ActuateForced orig, int i, int j)
            {
                WiringWrapper.ActuateForced(i, j);
            }
            public static void MassWireOperation(On.Terraria.Wiring.orig_MassWireOperation orig, Point ps, Point pe, Player master)
            {
                WiringWrapper.MassWireOperation(ps, pe, master);
            }
            public static IEntitySource GetProjectileSource(On.Terraria.Wiring.orig_GetProjectileSource orig, int x, int y)
            {
                return WiringWrapper.GetProjectileSource(x, y);
            }
            public static IEntitySource GetNPCSource(On.Terraria.Wiring.orig_GetNPCSource orig, int x, int y)
            {
                return WiringWrapper.GetNPCSource(x, y);
            }
            public static IEntitySource GetItemSource(On.Terraria.Wiring.orig_GetItemSource orig, int x, int y)
            {
                return WiringWrapper.GetItemSource(x, y);
            }
            public static void ToggleHolidayLight(On.Terraria.Wiring.orig_ToggleHolidayLight orig, int i, int j, Tile tileCache, bool? f)
            {
                WiringWrapper.ToggleHolidayLight(i, j, tileCache, f);
            }
            public static void ToggleHangingLantern(On.Terraria.Wiring.orig_ToggleHangingLantern orig, int i, int j, Tile tileCache, bool? f, bool d)
            {
                WiringWrapper.ToggleHangingLantern(i, j, tileCache, f, d);
            }
            public static void Toggle2x2Light(On.Terraria.Wiring.orig_Toggle2x2Light orig, int i, int j, Tile tileCache, bool? f, bool d)
            {
                WiringWrapper.Toggle2x2Light(i, j, tileCache, f, d);
            }
            public static void ToggleLampPost(On.Terraria.Wiring.orig_ToggleLampPost orig, int i, int j, Tile tileCache, bool? f, bool d)
            {
                WiringWrapper.ToggleLampPost(i, j, tileCache, f, d);
            }
            public static void ToggleTorch(On.Terraria.Wiring.orig_ToggleTorch orig, int i, int j, Tile tileCache, bool? f)
            {
                WiringWrapper.ToggleTorch(i, j, tileCache, f);
            }
            public static void ToggleLamp(On.Terraria.Wiring.orig_ToggleLamp orig, int i, int j, Tile tileCache, bool? f, bool d)
            {
                WiringWrapper.ToggleLamp(i, j, tileCache, f, d);
            }
            public static void ToggleChandelier(On.Terraria.Wiring.orig_ToggleChandelier orig, int i, int j, Tile tileCache, bool? f, bool d)
            {
                WiringWrapper.ToggleChandelier(i, j, tileCache, f, d);
            }
            public static void ToggleCampFire(On.Terraria.Wiring.orig_ToggleCampFire orig, int i, int j, Tile tileCache, bool? f, bool d)
            {
                WiringWrapper.ToggleCampFire(i, j, tileCache, f, d);
            }
            public static void ToggleFirePlace(On.Terraria.Wiring.orig_ToggleFirePlace orig, int i, int j, Tile tileCache, bool? f, bool d)
            {
                WiringWrapper.ToggleFirePlace(i, j, tileCache, f, d);
            }
        }
    }
}