using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using Terraria.ObjectData;
using Terraria.ID;

namespace WireHead.Tiles
{
	public class ColorPixelBox : ModTile
	{
		// Probably a better way to do this but it saves a bunch of typing
		public static int ID = ModContent.TileType<ColorPixelBox>();
		public override void SetStaticDefaults()
		{

			Main.tileFrameImportant[Type] = true;
			AddMapEntry(new Color(200, 200, 200));
		}
	}
}
