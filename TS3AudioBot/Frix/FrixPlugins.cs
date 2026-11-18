// Frix eklentileri: her bot için ayrı açılıp kapatılabilir, panelden ayarlanır.
// Ayarlar SQLite "plugins" tablosunda JSON olarak tutulur.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using TS3AudioBot.Audio;
using TS3AudioBot.Playlists;
using TS3AudioBot.ResourceFactories;
using TSLib;
using TSLib.Full;

namespace TS3AudioBot.Frix;

public sealed record PluginField(string Key, string Type, string LabelTr, string LabelEn, object? Default, string[]? Options = null, string? HelpTr = null, string? HelpEn = null);

public abstract class FrixPlugin
{
	public abstract string Id { get; }
	public abstract string NameTr { get; }
	public abstract string NameEn { get; }
	public abstract string DescTr { get; }
	public abstract string DescEn { get; }
	public abstract PluginField[] Schema { get; }

	public bool Enabled { get; private set; }
	/// <summary>Panelde hiç ayarlanmamışsa açık mı başlasın?</summary>
	public virtual bool DefaultEnabled => false;
	protected JsonObject Config { get; private set; } = [];
	protected PluginContext Ctx { get; private set; } = null!;

	internal void Bind(PluginContext ctx) => Ctx = ctx;
	/// <summary>Ayarları olayları tetiklemeden yükler (sadece okuma için).</summary>
	internal void Load(JsonObject config) => Config = config;

	internal void Apply(bool enabled, JsonObject config)
	{
		Config = config;
		var was = Enabled;
		Enabled = enabled;
		if (enabled && !was) OnEnable();
		else if (!enabled && was) OnDisable();
		else if (enabled) OnConfigChanged();
	}

	protected virtual void OnEnable() { }
	protected virtual void OnDisable() { }
	protected virtual void OnConfigChanged() { }

	public virtual Task OnClientConnected(ClientId id, string name, Uid uid) => Task.CompletedTask;
	public virtual Task OnClientJoinedBotChannel(ClientId id, string name, Uid uid) => Task.CompletedTask;
	public virtual Task OnClientEnteredChannel(ClientId id, string name, Uid uid, ChannelId channel) => Task.CompletedTask;
	public virtual Task OnTick(DateTime now) => Task.CompletedTask;
	public virtual Task OnTextMessage(ClientId id, Uid uid, string name, string message) => Task.CompletedTask;
	public virtual Task OnSongStarted() => Task.CompletedTask;

	/// <summary>Sunucu grubu listesinde (virgüllü ID'ler) mi?</summary>
	protected bool InGroups(ClientId id, string key)
	{
		var groups = PluginContext.ParseIds(S(key));
		return groups.Count > 0 && Ctx.Full.Book.Clients.TryGetValue(id, out var c) && c.ServerGroups.Any(g => groups.Contains(g.Value));
	}
	public virtual Task OnAloneChanged(bool alone) => Task.CompletedTask;
	public virtual Task OnPlaybackStopped() => Task.CompletedTask;

	/// <summary>Ayar anahtarının (bool) değeri; komut/eklenti bağımlılığı kontrolü için.</summary>
	public bool Flag(string key) => Schema.Any(f => f.Key == key && f.Type == "bool") ? B(key) : true;

	protected string S(string key) => Config.TryGetPropertyValue(key, out var v) && v != null ? v.ToString() : Schema.First(f => f.Key == key).Default?.ToString() ?? "";
	protected int I(string key) => int.TryParse(S(key), out var i) ? i : Convert.ToInt32(Schema.First(f => f.Key == key).Default ?? 0);
	protected bool B(string key) => Config.TryGetPropertyValue(key, out var v) && v is JsonValue jv && jv.TryGetValue<bool>(out var b) ? b : (bool)(Schema.First(f => f.Key == key).Default ?? false);

	public static string Fill(string template, params (string k, string v)[] values)
	{
		foreach (var (k, v) in values)
			template = template.Replace("{" + k + "}", v);
		return template;
	}
}

/// <summary>Eklentilerin bota erişimi.</summary>
public sealed class PluginContext
{
	public required string BotName { get; init; }
	public required Ts3Client Client { get; init; }
	public required TsFullClient Full { get; init; }
	public required PlayManager PlayManager { get; init; }
	public required Player Player { get; init; }
	public required PlaylistManager Playlist { get; init; }
	public required Func<Func<Task>, Task> RunOnBot { get; init; }
	/// <summary>Botun ayarlardaki asıl adı (şarkı adı eklentisi geri dönerken kullanır).</summary>
	public required Func<string> BaseNickname { get; init; }
	/// <summary>Avatarı mevcut duruma göre yeniden ayarlar.</summary>
	public required Func<Task> RefreshAvatar { get; init; }

