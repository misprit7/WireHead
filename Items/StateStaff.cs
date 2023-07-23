using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace WireHead.Items
{
    public class StateStaff : ModItem
    {
        public override void SetStaticDefaults()
        {
            DisplayName.SetDefault("State Staff");
            Tooltip.SetDefault("Toggles logic gate lamp under cursor.");
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
        }

        public override bool? UseItem(Player player)
        {
            int x = Player.tileTargetX;
            int y = Player.tileTargetY;
            Tile tile = Main.tile[x, y];

            if (tile.TileType == TileID.LogicGateLamp && tile.TileFrameX < 36){
                if (tile.TileFrameX == 0) tile.TileFrameX = 18;
                else if (tile.TileFrameX == 18) tile.TileFrameX = 0;
            } else{
                Main.NewText($"No toggleable lamp not under cursor!");
            }

            return true;
        }
    }
}
