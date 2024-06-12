using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using Terraria.ObjectData;
using Terraria.ID;

namespace WireHead.Tiles
{
	public class ColorPixelBox : ModTile
	{
		public override void SetStaticDefaults()
		{
			
			/*Main.tileSolid[Type] = false;*/
			/*Main.tileLighted[Type] = true;*/
			Main.tileFrameImportant[Type] = true;
			/*TileObjectData.newTile.FullCopyFrom(TileObjectData.GetTileData(TileID.PixelBox, 0));*/
			AddMapEntry(new Color(200, 200, 200));
		}
	}
}