	public string ServerName => Full.Book.Server?.Name ?? "TeamSpeak";

	/// <summary>"/5", "5" ya da kanal adı ile kanal bulur.</summary>
	public ChannelId? FindChannel(string? spec)
	{
		spec = spec?.Trim().TrimStart('/');
		if (string.IsNullOrEmpty(spec)) return null;
		if (ulong.TryParse(spec, out var id) && Full.Book.Channels.ContainsKey((ChannelId)id)) return (ChannelId)id;
		foreach (var ch in Full.Book.Channels.Values)
			if (string.Equals(ch.Name, spec, StringComparison.OrdinalIgnoreCase)) return ch.Id;
		return null;
	}

	public IEnumerable<TSLib.Full.Book.Client> RealClients()
		=> Full.Book.Clients.Values.Where(c => c.ClientType == ClientType.Full && c.Id != Full.ClientId).ToArray();

	public static HashSet<ulong> ParseIds(string text)
		=> text.Split([',', ' ', ';', '\n'], StringSplitOptions.RemoveEmptyEntries)
			.Select(t => ulong.TryParse(t.Trim(), out var v) ? v : 0).Where(v => v > 0).ToHashSet();
	public string ChannelName => Full.Book.OwnChannel?.Name ?? "";
	public bool Connected => Full.Connected;
}

public sealed class FrixPluginHost
{
	private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();
	private static readonly ConcurrentDictionary<string, FrixPluginHost> Hosts = new();

	public static Func<FrixPlugin>[] Factories { get; } =
	[
		() => new MusicPlugin(),
		() => new RadioPlugin(),
		() => new NowPlayingStatusPlugin(),
		() => new AvatarPlugin(),
		() => new AutoReconnectPlugin(),
		() => new NormalizePlugin(),
		() => new AutoplayPlugin(),
		() => new FavoritesPlugin(),
		() => new ScheduledPlayPlugin(),
		() => new ActivityPlugin(),
		() => new WelcomePlugin(),
		() => new AdvertisementPlugin(),
		() => new AlonePausePlugin(),
		() => new IdleRadioPlugin(),
		() => new AfkMoverPlugin(),
		() => new NameFilterPlugin(),
		() => new SupportNotifyPlugin(),
		() => new ChannelInfoPlugin(),
		() => new RegistrationPlugin(),
		() => new TempChannelPlugin(),
		() => new RanksPlugin(),
		() => new ChannelGuardPlugin(),
		() => new AntiFloodPlugin(),
		() => new VoteSkipPlugin(),
		() => new DjModePlugin(),
		() => new FunPlugin(),
	];

	public static IEnumerable<FrixPlugin> Catalog => Factories.Select(f => f());

	public PluginContext Ctx { get; }
	public IReadOnlyList<FrixPlugin> Plugins { get; }
	private DateTime connectedAt = DateTime.MaxValue;

	private static int migrated;

	/// <summary>Eski "scheduled" eklentisinin ayarlarını "Duyurular" eklentisine taşır (bir kez).</summary>
	internal static void MigrateScheduled()
	{
		if (System.Threading.Interlocked.Exchange(ref migrated, 1) == 1) return;
		try
		{
			foreach (var row in FrixDb.Query("SELECT bot, enabled, config FROM plugins WHERE id='scheduled'"))
			{
				var bot = (string)row["bot"]!;
				var sched = JsonNode.Parse((row["config"] as string) ?? "{}") as JsonObject ?? [];
				var schedOn = (long)row["enabled"]! != 0;
				var adv = FrixDb.Query("SELECT enabled, config FROM plugins WHERE bot=$b AND id='advertisement'", ("$b", bot)).FirstOrDefault();
				var advCfg = adv is null ? [] : JsonNode.Parse((adv["config"] as string) ?? "{}") as JsonObject ?? [];
				var advOn = adv is not null && (long)adv["enabled"]! != 0;
				if (sched.TryGetPropertyValue("entries", out var e) && e is not null) advCfg["schedule"] = e.ToString();
				if (!advOn && schedOn) { advCfg["messages"] = ""; if (sched.TryGetPropertyValue("target", out var tg) && tg is not null) advCfg["target"] = tg.ToString(); }
				FrixDb.Exec("INSERT INTO plugins(bot,id,enabled,config) VALUES($b,'advertisement',$e,$c) ON CONFLICT(bot,id) DO UPDATE SET enabled=excluded.enabled, config=excluded.config",
					("$b", bot), ("$e", advOn || schedOn ? 1 : 0), ("$c", advCfg.ToJsonString()));
				FrixDb.Exec("DELETE FROM plugins WHERE bot=$b AND id='scheduled'", ("$b", bot));
			}
		}
		catch (Exception ex) { Log.Warn(ex, "Zamanlı duyuru ayarları taşınamadı"); }
	}

