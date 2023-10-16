using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;

namespace WireHead.Items
{
    public class CoordStaff : ModItem
    {
        public override void SetStaticDefaults()
        {
            // DisplayName.SetDefault("Coordinate Staff");
            // Tooltip.SetDefault("Prints mouse coordinates to chat on use.");
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
            Main.NewText($"Mouse Coordinates: X = {x}, Y = {y}");

            return true;
        }
    }
}
