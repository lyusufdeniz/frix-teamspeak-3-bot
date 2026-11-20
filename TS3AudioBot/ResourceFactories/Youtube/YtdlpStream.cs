// Frix: yt-dlp -> ffmpeg doğrudan akış (indirmesiz).
// yt-dlp sesi stdout'a yazar, ffmpeg stdin'den okur. Canlı yayınlar da bu yolla çalışır.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace TS3AudioBot.ResourceFactories.Youtube;

public static class YtdlpStream
{
	private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();
	public const string Scheme = "ytdlp:";
	private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(6) };

	public static string MakeUri(string target) => Scheme + target;
	public static bool IsYtdlpUri(string uri) => uri.StartsWith(Scheme, StringComparison.Ordinal);
	public static string TargetOf(string uri) => uri[Scheme.Length..];

	public static string? FindExe()
	{
		return Frix.FrixTools.FindYtdlp() ?? YoutubeDlHelper.FindYoutubeDl()?.ytdlpath;
	}

	/// <summary>yt-dlp'yi sesi stdout'a yazacak şekilde başlatır.</summary>
	public static Process Start(string target, string metaFile)
	{
		var exe = FindExe() ?? throw new FileNotFoundException("yt-dlp bulunamadı");
		var url = target.Contains("://") ? target : "https://www.youtube.com/watch?v=" + target;
		var args = new StringBuilder()
			.Append("--no-warnings --no-progress --no-playlist --no-part --quiet ")
			// Önce sadece ses; canlı yayınlarda (HLS) en düşük video+ses formatları
			.Append("-f \"bestaudio/91/92/93/94/best\" ")
			.Append("--retries 5 --fragment-retries 10 ")
			.Append("--print-to-file \"%(title)s\t%(duration)s\t%(is_live)s\" \"").Append(metaFile).Append("\" ")
			.Append("-o - -- \"").Append(url.Replace("\"", "")).Append('"')
			.ToString();

		var proc = new Process
		{
			StartInfo = new ProcessStartInfo(exe, args)
			{
				UseShellExecute = false,
				CreateNoWindow = true,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				WorkingDirectory = AppContext.BaseDirectory,
				StandardErrorEncoding = Encoding.UTF8,
			},
			EnableRaisingEvents = true,
		};
		Log.Debug("yt-dlp akışı: {0} {1}", exe, args);
		proc.Start();
		return proc;
	}

	public sealed record Meta(string? Title, TimeSpan? Duration, bool IsLive);

	public static Meta? ReadMeta(string metaFile)
	{
		try
		{
			if (!File.Exists(metaFile))
				return null;
			var line = File.ReadAllText(metaFile, Encoding.UTF8).Split('\n').FirstOrDefault(l => l.Trim().Length > 0);
			if (line is null)
				return null;
			var parts = line.TrimEnd('\r').Split('\t');
			string? title = parts.Length > 0 && parts[0] != "NA" ? parts[0] : null;
			TimeSpan? dur = parts.Length > 1 && double.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d) && d > 0
				? TimeSpan.FromSeconds(d) : null;
			var live = parts.Length > 2 && parts[2].Trim().Equals("True", StringComparison.OrdinalIgnoreCase);
			return new Meta(title, dur, live);
		}
		catch (Exception ex)
		{
			Log.Debug(ex, "yt-dlp meta okunamadı");
			return null;
		}
	}

	/// <summary>YouTube başlığını yt-dlp'yi beklemeden hızlıca alır (oEmbed).</summary>
	public static async Task<string?> QuickTitle(string videoId, CancellationToken ct)
	{
		try
		{
			var url = "https://www.youtube.com/oembed?format=json&url=" + Uri.EscapeDataString("https://www.youtube.com/watch?v=" + videoId);
			using var res = await Http.GetAsync(url, ct);
			if (!res.IsSuccessStatusCode)
				return null;
			using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
			return doc.RootElement.TryGetProperty("title", out var t) ? t.GetString() : null;
		}
		catch
		{
			return null;
		}
	}

	public static string ExplainError(string stderr)
	{
		var text = string.Join(" ", stderr.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith("WARNING", StringComparison.Ordinal)));
		if (text.Contains("Sign in to confirm", StringComparison.OrdinalIgnoreCase) || text.Contains("not a bot", StringComparison.OrdinalIgnoreCase))
			return "YouTube bot doğrulaması istedi. Panelden YouTube çerez dosyası yükleyin (SSS'ye bakın).";
		if (text.Contains("Video unavailable", StringComparison.OrdinalIgnoreCase))
			return "Video kullanılamıyor.";
		if (text.Contains("live stream recording is not available", StringComparison.OrdinalIgnoreCase))
			return "Bu canlı yayının kaydı YouTube'da yok.";
		if (text.Contains("live event will begin", StringComparison.OrdinalIgnoreCase) || text.Contains("Premieres in", StringComparison.OrdinalIgnoreCase))
			return "Bu yayın henüz başlamadı.";
		if (text.Contains("Requested format is not available", StringComparison.OrdinalIgnoreCase))
			return "Bu video için çalınabilir bir ses formatı bulunamadı. yt-dlp'yi güncelleyin.";
		if (text.Contains("not available in your country", StringComparison.OrdinalIgnoreCase) || text.Contains("blocked it in your country", StringComparison.OrdinalIgnoreCase))
			return "Bu video sunucunun ülkesinde engelli.";
		if (text.Contains("members-only", StringComparison.OrdinalIgnoreCase) || text.Contains("Join this channel", StringComparison.OrdinalIgnoreCase))
			return "Bu video sadece kanal üyelerine açık.";
		if (text.Contains("copyright", StringComparison.OrdinalIgnoreCase))
			return "Video telif nedeniyle kaldırılmış.";
		if (text.Contains("Private video", StringComparison.OrdinalIgnoreCase))
			return "Bu video gizli.";
		if (text.Contains("confirm your age", StringComparison.OrdinalIgnoreCase))
			return "Yaş doğrulaması gereken video. Çerez dosyası gerekiyor.";
		if (text.Contains("Unsupported URL", StringComparison.OrdinalIgnoreCase))
			return "Bu link desteklenmiyor.";
		if (text.Contains("HTTP Error 403", StringComparison.OrdinalIgnoreCase))
			return "YouTube erişimi reddetti (403). yt-dlp'yi güncelleyin veya çerez yükleyin.";
		if (text.Length == 0)
			return "Akış başlatılamadı.";
		return text.Length > 220 ? text[..220] + "…" : text;
	}
}