	public FrixPluginHost(PluginContext ctx)
	{
		MigrateScheduled();
		Ctx = ctx;
		Plugins = Factories.Select(f => f()).ToList();
		foreach (var p in Plugins)
			p.Bind(ctx);
		Hosts[ctx.BotName] = this;
		Reload();
	}

	public static FrixPluginHost? For(string bot) => Hosts.TryGetValue(bot, out var h) ? h : null;
	public static void Remove(string bot, FrixPluginHost host) => Hosts.TryRemove(new KeyValuePair<string, FrixPluginHost>(bot, host));

	public void Reload()
	{
		var rows = FrixDb.Query("SELECT id, enabled, config FROM plugins WHERE bot=$b", ("$b", Ctx.BotName))
			.ToDictionary(r => (string)r["id"]!, r => r);
		foreach (var p in Plugins)
		{
			try
			{
				if (rows.TryGetValue(p.Id, out var row))
					p.Apply((long)row["enabled"]! != 0, JsonNode.Parse((row["config"] as string) ?? "{}") as JsonObject ?? []);
				else
					p.Apply(p.DefaultEnabled, []);
			}
			catch (Exception ex)
			{
				Log.Warn(ex, "Eklenti yüklenemedi: {0}", p.Id);
			}
		}
	}

	public static void Save(string bot, string id, bool enabled, JsonObject config)
	{
		FrixDb.Exec("INSERT INTO plugins(bot,id,enabled,config) VALUES($b,$i,$e,$c) ON CONFLICT(bot,id) DO UPDATE SET enabled=excluded.enabled, config=excluded.config",
			("$b", bot), ("$i", id), ("$e", enabled ? 1 : 0), ("$c", config.ToJsonString()));
		For(bot)?.Reload();
	}

	public void OnConnected() => connectedAt = DateTime.UtcNow;
	public void OnDisconnected() => connectedAt = DateTime.MaxValue;

	private async Task Each(Func<FrixPlugin, Task> action)
	{
		foreach (var p in Plugins)
		{
			if (!p.Enabled) continue;
			try { await action(p); }
			catch (Exception ex) { Log.Warn("Eklenti hatası ({0}): {1}", p.Id, ex.Message); }
		}
	}

	public Task ClientEntered(ClientId id, string name, Uid uid, ChannelId source, ChannelId target, int reason, bool isQuery)
	{
		// Bağlanırken mevcut kullanıcılar da "girdi" olarak gelir; ilk 15 saniyeyi yoksay
		if (isQuery || DateTime.UtcNow - connectedAt < TimeSpan.FromSeconds(15) || id == Ctx.Full.ClientId)
			return Task.CompletedTask;
		var tasks = new List<Task>();
		if (reason == 0 && source == ChannelId.Null)
			tasks.Add(Each(p => p.OnClientConnected(id, name, uid)));
		if (Ctx.Full.Book.OwnClient is { } me && target == me.Channel)
			tasks.Add(Each(p => p.OnClientJoinedBotChannel(id, name, uid)));
		tasks.Add(Each(p => p.OnClientEnteredChannel(id, name, uid, target)));
		return Task.WhenAll(tasks);
	}

	public Task ClientMoved(ClientId id, ChannelId target)
	{
		if (id == Ctx.Full.ClientId)
			return Task.CompletedTask;
		if (!Ctx.Full.Book.Clients.TryGetValue(id, out var c) || c.ClientType != ClientType.Full)
			return Task.CompletedTask;
		var uid = c.Uid ?? Uid.Null;
		var tasks = new List<Task> { Each(p => p.OnClientEnteredChannel(id, c.Name, uid, target)) };
		if (Ctx.Full.Book.OwnClient is { } me && target == me.Channel)
			tasks.Add(Each(p => p.OnClientJoinedBotChannel(id, c.Name, uid)));
		return Task.WhenAll(tasks);
	}

	public Task Tick() => Ctx.Connected ? Each(p => p.OnTick(DateTime.UtcNow)) : Task.CompletedTask;
	public Task TextMessage(ClientId id, Uid uid, string name, string message)
		=> id == Ctx.Full.ClientId ? Task.CompletedTask : Each(p => p.OnTextMessage(id, uid, name, message));
	public Task SongStarted() => Each(p => p.OnSongStarted());

