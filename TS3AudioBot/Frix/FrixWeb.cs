// Frix: anahtarsız ücretsiz servisler — şarkı sözleri (lrclib.net), hava durumu (open-meteo.com),
// döviz kurları (open.er-api.com). Servislere sadece şarkı adı / şehir / döviz kodu gönderilir.

using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace TS3AudioBot.Frix;

public static class FrixWeb
{
	private static readonly HttpClient Http = Create();
	private static readonly ConcurrentDictionary<string, (DateTime at, object value)> Cache = new();

	private static HttpClient Create()
	{
		var h = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
		h.DefaultRequestHeaders.UserAgent.ParseAdd("FrixTS3Bot/2.0 (+https://github.com/lyusufdeniz/frix-teamspeak-bot)");
		return h;
	}

	private static async Task<T> Cached<T>(string key, TimeSpan ttl, Func<Task<T>> load) where T : notnull
	{
		if (Cache.TryGetValue(key, out var e) && DateTime.UtcNow - e.at < ttl && e.value is T t)
			return t;
		var v = await load();
		Cache[key] = (DateTime.UtcNow, v);
		return v;
	}

	// ---------------------------------------------------------------- şarkı sözleri

	public sealed record Lyrics(string Track, string Artist, string Text);

	/// <summary>YouTube başlıklarındaki "(Official Video)", "[4K]" gibi ekleri temizler.</summary>
	public static string CleanTitle(string title)
	{
		var t = System.Text.RegularExpressions.Regex.Replace(title, @"[\(\[][^\)\]]*(official|video|lyric|audio|clip|hd|4k|klip|resmi|music)[^\)\]]*[\)\]]", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
		t = System.Text.RegularExpressions.Regex.Replace(t, @"#\S+", "");
		t = System.Text.RegularExpressions.Regex.Replace(t, @"\s+(ft\.?|feat\.?)\s.*$", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
		return System.Text.RegularExpressions.Regex.Replace(t, @"\s{2,}", " ").Trim(' ', '-', '|');
	}

	public static Task<Lyrics?> FindLyrics(string query) => Cached("lyr:" + query.ToLowerInvariant(), TimeSpan.FromHours(6), async () =>
	{
		var url = "https://lrclib.net/api/search?q=" + Uri.EscapeDataString(CleanTitle(query));
		using var doc = JsonDocument.Parse(await Http.GetStringAsync(url));
		foreach (var item in doc.RootElement.EnumerateArray())
		{
			var text = item.TryGetProperty("plainLyrics", out var p) ? p.GetString() : null;
			if (string.IsNullOrWhiteSpace(text)) continue;
			return (Lyrics?)new Lyrics(item.GetProperty("trackName").GetString() ?? "", item.GetProperty("artistName").GetString() ?? "", text.Trim());
		}
		return (Lyrics?)null;
	}!)!;

	// ---------------------------------------------------------------- hava durumu

	public sealed record Weather(string Place, double Temp, double Feels, int Humidity, double Wind, int Code);

	public static Task<Weather?> GetWeather(string city, string lang) => Cached($"wx:{lang}:{city.ToLowerInvariant()}", TimeSpan.FromMinutes(10), async () =>
	{
		var geoUrl = $"https://geocoding-api.open-meteo.com/v1/search?count=1&language={lang}&name=" + Uri.EscapeDataString(city.Trim());
		using var geo = JsonDocument.Parse(await Http.GetStringAsync(geoUrl));
		if (!geo.RootElement.TryGetProperty("results", out var results) || results.GetArrayLength() == 0)
			return (Weather?)null;
		var r = results[0];
		var lat = r.GetProperty("latitude").GetDouble().ToString(CultureInfo.InvariantCulture);
		var lon = r.GetProperty("longitude").GetDouble().ToString(CultureInfo.InvariantCulture);
		var place = r.GetProperty("name").GetString() + (r.TryGetProperty("country", out var c) ? ", " + c.GetString() : "");
		var wxUrl = $"https://api.open-meteo.com/v1/forecast?latitude={lat}&longitude={lon}&current=temperature_2m,apparent_temperature,relative_humidity_2m,wind_speed_10m,weather_code";
		using var wx = JsonDocument.Parse(await Http.GetStringAsync(wxUrl));
		var cur = wx.RootElement.GetProperty("current");
		return (Weather?)new Weather(place, cur.GetProperty("temperature_2m").GetDouble(), cur.GetProperty("apparent_temperature").GetDouble(),
			(int)cur.GetProperty("relative_humidity_2m").GetDouble(), cur.GetProperty("wind_speed_10m").GetDouble(), cur.GetProperty("weather_code").GetInt32());
	}!)!;

	/// <summary>WMO hava kodu → kısa açıklama.</summary>
	public static string WeatherText(int code, string lang)
	{
		var tr = lang == "tr";
		return code switch
		{
			0 => tr ? "☀️ açık" : "☀️ clear",
			1 or 2 => tr ? "🌤️ parçalı bulutlu" : "🌤️ partly cloudy",
			3 => tr ? "☁️ kapalı" : "☁️ overcast",
			45 or 48 => tr ? "🌫️ sisli" : "🌫️ fog",
			>= 51 and <= 57 => tr ? "🌦️ çisenti" : "🌦️ drizzle",
			>= 61 and <= 67 => tr ? "🌧️ yağmurlu" : "🌧️ rain",
			>= 71 and <= 77 => tr ? "❄️ karlı" : "❄️ snow",
			>= 80 and <= 82 => tr ? "🌧️ sağanak" : "🌧️ showers",
			85 or 86 => tr ? "🌨️ kar sağanağı" : "🌨️ snow showers",
			>= 95 => tr ? "⛈️ fırtına" : "⛈️ thunderstorm",
			_ => "🌡️",
		};
	}

	// ---------------------------------------------------------------- döviz

	/// <summary>1 birim <paramref name="from"/> kaç <paramref name="to"/> eder.</summary>
	public static async Task<(double rate, string updated)?> Rate(string from, string to)
	{
		from = from.ToUpperInvariant();
		to = to.ToUpperInvariant();
		var data = await Cached("fx:" + from, TimeSpan.FromMinutes(30), async () =>
		{
			var json = await Http.GetStringAsync("https://open.er-api.com/v6/latest/" + Uri.EscapeDataString(from));
			return json;
		});
		using var doc = JsonDocument.Parse(data);
		if (doc.RootElement.GetProperty("result").GetString() != "success")
			return null;
		if (!doc.RootElement.GetProperty("rates").TryGetProperty(to, out var rate))
			return null;
		var updated = doc.RootElement.TryGetProperty("time_last_update_utc", out var u) ? u.GetString() ?? "" : "";
		return (rate.GetDouble(), updated);
	}
}
