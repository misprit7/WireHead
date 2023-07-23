using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;

namespace WireHead.Items
{
    public class TriggerStaff : ModItem
    {
        public override void SetStaticDefaults()
        {
            DisplayName.SetDefault("Trigger Staff");
            Tooltip.SetDefault("Triggers the wires at your cursor.");
        }

        public override void SetDefaults()
        {
            Item.width = 24;
            Item.height = 28;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.useTime = 15;
            Item.useAnimation = 15;
            Item.useTurn = true;
            Item.autoReuse = true;
            Item.rare = ItemRarityID.Master;
            Item.value = Item.sellPrice(platinum: 100);
            Item.mana = 0;
            Item.mech = true;
        }

        public override bool? UseItem(Player player)
        {
            int x = Player.tileTargetX;
            int y = Player.tileTargetY;
            if(WireHead.vanillaWiring)
                Wiring.TripWire(x, y, 1, 1);
            else
                WiringWrapper.TripWire(x, y, 1, 1);

            return true;
        }
    }
}
