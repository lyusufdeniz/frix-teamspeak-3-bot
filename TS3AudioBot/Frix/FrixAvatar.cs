// Frix: bot boştayken gösterilen avatarı çalışma anında çizer.
// İçerik kullanıcının ayarlarından gelir: yerleşik komutlar (açıklamaları panelden düzenlenebilir),
// panelden eklenen özel komutlar ve radyo varsa !radyo. Ayarlar değişince tüm botlar yenilenir.

using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace TS3AudioBot.Frix;

public static class FrixAvatar
{
	private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();
	private static readonly object Sync = new();
	private static byte[]? cached;
	private static FontFamily? mono;
	private static FontFamily? monoBold;

	/// <summary>Panel ayarları değişince çağrılır; çalışan ve boştaki botların avatarını yeniler.</summary>
	public static Action? RefreshAllBots { get; set; }

	private const int Width = 300;
	private const int MaxRows = 14;

	public static void Invalidate()
	{
		lock (Sync) cached = null;
		try { RefreshAllBots?.Invoke(); }
		catch (Exception ex) { Log.Debug(ex, "Avatarlar yenilenemedi"); }
	}

	public static Stream? IdleStream()
	{
		try
		{
			lock (Sync)
			{
				cached ??= Render();
				return new MemoryStream(cached, false);
			}
		}
		catch (Exception ex)
		{
			Log.Warn(ex, "Dinamik avatar çizilemedi, varsayılan kullanılacak");
			return null;
		}
	}

	/// <summary>Panelde önizleme için PNG baytları.</summary>
	public static byte[] Preview()
	{
		lock (Sync)
			return cached ??= Render();
	}

