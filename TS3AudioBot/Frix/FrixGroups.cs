// Frix: benzer eklentiler panelde tek eklenti (grup) olarak görünür.
// Eski eklenti sınıfları aynen çalışmaya devam eder ("parça"); grup sadece ayarlarını tek satırda tutar:
//   - grup satırı: enabled = ana anahtar, config = { "<parça>": true/false, "<parça>.<ayar>": değer, ... }
//   - parça açık = ana anahtar açık VE parça anahtarı açık
// Tek parçalı gruplarda parça anahtarı yoktur, ana anahtar yeterlidir.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace TS3AudioBot.Frix;

public sealed record PluginGroup(string Id, string NameTr, string NameEn, string DescTr, string DescEn, string[] Parts);

public static class FrixGroups
{
	private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();

	public static readonly PluginGroup[] All =
	[
		new("g_music", "Müzik", "Music",
			"Şarkı çalma ve müzik özellikleri: komutlar, ses eşitleme, otomatik devam, favoriler, oylamayla geçme, DJ modu, yalnızken duraklatma.",
			"Playing songs and music features: commands, volume levelling, autoplay, favourites, vote skip, DJ mode, pause when alone.",
			["music", "normalize", "autoplay", "favorites", "voteskip", "djmode", "alonepause"]),
		new("g_radio", "Radyo", "Radio",
			"Panelde eklenen radyolar (!radyo) ve kuyruk boşken otomatik radyo.",
			"Radios added in the panel (!radio) and a radio when the queue is idle.",
			["radio", "idleradio"]),
		new("g_look", "Bot görünümü", "Bot appearance",
			"Botun avatarı ve çalan şarkıyı ad / açıklama olarak gösterme.",
			"The bot's avatar and showing the playing song as its name / description.",
			["avatar", "npstatus"]),
		new("g_reconnect", "Otomatik yeniden bağlan", "Auto reconnect",
			"Bot sunucudan düşünce kendiliğinden tekrar bağlanır ve aynı kanala girer.",
			"When the bot drops it reconnects by itself and rejoins the same channel.",
			["autoreconnect"]),
		new("g_welcome", "Karşılama ve duyurular", "Welcome & announcements",
			"Gelenlere karşılama mesajı, dönen duyuru/reklam mesajları ve zamanlı duyurular.",
			"Welcome messages, rotating announcements/ads and scheduled messages.",
			["welcome", "advertisement"]),
		new("g_schedule", "Zamanlanmış çalma", "Scheduled playback",
			"Belirli gün ve saatlerde radyo / çalma listesi başlatır, durdurur veya sesi ayarlar.",
			"Starts a radio or playlist, stops or sets the volume at set days and times.",
			["scheduleplay"]),
		new("g_moderation", "Moderasyon", "Moderation",
			"AFK taşıyıcı, takma ad filtresi, kanal koruma ve flood koruması.",
			"AFK mover, nickname filter, channel guard and anti-flood.",
			["afkmover", "namefilter", "channelguard", "antiflood"]),
		new("g_server", "Sunucu araçları", "Server tools",
			"Kayıt botu, geçici kanal oluşturucu, kanal adında bilgi ve destek bildirimi.",
			"Registration, temporary channel creator, channel name info and support notifier.",
			["registration", "tempchannel", "channelinfo", "supportnotify"]),
		new("g_activity", "Aktivite", "Activity",
			"Çevrimiçi kişi sayısını kaydeder (dashboard grafiği) ve sunucuda geçirilen süreye göre rütbe verir.",
			"Records who is online (dashboard chart) and gives ranks by time spent on the server.",
			["activity", "ranks"]),
		new("g_fun", "Eğlence ve araçlar", "Fun & tools",
			"!zar, !yazitura, !sec, !anket, !hava, !doviz ve !soz komutları.",
			"!zar, !yazitura, !sec, !anket, !hava, !doviz and !soz commands.",
			["fun"]),
	];

	private static readonly Dictionary<string, FrixPlugin> Protos =
		FrixPluginHost.Factories.Select(f => f()).ToDictionary(p => p.Id);

	public static PluginGroup? Get(string id) => All.FirstOrDefault(g => g.Id == id);
	public static PluginGroup? GroupOf(string partId) => All.FirstOrDefault(g => g.Parts.Contains(partId));
	public static FrixPlugin Proto(string partId) => Protos[partId];

	/// <summary>Grup için hiç kayıt yokken ana anahtar: parçalardan biri varsayılan açıksa açık.</summary>
	public static bool DefaultOn(PluginGroup g) => g.Parts.Any(p => Protos[p].DefaultEnabled);