	/// <summary>Komutların eklentiye erişmesi için: bu botta açık olan T eklentisi.</summary>
	public static T? Enabled<T>(string? bot) where T : FrixPlugin
		=> bot is null ? null : For(bot)?.Plugins.OfType<T>().FirstOrDefault(p => p.Enabled);
	/// <summary>
	/// "id" ya da "id:ayar" biçimindeki gereksinim bu botta açık mı? Bot çalışmıyorsa veritabanına bakar.
	/// </summary>
	public static bool Active(string bot, string requirement)
	{
		var (id, flag) = requirement.IndexOf(':') is var i and >= 0 ? (requirement[..i], requirement[(i + 1)..]) : (requirement, null);
		if (For(bot) is { } host)
		{
			var p = host.Plugins.FirstOrDefault(x => x.Id == id);
			return p is not null && p.Enabled && (flag is null || p.Flag(flag));
		}
		var proto = Factories.Select(f => f()).FirstOrDefault(x => x.Id == id);
		if (proto is null) return false;
		var row = FrixDb.Query("SELECT enabled, config FROM plugins WHERE bot=$b AND id=$i", ("$b", bot), ("$i", id)).FirstOrDefault();
		if (row is null) return proto.DefaultEnabled && (flag is null || proto.Flag(flag));
		if ((long)row["enabled"]! == 0) return false;
		if (flag is null) return true;

		proto.Load(JsonNode.Parse((row["config"] as string) ?? "{}") as JsonObject ?? []);
		return proto.Flag(flag);
	}

	/// <summary>Çalışan botlar.</summary>
	public static string[] RunningBots => [.. Hosts.Keys];

	public Task AloneChanged(bool alone) => Each(p => p.OnAloneChanged(alone));
	public Task PlaybackStopped() => Each(p => p.OnPlaybackStopped());
}

// ===================================================================== Eklentiler

public sealed class WelcomePlugin : FrixPlugin
{
	public override string Id => "welcome";
	public override string NameTr => "Karşılama mesajı";
	public override string NameEn => "Welcome message";
	public override string DescTr => "Sunucuya bağlanan ya da botun kanalına giren kullanıcılara mesaj gönderir.";
	public override string DescEn => "Greets users who connect to the server or join the bot's channel.";
	public override PluginField[] Schema =>
	[
		new("trigger", "select", "Ne zaman", "When", "server", ["server", "channel", "both"],
			"server: sunucuya bağlanınca · channel: botun kanalına girince", "server: on connect · channel: on joining the bot's channel"),
		new("mode", "select", "Gönderim şekli", "Delivery", "private", ["private", "poke", "channel"]),
		new("message", "textarea", "Mesaj", "Message", "[b][color=#00FF00]👋 Hoş geldin {user}! {server} sunucusuna hoş geldin. Müzik için: !ara <şarkı>[/color][/b]",
			null, "Değişkenler: {user} {server} {channel} {time} {date}", "Variables: {user} {server} {channel} {time} {date}"),
		new("cooldown", "number", "Aynı kişiye tekrar (dakika)", "Repeat for same user (minutes)", 60),
	];

	private readonly ConcurrentDictionary<string, DateTime> lastSent = new();

	public override Task OnClientConnected(ClientId id, string name, Uid uid) => S("trigger") is "server" or "both" ? Send(id, name, uid) : Task.CompletedTask;
	public override Task OnClientJoinedBotChannel(ClientId id, string name, Uid uid) => S("trigger") is "channel" or "both" ? Send(id, name, uid) : Task.CompletedTask;

	private async Task Send(ClientId id, string name, Uid uid)
	{
		var now = DateTime.UtcNow;
		var key = uid.Value ?? id.ToString();
		if (lastSent.TryGetValue(key, out var last) && now - last < TimeSpan.FromMinutes(Math.Max(0, I("cooldown"))))
			return;
		lastSent[key] = now;
		var text = Fill(S("message"), ("user", name), ("server", Ctx.ServerName), ("channel", Ctx.ChannelName),
			("time", DateTime.Now.ToString("HH:mm")), ("date", DateTime.Now.ToString("dd.MM.yyyy")));
		await Ctx.RunOnBot(() => S("mode") switch
		{
			"poke" => Ctx.Client.SendPoke(text.Length > 100 ? text[..100] : text, id),
			"channel" => Ctx.Client.SendChannelMessage(text),
			_ => Ctx.Client.SendMessage(text, id),
		});
	}
}