	private static (FontFamily mono, FontFamily bold) Fonts()
	{
		if (mono is null || monoBold is null)
		{
			var collection = new FontCollection();
			var asm = Assembly.GetExecutingAssembly();
			foreach (var name in asm.GetManifestResourceNames().Where(n => n.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase)))
			{
				using var s = asm.GetManifestResourceStream(name)!;
				var fam = collection.Add(s);
				if (name.Contains("Bold", StringComparison.OrdinalIgnoreCase)) monoBold = fam;
				else mono = fam;
			}
			if (mono is null || monoBold is null)
				throw new InvalidOperationException("Avatar fontları bulunamadı");
		}
		return (mono.Value, monoBold.Value);
	}

	private static List<(string cmd, string desc)> Rows()
	{
		static string N(string b) => "!" + FrixCommands.Display(b);
		var bots = FrixPluginHost.RunningBots;
		bool On(string b) => FrixCommands.IsAvailableAny(b, bots);
		var rows = new List<(string, string)>();
		if (On("ara")) rows.Add((N("ara"), FrixText.Raw("avatar.ara")));
		if (On("radyo") && FrixRadio.List().Count > 0)
			rows.Add((N("radyo"), FrixText.Raw("avatar.radyo")));
		if (On("gec"))
		{
			rows.Add((N("gec"), FrixText.Raw("avatar.gec")));
			rows.Add((N("dur"), FrixText.Raw("avatar.dur")));
			rows.Add((N("bitir"), FrixText.Raw("avatar.bitir")));
			rows.Add((N("ses") + " 50", FrixText.Raw("avatar.ses")));
			rows.Add((N("calan"), FrixText.Raw("avatar.calan")));
			rows.Add((N("siradaki"), FrixText.Raw("avatar.siradaki")));
		}

		foreach (var c in FrixCommands.List().Where(c => c.Enabled))
		{
			var desc = !string.IsNullOrWhiteSpace(c.Description) ? c.Description!
				: c.Kind == "alias" ? "→ " + c.Value.Trim()
				: StripBb(c.Value);
			rows.Add(("!" + c.Trigger, desc));
		}
		rows.Add((N("yardim"), FrixText.Raw("avatar.yardim")));
		if (rows.Count > MaxRows)
			rows = [.. rows.Take(MaxRows - 1), rows[^1]];
		return rows;
	}

	private static string StripBb(string s)
	{
		var text = System.Text.RegularExpressions.Regex.Replace(s, @"\[/?[a-zA-Z*]+(=[^\]]*)?\]", "");
		text = text.Replace('\n', ' ').Trim();
		return text;
	}

	private static string Fit(string text, Font font, float maxWidth)
	{
		var opts = new TextOptions(font);
		if (TextMeasurer.MeasureSize(text, opts).Width <= maxWidth)
			return text;
		while (text.Length > 1 && TextMeasurer.MeasureSize(text + "…", opts).Width > maxWidth)
			text = text[..^1];
		return text.TrimEnd() + "…";
	}

	// Marka renkleri (lacivert tema)
	private static readonly Color Page = Color.ParseHex("16212D");
	private static readonly Color Surface = Color.ParseHex("1D2B3A");
	private static readonly Color Line = Color.ParseHex("2F4255");
	private static readonly Color Cream = Color.ParseHex("FFF6EA");
	private static readonly Color Muted = Color.ParseHex("9FB0C2");
	private static readonly Color Orange = Color.ParseHex("EC6B2D");
	private static readonly Color OrangeDark = Color.ParseHex("C9531C");
	private static readonly Color Ink = Color.ParseHex("2A201C");
	private static readonly Color Headphone = Color.ParseHex("F3E7D6");

	/// <summary>Kulaklıklı tilki (packaging/tools/make_brand.py ile aynı 100x100 koordinatlar).</summary>
	private static void DrawFox(IImageProcessingContext ctx, float ox, float oy, float size)
	{
		float k = size / 100f;
		PointF P(float x, float y) => new(ox + x * k, oy + y * k);
		void Poly(Color c, params (float x, float y)[] pts) => ctx.FillPolygon(c, pts.Select(p => P(p.x, p.y)).ToArray());
		void Arc(Color c, float x0, float y0, float x1, float y1, float a0, float a1, float w)
		{
			var cx = (x0 + x1) / 2; var cy = (y0 + y1) / 2; var rx = (x1 - x0) / 2; var ry = (y1 - y0) / 2;
			var pts = Enumerable.Range(0, 41).Select(i =>
			{
				var a = (a0 + (a1 - a0) * i / 40f) * MathF.PI / 180f;
				return P(cx + rx * MathF.Cos(a), cy + ry * MathF.Sin(a));
			}).ToArray();
			ctx.DrawLine(c, w * k, pts);
		}
		Arc(Headphone, 16, 13, 84, 81, 196, 344, 6);
		Poly(Orange, (26, 44), (34, 14), (48, 36));
		Poly(Orange, (74, 44), (66, 14), (52, 36));
		Poly(Ink, (31.5f, 38), (35, 23), (42, 34));
		Poly(Ink, (68.5f, 38), (65, 23), (58, 34));
		Poly(Orange, (22, 42), (78, 42), (50, 84));
		Poly(Cream, (22, 42), (50, 60), (50, 84));
		Poly(Cream, (78, 42), (50, 60), (50, 84));
		Poly(OrangeDark, (40, 42), (60, 42), (50, 58));
		Arc(Ink, 32, 47, 42, 55, 200, 340, 2.2f);
		Arc(Ink, 58, 47, 68, 55, 200, 340, 2.2f);
		ctx.Fill(Ink, new SixLabors.ImageSharp.Drawing.EllipsePolygon(P(50, 82).X, P(50, 82).Y, 7f * k, 6f * k));
		ctx.Fill(Headphone, RoundedRect(new RectangleF(P(12, 41).X, P(12, 41).Y, 13 * k, 23 * k), 5 * k));
		ctx.Fill(Headphone, RoundedRect(new RectangleF(P(75, 41).X, P(75, 41).Y, 13 * k, 23 * k), 5 * k));
		ctx.Fill(Orange, RoundedRect(new RectangleF(P(15, 46).X, P(15, 46).Y, 4 * k, 13 * k), 2 * k));
		ctx.Fill(Orange, RoundedRect(new RectangleF(P(81, 46).X, P(81, 46).Y, 4 * k, 13 * k), 2 * k));
	}

	private static byte[] Render()
	{
		var (monoFam, boldFam) = Fonts();
		var rows = Rows();
		const int scale = 2; // keskin yazı için 2x çiz, sonra küçült
		const int rowH = 30;
		const int top = 104;
		var height = top + rows.Count * rowH + 44;

		var title = boldFam.CreateFont(30 * scale);
		var sub = monoFam.CreateFont(11.5f * scale);
		var cmdFont = boldFam.CreateFont(15 * scale);
		var descFont = monoFam.CreateFont(12.5f * scale);

		using var img = new Image<Rgba32>(Width * scale, height * scale, Page);
		img.Mutate(ctx =>
		{
			float S(float v) => v * scale;

			// Başlık: tilki + "frix"
			ctx.Fill(Surface, new RectangleF(0, 0, S(Width), S(88)));
			ctx.Fill(Orange, new RectangleF(0, S(88), S(Width), S(2)));
			DrawFox(ctx, S(8), S(8), S(72));
			ctx.DrawText("frix", title, Cream, new PointF(S(86), S(12)));
			ctx.DrawText(Fit(FrixText.Raw("avatar.subtitle"), sub, S(Width - 96)), sub, Muted, new PointF(S(88), S(56)));

			for (int i = 0; i < rows.Count; i++)
			{
				var y = S(top + i * rowH);
				var (cmd, desc) = rows[i];
				if (i > 0) ctx.Fill(Line, new RectangleF(S(12), y - S(8), S(Width - 24), S(1)));
				ctx.DrawText(Fit(cmd, cmdFont, S(120)), cmdFont, Orange, new PointF(S(12), y));
				ctx.DrawText(Fit(desc, descFont, S(Width - 148)), descFont, Cream, new PointF(S(138), y + S(2)));
			}

			var footY = S(height - 30);
			ctx.Fill(Surface, new RectangleF(0, footY - S(6), S(Width), S(36)));
			ctx.DrawText(Fit(FrixCommands.ApplyNames(FrixText.Raw("avatar.footer")), sub, S(Width - 24)), sub, Muted, new PointF(S(12), footY + S(2)));
		});
		img.Mutate(x => x.Resize(Width, height, KnownResamplers.Lanczos3));
		using var ms = new MemoryStream();
		img.SaveAsPng(ms);
		return ms.ToArray();
	}

	private static SixLabors.ImageSharp.Drawing.IPath RoundedRect(RectangleF r, float radius)
		=> new SixLabors.ImageSharp.Drawing.ComplexPolygon(
			new SixLabors.ImageSharp.Drawing.RectangularPolygon(r.Left + radius, r.Top, r.Width - 2 * radius, r.Height),
			new SixLabors.ImageSharp.Drawing.RectangularPolygon(r.Left, r.Top + radius, r.Width, r.Height - 2 * radius),
			new SixLabors.ImageSharp.Drawing.EllipsePolygon(r.Left + radius, r.Top + radius, radius),
			new SixLabors.ImageSharp.Drawing.EllipsePolygon(r.Right - radius, r.Top + radius, radius),
			new SixLabors.ImageSharp.Drawing.EllipsePolygon(r.Left + radius, r.Bottom - radius, radius),
			new SixLabors.ImageSharp.Drawing.EllipsePolygon(r.Right - radius, r.Bottom - radius, radius));
}
