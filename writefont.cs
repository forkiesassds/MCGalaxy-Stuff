//reference System.Drawing.dll
using System;
using System.Drawing;
using System.IO;
using MCGalaxy.Drawing;
using MCGalaxy.Drawing.Brushes;
using MCGalaxy.Drawing.Ops;
using MCGalaxy.Maths;
using BlockID = System.UInt16;
using MCGalaxy.Commands;

namespace MCGalaxy.Commands.Building {
	public sealed class WriteFontPlugin : Plugin {
		public override string name { get { return "WriteFontPlugin"; } }
		public override string creator { get { return ""; } }
		public override string welcome { get { return ""; } }
		public override string MCGalaxy_Version { get { return "1.0.1"; } }
		
		public override void Load(bool startup) {
			Command.Register(new CmdWriteFont());
			Command.Register(new CmdWriteChat());
		}
		public override void Unload(bool shutdown) {
			Command.Unregister(Command.Find("WriteFont"));
			Command.Unregister(Command.Find("WriteChat"));
		}
		
	}
	
	class CmdWriteFont : DrawCmd {
		public override string name { get { return "WriteFont"; } }
		public override LevelPermission defaultRank { get { return LevelPermission.Guest; } }
		
		protected override string SelectionType { get { return "direction"; } }
		protected override string PlaceMessage { get { return "Place or break two blocks to determine direction."; } }
		
		// TODO: Absolutely filthy copy paste! Fix in MCGalaxy
		BrushFactory MakeBrush(DrawArgs args) {
			args.BrushName = args.Player.BrushName;
			args.BrushArgs = "";
			GetBrush(args);
			
			if (args.BrushArgs.Length == 0) args.BrushArgs = args.Player.DefaultBrushArgs;
			return BrushFactory.Find(args.BrushName);
		}
		// END filthy copy paste
		
		public override void Use(Player p, string message, CommandData data) {
			string[] args = message.SplitSpaces(2);
			if (args.Length < 2) { Help(p); return; }
			string font = args[0].ToLower();
			
			byte scale = 1, spacing = 1;
			bool validFont = false;
			foreach (string fontFile in GetFonts()) { if (fontFile == font) { validFont = true; break; } }
			if (!validFont) {
				p.Message("There is no font \"{0}\".", font);
				MessageFonts(p);
				return;
			}
			
			if (!Formatter.ValidName(p, font, "file")) return;
			string path = "extra/fonts/" + font + ".png";
			if (!File.Exists(path)) { p.Message("%WFont {0} doesn't exist", path); return; }
			
			FontWriteDrawOp op = new FontWriteDrawOp();
			op.Scale = scale; op.Spacing = spacing;
			op.Path  = path;  op.Text    = args[1];
			
			// TODO: filthy copy paste
			DrawArgs dArgs = new DrawArgs();
			dArgs.Message = message;
			dArgs.Player = p;
			dArgs.Op = op;
			
			// Validate the brush syntax is correct
			BrushFactory factory = MakeBrush(dArgs);
			BrushArgs bArgs = new BrushArgs(p, dArgs.BrushArgs, dArgs.Block);
			if (!factory.Validate(bArgs)) return;
			
			p.Message(PlaceMessage);
			p.MakeSelection(MarksCount, "Selecting " + SelectionType + " for %S" + dArgs.Op.Name, dArgs, DoDraw);
			// END filthy copy paste
		}
		
		protected override DrawOp GetDrawOp(DrawArgs dArgs) { return null; }
		
		protected override void GetMarks(DrawArgs dArgs, ref Vec3S32[] m) {
			if (m[0].X != m[1].X || m[0].Z != m[1].Z) return;
			dArgs.Player.Message("No direction was selected");
			m = null;
		}
		
		protected override void GetBrush(DrawArgs dArgs) { dArgs.BrushArgs = ""; }