public sealed class AdvertisementPlugin : FrixPlugin
{
	public override string Id => "advertisement";
	public override string NameTr => "Duyurular";
	public override string NameEn => "Announcements";
	public override string DescTr => "Belirli aralıklarla dönen duyuru/reklam mesajları ve belirli gün ve saatlerde gönderilen zamanlı duyurular.";
	public override string DescEn => "Rotating announcement/ad messages at an interval, plus scheduled messages on specific days and times.";
	public override PluginField[] Schema =>
	[
		new("messages", "textarea", "Dönen mesajlar (her satır bir mesaj)", "Rotating messages (one per line)",
			"[b][color=#00FF00]🎵 Müzik botunu kullanmak için !yardim yazın.[/color][/b]\n[b][color=#00FF00]📻 Radyolar için: !radyo[/color][/b]",
			null, "Boş bırakırsan sadece zamanlı duyurular gönderilir.", "Leave empty to send only scheduled messages."),
		new("interval", "number", "Aralık (dakika)", "Interval (minutes)", 30),
		new("order", "select", "Sıra", "Order", "sequential", ["sequential", "random"]),
		new("target", "select", "Nereye", "Target", "channel", ["channel", "server"]),
		new("schedule", "textarea", "Zamanlı duyurular (her satır: [günler] SS:DD | mesaj)", "Scheduled messages (one per line: [days] HH:MM | message)",
			"", null, "Örn: 21:00 | 🎮 Akşam etkinliği başlıyor! — Cmt,Paz 12:00 | ☀️ İyi hafta sonları! Günler: pzt sal çar per cum cmt paz; boş = her gün.",
			"E.g. 21:00 | 🎮 Event starts! — sat,sun 12:00 | ☀️ Have a nice weekend! Days: mon tue wed thu fri sat sun; empty = every day."),
	];

	private string lastMinute = "";

	private DateTime next = DateTime.MinValue;
	private int index;

	protected override void OnEnable() => next = DateTime.UtcNow.AddMinutes(1);
	protected override void OnConfigChanged() => next = DateTime.UtcNow.AddMinutes(Math.Max(1, I("interval")));

	public override async Task OnTick(DateTime now)
	{
		await RunSchedule();
		if (now < next) return;
		next = now.AddMinutes(Math.Max(1, I("interval")));
		var lines = S("messages").Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToArray();
		if (lines.Length == 0) return;
		var msg = S("order") == "random" ? lines[Random.Shared.Next(lines.Length)] : lines[index++ % lines.Length];
		msg = Fill(msg, ("server", Ctx.ServerName), ("channel", Ctx.ChannelName), ("time", DateTime.Now.ToString("HH:mm")));
		await Ctx.RunOnBot(() => S("target") == "server" ? Ctx.Client.SendServerMessage(msg) : Ctx.Client.SendChannelMessage(msg));
	}

	/// <summary>Zamanlı duyurular (eski "Zamanlı duyuru" eklentisi).</summary>
	private async Task RunSchedule()
	{
		var local = DateTime.Now;
		var minute = local.ToString("yyyyMMddHHmm");
		if (minute == lastMinute) return;
		lastMinute = minute;
		foreach (var raw in S("schedule").Split('\n'))
		{
			var bar = raw.IndexOf('|');
			if (bar < 0) continue;
			var when = raw[..bar].Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
			var msg = raw[(bar + 1)..].Trim();
			if (when.Length == 0 || msg.Length == 0 || when[^1] != local.ToString("HH:mm")) continue;
			if (when.Length > 1 && !ScheduledPlugin.DayMatches(when[0], local.DayOfWeek)) continue;
			msg = Fill(msg, ("server", Ctx.ServerName), ("channel", Ctx.ChannelName), ("time", local.ToString("HH:mm")), ("date", local.ToString("dd.MM.yyyy")));
			await Ctx.RunOnBot(() => S("target") == "server" ? Ctx.Client.SendServerMessage(msg) : Ctx.Client.SendChannelMessage(msg));
		}
	}
}

public sealed class AlonePausePlugin : FrixPlugin
{
	public override string Id => "alonepause";
	public override string NameTr => "Yalnızken duraklat";
	public override string NameEn => "Pause when alone";
	public override string DescTr => "Botun kanalında dinleyen kimse kalmayınca müziği duraklatır, biri gelince devam ettirir.";
	public override string DescEn => "Pauses music when nobody is left in the bot's channel and resumes when someone joins.";
	public override PluginField[] Schema =>
	[
		new("resume", "bool", "Biri gelince devam et", "Resume when someone joins", true),
		new("stopAfter", "number", "Bu kadar dakika sonra tamamen durdur (0 = hiç)", "Stop completely after minutes (0 = never)", 0),
	];

	private bool pausedByMe;
	private DateTime aloneSince = DateTime.MaxValue;

