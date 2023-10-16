using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using System.Text;

namespace WireHead.Items
{
    public class GroupStaff : ModItem
    {
        public override void SetStaticDefaults()
        {
            // DisplayName.SetDefault("Group Staff");
            // Tooltip.SetDefault("Prints groups under mouse to chat on use.");
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
            StringBuilder sb = new StringBuilder();
            string[] clrs = {"Red", "Blue", "Green", "Yellow"};
            for(int c = 0; c < Accelerator.colors; ++c){
                sb.Append(clrs[c] + ": ");
                int g = Accelerator.wireGroup[x,y,c];
                sb.Append(g);
                if(g>= 0){
                    sb.Append($" ({(Accelerator.groupState[g] ? 1 : 0)}, {(Accelerator.groupOutOfSync[g] ? 1 : 0)})");
                }
                if(c != 3){
                    sb.Append(", ");
                }
            }
            Main.NewText(sb.ToString());

            return true;
        }
    }
}
