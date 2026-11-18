// Frix: panelden eklenen radyo istasyonları (!radyo <isim>)

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace TS3AudioBot.Frix;

public sealed record RadioStation(long Id, string Name, string Url, string? Genre);

public static class FrixRadio
{
	private static readonly HttpClient Http = CreateHttp();

	private static HttpClient CreateHttp()
	{
		var h = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
		h.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 Frix");
		return h;
	}

	public static List<RadioStation> List()
		=> FrixDb.Query("SELECT id, name, url, genre FROM radios ORDER BY name COLLATE NOCASE")
			.Select(r => new RadioStation((long)r["id"]!, (string)r["name"]!, (string)r["url"]!, r["genre"] as string))
			.ToList();

	public static RadioStation? Find(string query)
	{
		query = query.Trim();
		var all = List();
		return all.FirstOrDefault(r => r.Name.Equals(query, StringComparison.OrdinalIgnoreCase))
			?? all.FirstOrDefault(r => r.Name.StartsWith(query, StringComparison.OrdinalIgnoreCase))
			?? all.FirstOrDefault(r => r.Name.Contains(query, StringComparison.OrdinalIgnoreCase));
	}

	public static void Validate(string name, string url)
	{
		if (string.IsNullOrWhiteSpace(name) || name.Length > 40)
			throw new ArgumentException("Radyo adı 1-40 karakter olmalı.");
		if (!Uri.TryCreate(url, UriKind.Absolute, out var u) || (u.Scheme != "http" && u.Scheme != "https"))
			throw new ArgumentException("Geçerli bir http/https adresi girin.");
	}

	public static long Add(string name, string url, string? genre)
	{
		Validate(name, url);
		FrixDb.Exec("INSERT INTO radios(name,url,genre,created) VALUES($n,$u,$g,$c)",
			("$n", name.Trim()), ("$u", url.Trim()), ("$g", genre?.Trim()), ("$c", DateTime.UtcNow.ToString("O")));
		return (long)FrixDb.Scalar("SELECT id FROM radios WHERE name=$n", ("$n", name.Trim()))!;
	}

	public static void Update(long id, string name, string url, string? genre)
	{
		Validate(name, url);
		FrixDb.Exec("UPDATE radios SET name=$n, url=$u, genre=$g WHERE id=$id",
			("$n", name.Trim()), ("$u", url.Trim()), ("$g", genre?.Trim()), ("$id", id));
	}

	public static void Delete(long id) => FrixDb.Exec("DELETE FROM radios WHERE id=$id", ("$id", id));

	/// <summary>m3u / pls / xspf çalma listelerinden gerçek yayın adresini çıkarır.</summary>
	public static async Task<string> ResolveStreamUrl(string url, CancellationToken ct = default)
	{
		var ext = Path.GetExtension(new Uri(url).AbsolutePath).ToLowerInvariant();
		if (ext == ".m3u8")
			return url; // HLS: ffmpeg doğrudan oynatır

		var isPlaylistExt = ext is ".m3u" or ".pls" or ".xspf" or ".asx";
		using var req = new HttpRequestMessage(HttpMethod.Get, url);
		using var res = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
		res.EnsureSuccessStatusCode();
		var type = res.Content.Headers.ContentType?.MediaType?.ToLowerInvariant() ?? "";

		var looksPlaylist = isPlaylistExt || type.Contains("mpegurl") || type.Contains("scpls") || type.Contains("xspf") || type.Contains("x-ms-asf");
		if (!looksPlaylist)
			return url; // Doğrudan ses yayını

		if (type.Contains("mpegurl") && type.Contains("application/vnd.apple"))
			return url; // HLS

		var text = await ReadLimited(await res.Content.ReadAsStreamAsync(ct), 256 * 1024, ct);
		if (text.Contains("#EXT-X-", StringComparison.Ordinal))
			return url; // aslında HLS

		var candidates = new List<string>();
		foreach (var raw in text.Split('\n'))
		{
			var line = raw.Trim();
			var m = Regex.Match(line, @"^File\d+\s*=\s*(\S+)", RegexOptions.IgnoreCase); // pls
			if (m.Success) { candidates.Add(m.Groups[1].Value); continue; }
			m = Regex.Match(line, @"<location>\s*(\S+?)\s*</location>", RegexOptions.IgnoreCase); // xspf
			if (m.Success) { candidates.Add(m.Groups[1].Value); continue; }
			m = Regex.Match(line, @"href\s*=\s*""(\S+?)""", RegexOptions.IgnoreCase); // asx
			if (m.Success) { candidates.Add(m.Groups[1].Value); continue; }
			if (line.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || line.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
				candidates.Add(line); // m3u
		}
		return candidates.FirstOrDefault() ?? throw new InvalidDataException("Çalma listesinde yayın adresi bulunamadı.");
	}

	private static async Task<string> ReadLimited(Stream s, int max, CancellationToken ct)
	{
		var buf = new byte[max];
		int total = 0, n;
		while (total < max && (n = await s.ReadAsync(buf.AsMemory(total, max - total), ct)) > 0)
			total += n;
		return System.Text.Encoding.UTF8.GetString(buf, 0, total);
	}
}