	public override Task OnAloneChanged(bool alone) => Ctx.RunOnBot(() =>
	{
		if (alone)
		{
			aloneSince = DateTime.UtcNow;
			if (Ctx.PlayManager.IsPlaying && !Ctx.Player.Paused)
			{
				Ctx.Player.Paused = true;
				pausedByMe = true;
			}
		}
		else
		{
			aloneSince = DateTime.MaxValue;
			if (pausedByMe && B("resume"))
				Ctx.Player.Paused = false;
			pausedByMe = false;
		}
		return Task.CompletedTask;
	});

	public override async Task OnTick(DateTime now)
	{
		var mins = I("stopAfter");
		if (mins > 0 && pausedByMe && now - aloneSince > TimeSpan.FromMinutes(mins))
		{
			pausedByMe = false;
			await Ctx.RunOnBot(() => Ctx.PlayManager.Stop());
		}
	}
}

public sealed class IdleRadioPlugin : FrixPlugin
{
	public override string Id => "idleradio";
	public override string NameTr => "Boştayken radyo";
	public override string NameEn => "Idle radio";
	public override string DescTr => "Kuyruk bitip bot belirli bir süre boş kalınca seçilen radyoyu çalar.";
	public override string DescEn => "Plays a chosen radio station when the queue is empty for a while.";
	public override PluginField[] Schema =>
	[
		new("radio", "radio", "Radyo", "Radio station", ""),
		new("delay", "number", "Kaç dakika boş kalınca", "Idle minutes before playing", 5),
	];

	private DateTime idleSince = DateTime.UtcNow;

	public override Task OnPlaybackStopped() { idleSince = DateTime.UtcNow; return Task.CompletedTask; }

	public override async Task OnTick(DateTime now)
	{
		if (Ctx.PlayManager.IsPlaying || Ctx.PlayManager.IsLoading)
		{
			idleSince = now;
			return;
		}
		if (now - idleSince < TimeSpan.FromMinutes(Math.Max(1, I("delay"))))
			return;
		idleSince = now.AddMinutes(10); // başarısız olursa 10 dk sonra tekrar dene
		var station = FrixRadio.Find(S("radio"));
		if (station is null) return;
		var url = await FrixRadio.ResolveStreamUrl(station.Url);
		var res = new AudioResource(url, station.Name, "media").Add("radio", station.Name);
		await Ctx.RunOnBot(() => Ctx.PlayManager.PlayNow(InvokerData.Anonymous, res));
	}
}

public sealed class AfkMoverPlugin : FrixPlugin
{
	public override string Id => "afkmover";
	public override string NameTr => "AFK taşıyıcı";
	public override string NameEn => "AFK mover";
	public override string DescTr => "Belirli süre boşta kalan, 'uzakta' olan ya da hoparlörünü kapatan kullanıcıları AFK kanalına taşır; dönünce geri alır.";
	public override string DescEn => "Moves idle, away or speaker-muted users to an AFK channel and brings them back when they return.";
	public override PluginField[] Schema =>
	[
		new("channel", "text", "AFK kanalı (ad ya da ID)", "AFK channel (name or ID)", "AFK"),
		new("idleMinutes", "number", "Boşta kalma süresi (dakika, 0 = kapalı)", "Idle time (minutes, 0 = off)", 15),
		new("away", "bool", "'Uzakta' durumundakileri taşı", "Move users set to away", true),
		new("outputMuted", "bool", "Hoparlörü kapalı olanları taşı", "Move users with speakers muted", false),
		new("moveBack", "bool", "Dönünce eski kanalına geri al", "Move back when active again", true),
		new("excludeGroups", "text", "Hariç tutulan sunucu grubu ID'leri (virgülle)", "Excluded server group IDs (comma separated)", "",
			null, "Örn: 6, 9 (yöneticiler taşınmaz)", "E.g. 6, 9 (admins are not moved)"),
		new("message", "text", "Taşınana özel mesaj (boş = gönderme)", "Private message to moved user (empty = none)", "💤 AFK olduğun için AFK kanalına taşındın."),
	];

	private readonly Dictionary<ClientId, ChannelId> movedFrom = new();
	private DateTime nextCheck = DateTime.MinValue;
	private bool warned;

	protected override void OnDisable() => movedFrom.Clear();

