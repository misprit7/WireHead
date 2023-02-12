using Terraria.ModLoader;
using Terraria.IO;
using WiringUtils.Commands;
using System;
using Steamworks;
using Terraria;

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
            On.Terraria.Netplay.UpdateConnectedClients += UpdateConnectedClients;
        }
    }
}