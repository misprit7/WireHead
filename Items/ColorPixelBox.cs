using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace WireHead.Items
{
	public class ColorPixelBox : ModItem
	{
		public override void SetStaticDefaults() {
			Item.ResearchUnlockCount = 25;
		}

		public override void SetDefaults() {
			Item.DefaultToPlaceableTile(ModContent.TileType<Tiles.ColorPixelBox>());
			/*Item.width = 20;*/
			/*Item.height = 20;*/
			Item.value = 750;
		}
	}
}