	/// <summary>Grup satırından bir parçanın durumu ve kendi ayarları.</summary>
	public static (bool on, JsonObject cfg) PartState(PluginGroup g, string partId, bool rowExists, bool master, JsonObject gcfg)
	{
		var proto = Protos[partId];
		if (!rowExists) return (proto.DefaultEnabled, []);
		var part = g.Parts.Length == 1 || (gcfg.TryGetPropertyValue(partId, out var v) && v is JsonValue jv && jv.TryGetValue<bool>(out var b) ? b : proto.DefaultEnabled);
		var cfg = new JsonObject();
		var prefix = partId + ".";
		foreach (var (k, val) in gcfg)
			if (k.StartsWith(prefix, StringComparison.Ordinal))
				cfg[k[prefix.Length..]] = val?.DeepClone();
		return (master && part, cfg);
	}

	/// <summary>Panelde gösterilen ayarlar: her parça için bir bölüm başlığı (anahtar) ve önekli alanlar.</summary>
	public static PluginField[] Schema(PluginGroup g)
	{
		var list = new List<PluginField>();
		foreach (var id in g.Parts)
		{
			var p = Protos[id];
			if (g.Parts.Length > 1)
				list.Add(new PluginField(id, "section", p.NameTr, p.NameEn, p.DefaultEnabled, null, p.DescTr, p.DescEn));
			list.AddRange(p.Schema.Select(f => f with { Key = id + "." + f.Key }));
		}
		return [.. list];
	}

	private static int migrated;

	/// <summary>Eski (parça başına) eklenti satırlarını grup satırlarına taşır. Bir kez çalışır.</summary>
	public static void Migrate()
	{
		if (System.Threading.Interlocked.Exchange(ref migrated, 1) == 1) return;
		try
		{
			var rows = FrixDb.Query("SELECT bot, id, enabled, config FROM plugins")
				.Select(r => (bot: (string)r["bot"]!, id: (string)r["id"]!, on: (long)r["enabled"]! != 0, cfg: JsonNode.Parse((r["config"] as string) ?? "{}") as JsonObject ?? []))
				.ToList();
			foreach (var botRows in rows.Where(r => !r.id.StartsWith("g_", StringComparison.Ordinal)).GroupBy(r => r.bot))
			{
				var bot = botRows.Key;
				var old = botRows.ToDictionary(r => r.id);
				foreach (var g in All)
				{
					if (!g.Parts.Any(old.ContainsKey) || rows.Any(r => r.bot == bot && r.id == g.Id)) continue;
					var gcfg = new JsonObject();
					var anyOn = false;
					foreach (var part in g.Parts)
					{
						var has = old.TryGetValue(part, out var r);
						var on = has ? r.on : Protos[part].DefaultEnabled;
						anyOn |= on;
						if (g.Parts.Length > 1) gcfg[part] = on;
						if (has)
							foreach (var (k, v) in r.cfg)
								gcfg[part + "." + k] = v?.DeepClone();
					}
					FrixDb.Exec("INSERT INTO plugins(bot,id,enabled,config) VALUES($b,$i,$e,$c)",
						("$b", bot), ("$i", g.Id), ("$e", anyOn ? 1 : 0), ("$c", gcfg.ToJsonString()));
				}
				foreach (var id in old.Keys)
					FrixDb.Exec("DELETE FROM plugins WHERE bot=$b AND id=$i", ("$b", bot), ("$i", id));
				Log.Info("Eklenti ayarları yeni gruplara taşındı: {0}", bot);
			}
		}
		catch (Exception ex) { Log.Warn(ex, "Eklenti ayarları gruplara taşınamadı"); }
	}

	/// <summary>Bir parçanın tek ayarını grup satırına yazar (örn. adla bulunan kanalın ID'si).</summary>
	public static void PersistPart(string bot, string partId, string key, string value)
	{
		var g = GroupOf(partId);
		if (g is null) return;
		var row = FrixDb.Query("SELECT enabled, config FROM plugins WHERE bot=$b AND id=$i", ("$b", bot), ("$i", g.Id)).FirstOrDefault();
		var cfg = row is null ? new JsonObject() : JsonNode.Parse((row["config"] as string) ?? "{}") as JsonObject ?? [];
		cfg[partId + "." + key] = value;
		FrixDb.Exec("INSERT INTO plugins(bot,id,enabled,config) VALUES($b,$i,$e,$c) ON CONFLICT(bot,id) DO UPDATE SET config=excluded.config",
			("$b", bot), ("$i", g.Id), ("$e", row is null ? (DefaultOn(g) ? 1 : 0) : (long)row["enabled"]!), ("$c", cfg.ToJsonString()));
	}
}
