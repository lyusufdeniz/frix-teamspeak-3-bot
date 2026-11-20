// Frix: istatistikler, çevrimiçi süreler (aktiflik rütbeleri) ve çalma listeleri.

using System;
using System.Collections.Generic;
using System.Linq;

namespace TS3AudioBot.Frix;

public sealed record PlaylistInfo(long Id, string Name, long Count, string? CreatedBy);
public sealed record PlaylistEntry(string Type, string ResourceId, string? Title);

public static class FrixData
{
	public static void EnsureTables()
	{
		using var c = FrixDb.Open();
		FrixDb.Exec(c, @"
CREATE TABLE IF NOT EXISTS play_stats (
	id INTEGER PRIMARY KEY AUTOINCREMENT, time TEXT NOT NULL, bot TEXT, uid TEXT, user TEXT,
	type TEXT, resource_id TEXT, title TEXT);
CREATE INDEX IF NOT EXISTS ix_play_stats_time ON play_stats(time);
CREATE TABLE IF NOT EXISTS online_time (
	uid TEXT PRIMARY KEY, name TEXT, seconds INTEGER NOT NULL DEFAULT 0, last_seen TEXT);
CREATE TABLE IF NOT EXISTS favorites (
	uid TEXT NOT NULL, type TEXT NOT NULL, resource_id TEXT NOT NULL, title TEXT, added TEXT NOT NULL,
	PRIMARY KEY(uid, type, resource_id));
CREATE TABLE IF NOT EXISTS server_activity (
	server TEXT NOT NULL, time TEXT NOT NULL, users INTEGER NOT NULL, PRIMARY KEY(server, time));
CREATE INDEX IF NOT EXISTS ix_activity_time ON server_activity(time);
CREATE TABLE IF NOT EXISTS playlists (
	id INTEGER PRIMARY KEY AUTOINCREMENT, name TEXT NOT NULL UNIQUE COLLATE NOCASE, created TEXT NOT NULL, created_by TEXT);
CREATE TABLE IF NOT EXISTS playlist_items (
	playlist_id INTEGER NOT NULL, pos INTEGER NOT NULL, type TEXT NOT NULL, resource_id TEXT NOT NULL, title TEXT,
	FOREIGN KEY(playlist_id) REFERENCES playlists(id) ON DELETE CASCADE);
");
	}

	// ---------------------------------------------------------------- çalma istatistikleri

	public static void RecordPlay(string? bot, string? uid, string? user, string type, string resourceId, string? title)
	{
		try
		{
			FrixDb.Exec("INSERT INTO play_stats(time,bot,uid,user,type,resource_id,title) VALUES($t,$b,$u,$n,$ty,$r,$ti)",
				("$t", DateTime.UtcNow.ToString("O")), ("$b", bot), ("$u", uid), ("$n", user), ("$ty", type), ("$r", resourceId), ("$ti", title));
		}
		catch { /* istatistik kritik değil */ }
	}

	private static string Since(int days) => (days <= 0 ? DateTime.MinValue : DateTime.UtcNow.AddDays(-days)).ToString("O");

	public static List<(string Title, string Type, string ResourceId, long Count)> TopSongs(int days, int limit, string? bot = null)
		=> FrixDb.Query(@"SELECT MAX(title) AS title, type, resource_id, COUNT(*) AS c FROM play_stats
			WHERE time >= $s AND ($b IS NULL OR bot = $b) AND type <> 'media'
			GROUP BY type, resource_id ORDER BY c DESC LIMIT $l", ("$s", Since(days)), ("$b", bot), ("$l", limit))
			.Select(r => ((r["title"] as string) ?? "?", (string)r["type"]!, (string)r["resource_id"]!, (long)r["c"]!)).ToList();

	public static List<(string User, long Count)> TopRequesters(int days, int limit, string? bot = null)
		=> FrixDb.Query(@"SELECT MAX(user) AS user, COUNT(*) AS c FROM play_stats
			WHERE time >= $s AND ($b IS NULL OR bot = $b) AND user IS NOT NULL AND user <> ''
			GROUP BY uid ORDER BY c DESC LIMIT $l", ("$s", Since(days)), ("$b", bot), ("$l", limit))
			.Select(r => ((string)r["user"]!, (long)r["c"]!)).ToList();

	public static long PlayCount(int days, string? bot = null)
		=> (long)(FrixDb.Scalar("SELECT COUNT(*) FROM play_stats WHERE time >= $s AND ($b IS NULL OR bot = $b)", ("$s", Since(days)), ("$b", bot)) ?? 0L);

	public static long PlayCountSince(DateTime fromUtc)
		=> (long)(FrixDb.Scalar("SELECT COUNT(*) FROM play_stats WHERE time >= $s", ("$s", fromUtc.ToString("O"))) ?? 0L);

	public static long PlayCountBetween(DateTime fromUtc, DateTime toUtc)
		=> (long)(FrixDb.Scalar("SELECT COUNT(*) FROM play_stats WHERE time >= $s AND time < $e", ("$s", fromUtc.ToString("O")), ("$e", toUtc.ToString("O"))) ?? 0L);

	/// <summary>Son N günün (UTC) günlük çalma sayıları; boş günler 0.</summary>
	public static List<(string Day, long Count)> PlaysPerDay(int days)
	{
		var from = DateTime.UtcNow.Date.AddDays(-(days - 1));
		var rows = FrixDb.Query("SELECT substr(time,1,10) AS d, COUNT(*) AS c FROM play_stats WHERE time >= $s GROUP BY d", ("$s", from.ToString("O")))
			.ToDictionary(r => (string)r["d"]!, r => (long)r["c"]!);
		return Enumerable.Range(0, days).Select(i => from.AddDays(i).ToString("yyyy-MM-dd")).Select(d => (d, rows.GetValueOrDefault(d))).ToList();
	}

	// ---------------------------------------------------------------- favoriler

	public sealed record Favorite(string Type, string ResourceId, string? Title);

	public static List<Favorite> Favorites(string uid)
		=> FrixDb.Query("SELECT type, resource_id, title FROM favorites WHERE uid=$u ORDER BY added", ("$u", uid))
			.Select(r => new Favorite((string)r["type"]!, (string)r["resource_id"]!, r["title"] as string)).ToList();

	/// <summary>true = eklendi, false = zaten vardı. Sınır aşılırsa InvalidOperationException.</summary>
	public static bool AddFavorite(string uid, string type, string id, string? title, int max)
	{
		if (FrixDb.Scalar("SELECT 1 FROM favorites WHERE uid=$u AND type=$t AND resource_id=$r", ("$u", uid), ("$t", type), ("$r", id)) != null)
			return false;
		if ((long)(FrixDb.Scalar("SELECT COUNT(*) FROM favorites WHERE uid=$u", ("$u", uid)) ?? 0L) >= max)
			throw new InvalidOperationException("limit");
		FrixDb.Exec("INSERT INTO favorites(uid,type,resource_id,title,added) VALUES($u,$t,$r,$ti,$a)",
			("$u", uid), ("$t", type), ("$r", id), ("$ti", title), ("$a", DateTime.UtcNow.ToString("O")));
		return true;
	}

	public static Favorite? RemoveFavorite(string uid, int index)
	{
		var all = Favorites(uid);
		if (index < 1 || index > all.Count) return null;
		var f = all[index - 1];
		FrixDb.Exec("DELETE FROM favorites WHERE uid=$u AND type=$t AND resource_id=$r", ("$u", uid), ("$t", f.Type), ("$r", f.ResourceId));
		return f;
	}

	// ---------------------------------------------------------------- sunucu aktivitesi

	public static void RecordActivity(string server, DateTime bucketUtc, int users, int keepDays)
	{
		try
		{
			FrixDb.Exec("INSERT INTO server_activity(server,time,users) VALUES($s,$t,$u) ON CONFLICT(server,time) DO UPDATE SET users=MAX(users, excluded.users)",
				("$s", server), ("$t", bucketUtc.ToString("O")), ("$u", users));
			if (bucketUtc.Minute == 0)
				FrixDb.Exec("DELETE FROM server_activity WHERE time < $c", ("$c", DateTime.UtcNow.AddDays(-keepDays).ToString("O")));
		}
		catch { /* kritik değil */ }
	}

	/// <summary>Son 24 saat (saatlik en yüksek) ve son 28 günün gün×saat ortalaması (yerel saat). Sunucular toplanır.</summary>
	public static (List<(DateTime Hour, long Users)> Last24, double[][] Heat, long Peak, DateTime? PeakAt, long? Now, bool Any) Activity()
	{
		var rows = FrixDb.Query("SELECT time, SUM(users) AS u FROM server_activity WHERE time >= $s GROUP BY time",
				("$s", DateTime.UtcNow.AddDays(-28).ToString("O")))
			.Select(r => (t: DateTime.Parse((string)r["time"]!, null, System.Globalization.DateTimeStyles.RoundtripKind).ToLocalTime(), u: (long)r["u"]!)).ToList();
		var nowH = DateTime.Now;
		nowH = new DateTime(nowH.Year, nowH.Month, nowH.Day, nowH.Hour, 0, 0);
		var last24 = Enumerable.Range(0, 24).Select(i => nowH.AddHours(i - 23))
			.Select(hr => (hr, rows.Where(r => r.t >= hr && r.t < hr.AddHours(1)).Select(r => r.u).DefaultIfEmpty(0).Max())).ToList();
		var heat = Enumerable.Range(0, 7).Select(_ => new double[24]).ToArray();
		foreach (var g in rows.GroupBy(r => ((int)r.t.DayOfWeek + 6) % 7 * 24 + r.t.Hour))
			heat[g.Key / 24][g.Key % 24] = Math.Round(g.Average(r => r.u), 1);
		if (rows.Count == 0) return (last24, heat, 0, null, null, false);
		var peak = rows.MaxBy(r => r.u);
		// "şu an": sadece son 10 dakikada kayıt varsa (kayıtlar 5 dakikalık); yoksa bilinmiyor
		var latest = rows.MaxBy(r => r.t);
		long? now = latest.t >= DateTime.Now.AddMinutes(-10) ? latest.u : null;
		return (last24, heat, peak.u, peak.t, now, true);
	}

	// ---------------------------------------------------------------- çevrimiçi süre

	public static void AddOnline(IEnumerable<(string uid, string name)> clients, int seconds)
	{
		using var c = FrixDb.Open();
		FrixDb.Exec(c, "BEGIN");
		var now = DateTime.UtcNow.ToString("O");
		foreach (var (uid, name) in clients)
		{
			FrixDb.Exec(c, @"INSERT INTO online_time(uid,name,seconds,last_seen) VALUES($u,$n,$s,$t)
				ON CONFLICT(uid) DO UPDATE SET seconds = seconds + $s, name = excluded.name, last_seen = excluded.last_seen",
				("$u", uid), ("$n", name), ("$s", seconds), ("$t", now));
		}
		FrixDb.Exec(c, "COMMIT");
	}

	/// <summary>Görülen kişilerin "son görülme" zamanını günceller (süreye dokunmaz).</summary>
	public static void TouchSeen(IEnumerable<(string uid, string name)> clients)
	{
		using var c = FrixDb.Open();
		FrixDb.Exec(c, "BEGIN");
		var now = DateTime.UtcNow.ToString("O");
		foreach (var (uid, name) in clients)
			FrixDb.Exec(c, @"INSERT INTO online_time(uid,name,seconds,last_seen) VALUES($u,$n,0,$t)
				ON CONFLICT(uid) DO UPDATE SET name = excluded.name, last_seen = excluded.last_seen",
				("$u", uid), ("$n", name), ("$t", now));
		FrixDb.Exec(c, "COMMIT");
	}

	/// <summary>Ada göre en son görülme (tam eşleşme önce, sonra içeren).</summary>
	public static (string Name, DateTime LastSeen)? LastSeen(string name)
	{
		var row = FrixDb.Query("SELECT name, last_seen FROM online_time WHERE last_seen IS NOT NULL AND name = $n COLLATE NOCASE ORDER BY last_seen DESC LIMIT 1", ("$n", name)).FirstOrDefault()
			?? FrixDb.Query("SELECT name, last_seen FROM online_time WHERE last_seen IS NOT NULL AND name LIKE $n ESCAPE '\\' ORDER BY last_seen DESC LIMIT 1",
				("$n", "%" + name.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%")).FirstOrDefault();
		if (row is null || row["last_seen"] is not string ls) return null;
		return ((string)row["name"]!, DateTime.Parse(ls, null, System.Globalization.DateTimeStyles.RoundtripKind));
	}

	public static long OnlineSeconds(string uid) => (long)(FrixDb.Scalar("SELECT seconds FROM online_time WHERE uid=$u", ("$u", uid)) ?? 0L);

	public static List<(string Uid, string Name, long Seconds, string? LastSeen)> OnlineTop(int limit)
		=> FrixDb.Query("SELECT uid, name, seconds, last_seen FROM online_time ORDER BY seconds DESC LIMIT $l", ("$l", limit))
			.Select(r => ((string)r["uid"]!, (r["name"] as string) ?? "?", (long)r["seconds"]!, r["last_seen"] as string)).ToList();

	public static long OnlineRank(string uid)
		=> (long)(FrixDb.Scalar("SELECT COUNT(*) + 1 FROM online_time WHERE seconds > (SELECT seconds FROM online_time WHERE uid=$u)", ("$u", uid)) ?? 0L);

	public static string FormatDuration(long seconds)
	{
		var t = TimeSpan.FromSeconds(seconds);
		return t.TotalHours >= 1 ? $"{(int)t.TotalHours} sa {t.Minutes} dk" : $"{t.Minutes} dk";
	}

	// ---------------------------------------------------------------- çalma listeleri

	public static string NormalizeListName(string name)
	{
		name = name.Trim();
		if (name.Length is < 1 or > 40 || name.Contains('|'))
			throw new ArgumentException("Liste adı 1-40 karakter olmalı.");
		return name;
	}

	public static List<PlaylistInfo> Playlists()
		=> FrixDb.Query(@"SELECT p.id, p.name, p.created_by, (SELECT COUNT(*) FROM playlist_items i WHERE i.playlist_id = p.id) AS c
			FROM playlists p ORDER BY p.name COLLATE NOCASE")
			.Select(r => new PlaylistInfo((long)r["id"]!, (string)r["name"]!, (long)r["c"]!, r["created_by"] as string)).ToList();

	public static PlaylistInfo? FindPlaylist(string name)
		=> Playlists().FirstOrDefault(p => p.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase));

	public static List<PlaylistEntry> PlaylistItems(long id)
		=> FrixDb.Query("SELECT type, resource_id, title FROM playlist_items WHERE playlist_id=$i ORDER BY pos", ("$i", id))
			.Select(r => new PlaylistEntry((string)r["type"]!, (string)r["resource_id"]!, r["title"] as string)).ToList();

	/// <summary>Listeyi verilen şarkılarla oluşturur ya da üzerine yazar.</summary>
	public static long SavePlaylist(string name, IEnumerable<PlaylistEntry> items, string? by, bool append = false)
	{
		name = NormalizeListName(name);
		using var c = FrixDb.Open();
		FrixDb.Exec(c, "BEGIN");
		FrixDb.Exec(c, "INSERT OR IGNORE INTO playlists(name,created,created_by) VALUES($n,$t,$b)",
			("$n", name), ("$t", DateTime.UtcNow.ToString("O")), ("$b", by));
		using var cmd = c.CreateCommand();
		cmd.CommandText = "SELECT id FROM playlists WHERE name=$n";
		cmd.Parameters.AddWithValue("$n", name);
		var id = (long)cmd.ExecuteScalar()!;
		var start = 0L;
		if (append)
		{
			using var q = c.CreateCommand();
			q.CommandText = "SELECT COALESCE(MAX(pos)+1,0) FROM playlist_items WHERE playlist_id=$i";
			q.Parameters.AddWithValue("$i", id);
			start = (long)q.ExecuteScalar()!;
		}
		else
		{
			FrixDb.Exec(c, "DELETE FROM playlist_items WHERE playlist_id=$i", ("$i", id));
		}
		var pos = start;
		foreach (var it in items.Take(500))
			FrixDb.Exec(c, "INSERT INTO playlist_items(playlist_id,pos,type,resource_id,title) VALUES($i,$p,$ty,$r,$ti)",
				("$i", id), ("$p", pos++), ("$ty", it.Type), ("$r", it.ResourceId), ("$ti", it.Title));
		FrixDb.Exec(c, "COMMIT");
		return id;
	}

	public static void DeletePlaylist(long id) => FrixDb.Exec("DELETE FROM playlists WHERE id=$i", ("$i", id));

	public static void RemovePlaylistItem(long id, int index)
	{
		var items = PlaylistItems(id);
		if (index < 0 || index >= items.Count) throw new ArgumentException("Geçersiz sıra.");
		items.RemoveAt(index);
		var name = (string)FrixDb.Scalar("SELECT name FROM playlists WHERE id=$i", ("$i", id))!;
		SavePlaylist(name, items, null);
	}
}