	public override async Task OnTick(DateTime now)
	{
		if (now < nextCheck) return;
		nextCheck = now.AddSeconds(30);
		var afk = Ctx.FindChannel(S("channel"));
		if (afk is null)
		{
			if (!warned) { warned = true; NLog.LogManager.GetCurrentClassLogger().Warn("AFK taşıyıcı: '{0}' kanalı bulunamadı.", S("channel")); }
			return;
		}
		warned = false;
		var excluded = PluginContext.ParseIds(S("excludeGroups"));
		var idleLimit = TimeSpan.FromMinutes(Math.Max(0, I("idleMinutes")));

		foreach (var c in Ctx.RealClients().Take(200))
		{
			if (c.ServerGroups.Any(g => excluded.Contains(g.Value))) continue;
			var info = await Ctx.Full.ClientInfo(c.Id);
			if (!info.Ok) continue;
			var ci = info.Value;
			var isAfk = (idleLimit > TimeSpan.Zero && ci.ClientIdleTime >= idleLimit)
				|| (B("away") && ci.IsAway)
				|| (B("outputMuted") && ci.OutputMuted);

			if (isAfk && c.Channel != afk.Value)
			{
				var from = c.Channel;
				var res = await Ctx.Full.ClientMove(c.Id, afk.Value);
				if (res.Ok)
				{
					movedFrom[c.Id] = from;
					var msg = S("message");
					if (msg.Length > 0) await Ctx.RunOnBot(() => Ctx.Client.SendMessage(Fill(msg, ("user", c.Name)), c.Id));
				}
			}
			else if (!isAfk && c.Channel == afk.Value && B("moveBack") && movedFrom.Remove(c.Id, out var back))
			{
				if (Ctx.Full.Book.Channels.ContainsKey(back))
					await Ctx.Full.ClientMove(c.Id, back);
			}
		}
		foreach (var gone in movedFrom.Keys.Where(k => !Ctx.Full.Book.Clients.ContainsKey(k)).ToArray())
			movedFrom.Remove(gone);
	}
}

public sealed class NameFilterPlugin : FrixPlugin
{
	public override string Id => "namefilter";
	public override string NameTr => "İsim filtresi";
	public override string NameEn => "Nickname filter";
	public override string DescTr => "Takma adında yasaklı kelime geçen kullanıcıları uyarır ya da sunucudan atar.";
	public override string DescEn => "Warns or kicks users whose nickname contains a forbidden word.";
	public override PluginField[] Schema =>
	[
		new("words", "textarea", "Yasaklı kelimeler (her satıra bir tane)", "Forbidden words (one per line)", ""),
		new("action", "select", "Ne yapılsın", "Action", "poke", ["poke", "kick"]),
		new("message", "text", "Mesaj", "Message", "⚠️ Takma adın sunucu kurallarına uymuyor, lütfen değiştir."),
		new("excludeGroups", "text", "Hariç tutulan sunucu grubu ID'leri", "Excluded server group IDs", ""),
	];

	private readonly Dictionary<ClientId, string> checkedNames = new();
	private DateTime nextCheck = DateTime.MinValue;

	private string[] Words() => S("words").Split('\n').Select(w => w.Trim().ToLowerInvariant()).Where(w => w.Length > 0).ToArray();

	public override Task OnClientConnected(ClientId id, string name, Uid uid) => Check(id, name, null);

	public override async Task OnTick(DateTime now)
	{
		if (now < nextCheck) return;
		nextCheck = now.AddSeconds(60);
		foreach (var c in Ctx.RealClients())
			if (!checkedNames.TryGetValue(c.Id, out var n) || n != c.Name)
				await Check(c.Id, c.Name, c);
		foreach (var gone in checkedNames.Keys.Where(k => !Ctx.Full.Book.Clients.ContainsKey(k)).ToArray())
			checkedNames.Remove(gone);
	}

	private async Task Check(ClientId id, string name, TSLib.Full.Book.Client? client)
	{
		checkedNames[id] = name;
		var words = Words();
		if (words.Length == 0) return;
		var lower = name.ToLowerInvariant();
		if (!words.Any(lower.Contains)) return;
		client ??= Ctx.Full.Book.Clients.GetValueOrDefault(id);
		var excluded = PluginContext.ParseIds(S("excludeGroups"));
		if (client != null && client.ServerGroups.Any(g => excluded.Contains(g.Value))) return;
		var msg = Fill(S("message"), ("user", name));
		if (S("action") == "kick")
			await Ctx.Full.KickClientFromServer(id, msg.Length > 80 ? msg[..80] : msg);
		else
			await Ctx.RunOnBot(() => Ctx.Client.SendPoke(msg.Length > 100 ? msg[..100] : msg, id));
	}
}

