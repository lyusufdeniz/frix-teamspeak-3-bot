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

	/// <summary>Kendi rengi olmayan mesajları yeşil ve kalın yapar.</summary>
	public static string Colorize(string message)
	{
		if (string.IsNullOrWhiteSpace(message) || message.Contains("[color", System.StringComparison.OrdinalIgnoreCase))
			return message;
		return $"[b][color={Green}]{message}[/color][/b]";
	}

	public static string Color(string message, string color = Green)
		=> $"[b][color={color}]{Escape(message)}[/color][/b]";

	private static readonly Regex YtId = new(@"(?:youtube\.com/(?:watch\?(?:.*&)?v=|shorts/|live/|embed/)|youtu\.be/|music\.youtube\.com/watch\?(?:.*&)?v=)([A-Za-z0-9_-]{11})", RegexOptions.IgnoreCase | RegexOptions.Compiled);

	public static string? YoutubeId(string url)
	{
		var m = YtId.Match(url);
		return m.Success ? m.Groups[1].Value : null;
	}

	/// <summary>Kullanıcıdan/YouTube'dan gelen metindeki BBCode'u etkisiz bırakır.</summary>
	public static string Escape(string text) => text.Replace("[", "［").Replace("]", "］");
}
