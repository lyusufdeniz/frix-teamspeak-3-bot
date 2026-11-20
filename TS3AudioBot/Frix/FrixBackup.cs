// Frix: panelden yedek alma / geri yükleme.
// Yedek tek bir JSON dosyasıdır: seçilen bölümlerin tablo satırları ve yüklenen avatar resimleri.
// Kullanıcılar, oturumlar ve işlem kaydı yedeğe girmez (şifre özetleri dosyada dolaşmasın).

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;

namespace TS3AudioBot.Frix;

public static class FrixBackup
{
	private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();
	public const int FormatVersion = 1;

	/// <summary>Bölüm → içerdiği tablolar. "avatars" dosya bölümüdür.</summary>
	public static readonly (string Key, string[] Tables)[] Sections =
	[
		("plugins", ["plugins"]),
		("texts", ["texts"]),
		("commands", ["commands", "builtin_names"]),
		("radios", ["radios"]),
		("playlists", ["playlists", "playlist_items", "favorites"]),
		("settings", ["meta"]),
		("stats", ["play_stats", "online_time", "server_activity"]),
		("avatars", []),
	];

	private static string AvatarDir => Path.Combine(AppContext.BaseDirectory, "avatars");

	public static JsonObject Export(IEnumerable<string> sections)
	{
		var want = sections.ToHashSet(StringComparer.OrdinalIgnoreCase);
		var data = new JsonObject();
		foreach (var (key, tables) in Sections.Where(s => want.Contains(s.Key)))
		{
			var sec = new JsonObject();
			if (key == "avatars")
			{
				if (Directory.Exists(AvatarDir))
					foreach (var f in Directory.EnumerateFiles(AvatarDir, "*.png"))
						sec[Path.GetFileName(f)] = Convert.ToBase64String(File.ReadAllBytes(f));
			}
			else
			{
				foreach (var t in tables)
				{
					var rows = new JsonArray();
					foreach (var r in FrixDb.Query($"SELECT * FROM {t}"))
					{
						var o = new JsonObject();
						foreach (var (k, v) in r)
							o[k] = v switch { null => null, long l => l, double d => d, byte[] b => Convert.ToBase64String(b), _ => v.ToString() };
						rows.Add(o);
					}
					sec[t] = rows;
				}
			}
			data[key] = sec;
		}
		return new JsonObject
		{
			["frix"] = "backup",
			["format"] = FormatVersion,
			["version"] = FrixUpdater.CurrentVersion,
			["created"] = DateTime.UtcNow.ToString("O"),
			["sections"] = data,
		};
	}

	/// <summary>Yedek dosyasında hangi bölümler var, kaçar kayıt.</summary>
	public static object Inspect(JsonObject backup)
	{
		Validate(backup);
		var secs = (JsonObject)backup["sections"]!;
		return new
		{
			version = backup["version"]?.ToString(),
			created = backup["created"]?.ToString(),
			sections = Sections.Where(s => secs.ContainsKey(s.Key)).Select(s => new
			{
				key = s.Key,
				count = secs[s.Key] is JsonObject o ? (s.Key == "avatars" ? o.Count : o.Sum(kv => kv.Value is JsonArray a ? a.Count : 0)) : 0,
			}),
		};
	}

	private static void Validate(JsonObject backup)
	{
		if (backup["frix"]?.ToString() != "backup" || backup["sections"] is not JsonObject)
			throw new InvalidDataException("not_a_backup");
		if (backup["format"] is JsonValue fv && fv.TryGetValue<int>(out var f) && f > FormatVersion)
			throw new InvalidDataException("backup_too_new");
	}

	/// <summary>Seçilen bölümleri yedektekiyle değiştirir (bölüm içindeki eski kayıtlar silinir).</summary>
	public static void Import(JsonObject backup, IEnumerable<string> sections)
	{
		Validate(backup);
		var secs = (JsonObject)backup["sections"]!;
		var want = sections.ToHashSet(StringComparer.OrdinalIgnoreCase);
		using (var c = FrixDb.Open())
		{
			FrixDb.Exec(c, "BEGIN");
			try
			{
				foreach (var (key, tables) in Sections.Where(s => want.Contains(s.Key) && secs.ContainsKey(s.Key) && s.Tables.Length > 0))
				{
					var sec = secs[key] as JsonObject ?? [];
					foreach (var t in tables)
					{
						if (sec[t] is not JsonArray rows) continue;
						var cols = FrixDb.Query($"PRAGMA table_info({t})").Select(r => (string)r["name"]!).ToHashSet(StringComparer.OrdinalIgnoreCase);
						FrixDb.Exec(c, $"DELETE FROM {t}");
						foreach (var row in rows.OfType<JsonObject>())
						{
							var kv = row.Where(p => cols.Contains(p.Key)).ToList();
							if (kv.Count == 0) continue;
							var sql = $"INSERT OR REPLACE INTO {t}({string.Join(',', kv.Select(p => p.Key))}) VALUES({string.Join(',', kv.Select((_, i) => "$p" + i))})";
							FrixDb.Exec(c, sql, kv.Select((p, i) => ("$p" + i, ToDb(p.Value))).ToArray());
						}
					}
				}
				FrixDb.Exec(c, "COMMIT");
			}
			catch
			{
				FrixDb.Exec(c, "ROLLBACK");
				throw;
			}
		}

		if (want.Contains("avatars") && secs["avatars"] is JsonObject av)
		{
			Directory.CreateDirectory(AvatarDir);
			foreach (var (name, v) in av)
			{
				var file = Path.GetFileName(name);
				if (!file.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || v is null) continue;
				try { File.WriteAllBytes(Path.Combine(AvatarDir, file), Convert.FromBase64String(v.ToString())); }
				catch (Exception ex) { Log.Warn(ex, "Avatar resmi geri yüklenemedi: {0}", file); }
			}
		}

		// önbellekleri yenile
		FrixText.Load();
		FrixCommands.Load();
		FrixPluginHost.ReloadAll();
		FrixAvatar.Invalidate();
		Log.Info("Yedek geri yüklendi: {0}", string.Join(", ", want));
	}

	private static object? ToDb(JsonNode? v) => v switch
	{
		null => null,
		JsonValue jv when jv.TryGetValue<long>(out var l) => l,
		JsonValue jv when jv.TryGetValue<double>(out var d) => d,
		JsonValue jv when jv.TryGetValue<bool>(out var b) => b ? 1L : 0L,
		_ => v.ToString(),
	};
}
