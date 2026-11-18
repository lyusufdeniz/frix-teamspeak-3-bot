// Frix: eski botla aynı görünümde renkli kanal mesajları.

using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using TSLib;

namespace TS3AudioBot;

public static class FrixChat
{
	public const string Green = "#00FF00";
	public const string Red = "#FF5555";

	private static readonly ConcurrentDictionary<string, string> Names = new();

	public static void RememberName(Uid? uid, string? name)
	{
		if (uid is { } u && !string.IsNullOrEmpty(u.Value) && !string.IsNullOrEmpty(name))
			Names[u.Value] = name;
	}

	public static string NameOf(Uid? uid)
	{
		if (uid is null || uid.Value == Uid.Anonymous || uid.Value == Uid.Null)
			return FrixText.Raw("user.webpanel");
		return Names.TryGetValue(uid.Value.Value, out var n) ? n : FrixText.Raw("user.unknown");
	}

	// Panel → Komutlar → Bot cevapları → Görünüm ayarları (meta tablosu)
	public static string DefaultColor => Meta("chat_color", Green);
	public static string ErrorColor => Meta("chat_error_color", Red);
	public static bool Bold => Meta("chat_bold", "1") != "0";

	private static readonly Regex ColorValue = new(@"^(#[0-9A-Fa-f]{3}|#[0-9A-Fa-f]{6}|[A-Za-z]{3,20})$", RegexOptions.Compiled);
	public static bool IsValidColor(string c) => ColorValue.IsMatch(c);

	private static string Meta(string key, string fallback)
	{
		try { return Frix.FrixDb.GetMeta(key) is { Length: > 0 } v ? v : fallback; }
		catch { return fallback; }
	}

	/// <summary>
	/// Mesajı varsayılan renk/kalınlıkla sarar. Metin kendi [color] etiketini içeriyorsa
	/// (panelden BBCode ile düzenlenmişse) olduğu gibi gönderilir. Varsayılan renk "none" ise renk eklenmez.
	/// </summary>
	public static string Colorize(string message) => Color(message, DefaultColor);

	/// <summary>Mesajı renklendirir. Değişkenler FrixText.Get içinde zaten etkisizleştirilir.</summary>
	public static string Color(string message, string color = Green)
	{
		if (string.IsNullOrWhiteSpace(message) || message.Contains("[color", System.StringComparison.OrdinalIgnoreCase))
			return message;
		if (color == Green) color = DefaultColor;
		else if (color == Red) color = ErrorColor;
		var body = color.Equals("none", System.StringComparison.OrdinalIgnoreCase) ? message : $"[color={color}]{message}[/color]";
		return Bold && !message.Contains("[b]", System.StringComparison.OrdinalIgnoreCase) ? $"[b]{body}[/b]" : body;
	}

	private static readonly Regex YtId = new(@"(?:youtube\.com/(?:watch\?(?:.*&)?v=|shorts/|live/|embed/)|youtu\.be/|music\.youtube\.com/watch\?(?:.*&)?v=)([A-Za-z0-9_-]{11})", RegexOptions.IgnoreCase | RegexOptions.Compiled);

	public static string? YoutubeId(string url)
	{
		var m = YtId.Match(url);
		return m.Success ? m.Groups[1].Value : null;
	}

	/// <summary>Kullanıcıdan/YouTube'dan gelen metindeki BBCode'u etkisiz bırakır.</summary>
	public static string Escape(string text) => text.Replace("[", "［").Replace("]", "］");
}
