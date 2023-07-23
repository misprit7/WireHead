using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;

namespace WireHead.Items
{
    public class GroupStaff : ModItem
    {
        public override void SetStaticDefaults()
        {
            DisplayName.SetDefault("Group Staff");
            Tooltip.SetDefault("Prints groups under mouse to chat on use.");
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
            Main.NewText($"Red: {Accelerator.wireGroup[x,y,0]}, Blue: {Accelerator.wireGroup[x,y,1]}, Green: {Accelerator.wireGroup[x,y,2]}, Yellow: {Accelerator.wireGroup[x,y,3]}");

            return true;
        }
    }
}