public sealed class SupportNotifyPlugin : FrixPlugin
{
	public override string Id => "supportnotify";
	public override string NameTr => "Destek bildirimi";
	public override string NameEn => "Support notifier";
	public override string DescTr => "Destek kanalına biri girince yetkili gruplardaki çevrimiçi kişilere dürtme gönderir.";
	public override string DescEn => "Pokes online staff when someone joins the support channel.";
	public override PluginField[] Schema =>
	[
		new("channel", "text", "Destek kanalı (ad ya da ID)", "Support channel (name or ID)", "Destek"),
		new("groups", "text", "Yetkili sunucu grubu ID'leri (virgülle)", "Staff server group IDs (comma separated)", ""),
		new("staffMessage", "text", "Yetkililere giden mesaj", "Message to staff", "🆘 {user} destek kanalında bekliyor."),
		new("userMessage", "text", "Kullanıcıya giden mesaj", "Message to the user", "👋 Yetkililere haber verildi ({count} kişi çevrimiçi)."),
		new("noStaffMessage", "text", "Yetkili yoksa kullanıcıya", "Message if no staff online", "😔 Şu an çevrimiçi yetkili yok, lütfen daha sonra tekrar dene."),
		new("cooldown", "number", "Aynı kişi için bekleme (dakika)", "Cooldown per user (minutes)", 5),
	];

	private readonly Dictionary<string, DateTime> last = new();

	public override async Task OnClientEnteredChannel(ClientId id, string name, Uid uid, ChannelId channel)
	{
		var support = Ctx.FindChannel(S("channel"));
		if (support is null || channel != support.Value) return;
		var key = uid.Value ?? id.ToString();
		if (last.TryGetValue(key, out var t) && DateTime.UtcNow - t < TimeSpan.FromMinutes(Math.Max(0, I("cooldown")))) return;
		last[key] = DateTime.UtcNow;

		var groups = PluginContext.ParseIds(S("groups"));
		var staff = Ctx.RealClients().Where(c => c.Id != id && c.ServerGroups.Any(g => groups.Contains(g.Value))).ToArray();
		var staffMsg = Fill(S("staffMessage"), ("user", name));
		await Ctx.RunOnBot(async () =>
		{
			foreach (var s in staff)
				await Ctx.Client.SendPoke(staffMsg.Length > 100 ? staffMsg[..100] : staffMsg, s.Id);
			var userMsg = staff.Length > 0 ? Fill(S("userMessage"), ("count", staff.Length.ToString()), ("user", name)) : Fill(S("noStaffMessage"), ("user", name));
			if (userMsg.Length > 0) await Ctx.Client.SendMessage(userMsg, id);
		});
	}
}

public sealed class ChannelInfoPlugin : FrixPlugin
{
	public override string Id => "channelinfo";
	public override string NameTr => "Kanal adında bilgi";
	public override string NameEn => "Channel name info";
	public override string DescTr => "Seçilen kanalın adını çevrimiçi kişi sayısı ve saatle günceller (botun kanal düzenleme yetkisi gerekir).";
	public override string DescEn => "Keeps a channel's name updated with the online count and time (bot needs channel edit permission).";
	public override PluginField[] Schema =>
	[
		new("channel", "text", "Kanal (ad ya da ID)", "Channel (name or ID)", ""),
		new("template", "text", "Kanal adı şablonu", "Name template", "🎧 Çevrimiçi: {online} | {time}",
			null, "Değişkenler: {online} {max} {time} {date} — en fazla 40 karakter", "Variables: {online} {max} {time} {date} — max 40 characters"),
		new("interval", "number", "Güncelleme aralığı (dakika)", "Update interval (minutes)", 1),
	];

	private DateTime next = DateTime.MinValue;
	private ChannelId? channelId;

	protected override void OnConfigChanged() { next = DateTime.MinValue; channelId = null; }

	public override async Task OnTick(DateTime now)
	{
		if (now < next) return;
		next = now.AddMinutes(Math.Max(1, I("interval")));
		// İlk bulunan kanal ID'sini sakla (adı değişeceği için sonradan adla bulunamaz)
		channelId ??= Ctx.FindChannel(S("channel"));
		if (channelId is null || !Ctx.Full.Book.Channels.TryGetValue(channelId.Value, out var ch)) return;
		var max = Ctx.Full.Book.Server?.MaxClients.ToString() ?? "?";
		var name = Fill(S("template"), ("online", Ctx.RealClients().Count().ToString()), ("max", max),
			("time", DateTime.Now.ToString("HH:mm")), ("date", DateTime.Now.ToString("dd.MM")));
		if (name.Length > 40) name = name[..40];
		if (name == ch.Name) return;
		var res = await Ctx.Full.ChannelEdit(channelId.Value, name: name);
		if (!res.Ok)
			NLog.LogManager.GetCurrentClassLogger().Warn("Kanal adı değiştirilemedi: {0}", res.Error.ErrorFormat());
	}
}