		public override void Help(Player p) {
			p.Message("%T/WriteFont [font] [message]");
			p.Message("%HWrites [message] in blocks. Supports color codes.");
			MessageFonts(p);
			p.Message("%HUse %T/WriteChat %Hfor default font shortcut.");
		}
		static void MessageFonts(Player p) {
			p.Message("&HAvailable fonts: &b{0}", String.Join("&H, &b", GetFonts()));
		}
		static string[] GetFonts() {
			const string directory = "extra/fonts/";
			DirectoryInfo info = new DirectoryInfo(directory);
			FileInfo[] fontFiles = info.GetFiles();
			string[] allFonts = new string[fontFiles.Length];
			for (int i = 0; i < allFonts.Length; i++) {
				allFonts[i] = fontFiles[i].Name.Replace(".png", "");
			}
			return allFonts;
		}
	}
	
	sealed class CmdWriteChat : CmdWriteFont {
		public override string name { get { return "WriteChat"; } }

		public override void Use(Player p, string message, CommandData data) {
			if (message.Length == 0) { Help(p); return; }
			base.Use(p, "default " + message, data);
		}

		public override void Help(Player p) {
			p.Message("%T/WriteChat [message]");
			p.Message("%HWrites [message] with NA2 chat font. Supports color codes.");
			p.Message("%HSee %T/WriteFont %Hfor other fonts.");
		}
	}
	
	class FontWriteDrawOp : DrawOp {
		public override string Name { get { return "FontWrite"; } }
		public string Text, Path;
		public byte Scale, Spacing;
		
		IPaletteMatcher selector = new RgbPaletteMatcher();
		ImagePalette palette     = ImagePalette.Find("Color");
		
		public override long BlocksAffected(Level lvl, Vec3S32[] marks) {
			// TODO: Lazyyyyyyyy
			return Text.Length * 64;
		}
		
		Vec3S32 dir, pos;
		public override void Perform(Vec3S32[] marks, MCGalaxy.Drawing.Brushes.Brush brush, DrawOpOutput output) {
			Vec3S32 p1 = marks[0], p2 = marks[1];
			if (Math.Abs(p2.X - p1.X) > Math.Abs(p2.Z - p1.Z)) {
				dir.X = p2.X > p1.X ? 1 : -1;
			} else {
				dir.Z = p2.Z > p1.Z ? 1 : -1;
			}
			
			pos = p1;
			
			using (Bitmap img = new Bitmap(Path))
				using (PixelGetter src = new PixelGetter(img))
			{
				if (img.Width != 128 || img.Height != 128)
					throw new InvalidOperationException("Font must be 128x128 image");
				
				src.Init();
				
				for (int i = 0; i < Text.Length; i++) {
					char c = Text[i].UnicodeToCp437();
					DrawLetter(Player, c, src, brush, output); 
				}
			}
		}
		
		static int GetTileWidth(PixelGetter src, int x, int y) {
			for (int xx = 7; xx >= 0; xx--) {
				// Is there a pixel in this column
				for (int yy = 0; yy < 8; yy++) {
					if (src.Get(x + xx, y + yy).A > 20) return xx + 1;
				}
			}
			return 0;
		}
		
		void DrawLetter(Player p, char c, PixelGetter src, MCGalaxy.Drawing.Brushes.Brush brush, DrawOpOutput output) {
			int Y = (int)(c >> 4)   * 8;
			int X = (int)(c & 0x0F) * 8;
			int width = GetTileWidth(src, X, Y);
			
			if (width == 0) {
				if (c != ' ') p.Message("\"{0}\" is not currently supported, replacing with space.", c);
				pos += dir * (2 * Scale);
			} else {
				for (int xx = 0; xx < width; xx++) {
					for (int yy = 0; yy < 8; yy++) {
						Pixel P = src.Get(X + xx, Y + (7-yy));
						if (P.A <= 127) continue;
						
						for (int ver = 0; ver < Scale; ver++)
							for (int hor = 0; hor < Scale; hor++)
						{
							int x = pos.X + dir.X * hor, y = pos.Y + yy * Scale + ver, z = pos.Z + dir.Z * hor;
							output(Place((ushort)x, (ushort)y, (ushort)z, brush));
						}
					}
					pos += dir * Scale;
				}
			}
			pos += dir * Spacing;
		}
	}
	
}


