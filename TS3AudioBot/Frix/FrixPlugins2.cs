// Frix eklentileri (2): sunucu yönetimi, müzik kuralları, zamanlı duyuru, eğlence/araçlar.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TSLib;
using TSLib.Full.Book;

namespace TS3AudioBot.Frix;

// ===================================================================== Kayıt botu
public sealed class RegistrationPlugin : FrixPlugin
{
	public override string Id => "registration";
	public override string NameTr => "Kayıt botu";
	public override string NameEn => "Registration";
	public override string DescTr => "Kayıt kanalına giren kayıtsızları yetkililere bildirir; yetkili !kayit <isim> yazınca üye grubunu verir ve lobiye taşır.";
	public override string DescEn => "Notifies staff about unregistered users in the registration channel; staff type !kayit <name> to grant the member group.";
	public override PluginField[] Schema =>
	[
		new("channel", "text", "Kayıt kanalı (ad ya da ID)", "Registration channel (name or ID)", "Kayıt"),
		new("memberGroup", "text", "Verilecek üye grubu ID'si", "Member group ID to grant", ""),
		new("unregisteredGroup", "text", "Kaldırılacak kayıtsız grubu ID'si (boş = yok)", "Unregistered group ID to remove (empty = none)", ""),
		new("staffGroups", "text", "Kayıt yapabilen yetkili grup ID'leri (virgülle)", "Staff group IDs allowed to register (comma separated)", ""),
		new("lobby", "text", "Kayıttan sonra taşınacak kanal (boş = taşıma)", "Channel to move to after registration (empty = don't move)", ""),
		new("staffMessage", "text", "Yetkililere dürtme", "Poke to staff", "📝 {user} kayıt bekliyor → !kayit {user}"),
		new("userMessage", "text", "Bekleyen kişiye mesaj", "Message to the waiting user", "👋 Hoş geldin {user}! Yetkililer bilgilendirildi, birazdan kaydın yapılacak."),
		new("doneMessage", "text", "Kayıt sonrası mesaj", "Message after registration", "✅ Kaydın tamamlandı, aramıza hoş geldin {user}!"),
		new("cooldown", "number", "Aynı kişi için bekleme (dakika)", "Cooldown per user (minutes)", 3),
	];

	private readonly ConcurrentDictionary<string, DateTime> last = new();

	public override async Task OnClientEnteredChannel(ClientId id, string name, Uid uid, ChannelId channel)
	{
		var reg = Ctx.FindChannel(S("channel"));
		if (reg is null || channel != reg.Value) return;
		if (InGroups(id, "memberGroup")) return; // zaten üye
		var key = uid.Value ?? id.ToString();
		if (last.TryGetValue(key, out var t) && DateTime.UtcNow - t < TimeSpan.FromMinutes(Math.Max(0, I("cooldown")))) return;
		last[key] = DateTime.UtcNow;

		var staff = PluginContext.ParseIds(S("staffGroups"));
		var online = Ctx.RealClients().Where(c => c.ServerGroups.Any(g => staff.Contains(g.Value))).ToArray();
		var poke = Fill(S("staffMessage"), ("user", name));
		await Ctx.RunOnBot(async () =>
		{
			foreach (var s in online)
				await Ctx.Client.SendPoke(poke.Length > 100 ? poke[..100] : poke, s.Id);
			var msg = Fill(S("userMessage"), ("user", name));
			if (msg.Length > 0) await Ctx.Client.SendMessage(msg, id);
		});
	}

	/// <summary>!kayit komutu. Hata ya da sonuç metni döner.</summary>
	public async Task<string> Register(ClientCall caller, string target)
	{
		if (caller.ClientId is not { } callerId || !InGroups(callerId, "staffGroups"))
			return FrixText.Get("reg.noperm");
		if (!ulong.TryParse(S("memberGroup"), out var member))
			return FrixText.Get("reg.noconfig");

		var reg = Ctx.FindChannel(S("channel"));
		target = target.Trim().TrimStart('@');
		var matches = Ctx.RealClients().Where(c => c.Name.Contains(target, StringComparison.OrdinalIgnoreCase)).ToList();
		var exact = matches.Where(c => c.Name.Equals(target, StringComparison.OrdinalIgnoreCase)).ToList();
		if (exact.Count == 1) matches = exact;
		else if (reg != null && matches.Count(c => c.Channel == reg.Value) == 1) matches = matches.Where(c => c.Channel == reg.Value).ToList();
		if (matches.Count == 0) return FrixText.Get("reg.notfound", ("user", target));
		if (matches.Count > 1) return FrixText.Get("reg.ambiguous", ("list", string.Join(", ", matches.Take(5).Select(c => c.Name))));
		var who = matches[0];

		var add = await Ctx.Full.ServerGroupAddClient((ServerGroupId)member, who.DatabaseId);
		if (!add.Ok && add.Error.Id != TsErrorCode.database_duplicate_entry)
			return FrixText.Get("reg.failed", ("reason", add.Error.ErrorFormat()));
		if (ulong.TryParse(S("unregisteredGroup"), out var unreg))
			await Ctx.Full.ServerGroupDelClient((ServerGroupId)unreg, who.DatabaseId);
		if (Ctx.FindChannel(S("lobby")) is { } lobby)
			await Ctx.Full.ClientMove(who.Id, lobby);
		var done = Fill(S("doneMessage"), ("user", who.Name));
		if (done.Length > 0) await Ctx.Client.SendMessage(done, who.Id);
		return FrixText.Get("reg.done", ("user", who.Name));
	}
}

// ===================================================================== Geçici kanal oluşturucu
public sealed class TempChannelPlugin : FrixPlugin
{
	public override string Id => "tempchannel";
	public override string NameTr => "Geçici kanal oluşturucu";
	public override string NameEn => "Temporary channel creator";
	public override string DescTr => "'Kanal oluştur' kanalına giren kişiye kendi adıyla geçici bir kanal açar ve taşır; kanal boşalınca TeamSpeak siler.";
	public override string DescEn => "Creates a temporary channel named after users who join the creator channel and moves them in; TeamSpeak deletes it when empty.";
	public override PluginField[] Schema =>
	[
		new("channel", "text", "Oluşturucu kanal (ad ya da ID)", "Creator channel (name or ID)", "➕ Kanal oluştur"),
		new("parent", "text", "Kanalların açılacağı üst kanal (boş = oluşturucunun üstü)", "Parent channel (empty = creator's parent)", ""),
		new("name", "text", "Kanal adı şablonu", "Channel name template", "🔊 {user} kanalı", null, "Değişkenler: {user} {n}", "Variables: {user} {n}"),
		new("maxClients", "number", "En fazla kişi (0 = sınırsız)", "Max clients (0 = unlimited)", 0),
		new("cooldown", "number", "Aynı kişi için bekleme (saniye)", "Cooldown per user (seconds)", 30),
	];

	private readonly ConcurrentDictionary<string, DateTime> last = new();

	public override async Task OnClientEnteredChannel(ClientId id, string name, Uid uid, ChannelId channel)
	{
		var creator = Ctx.FindChannel(S("channel"));
		if (creator is null || channel != creator.Value) return;
		var key = uid.Value ?? id.ToString();
		if (last.TryGetValue(key, out var t) && DateTime.UtcNow - t < TimeSpan.FromSeconds(Math.Max(5, I("cooldown")))) return;
		last[key] = DateTime.UtcNow;

		var book = Ctx.Full.Book;
		var parent = Ctx.FindChannel(S("parent")) ?? (book.Channels.TryGetValue(creator.Value, out var cc) ? cc.Parent : ChannelId.Null);
		var existing = book.Channels.Values.Where(c => c.Parent == parent).Select(c => c.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
		string chName = "";
		for (int n = 1; n < 50; n++)
		{
			chName = Fill(S("name"), ("user", name), ("n", n.ToString()));
			if (n > 1 && !S("name").Contains("{n}")) chName += " " + n;
			if (chName.Length > 40) chName = chName[..40];
			if (!existing.Contains(chName)) break;
		}
		var max = I("maxClients");
		var home = book.OwnClient?.Channel;

		var created = await Ctx.Full.ChannelCreate(chName, parent: parent == ChannelId.Null ? null : parent,
			type: TSLib.Full.Book.ChannelType.Temporary,
			maxClients: max > 0 ? max : null, maxClientsUnlimited: max > 0 ? false : null);
		if (!created.Ok)
		{
			NLog.LogManager.GetCurrentClassLogger().Warn("Geçici kanal açılamadı: {0}", created.Error.ErrorFormat());
			return;
		}
		var newId = created.Value.ChannelId;
		// Önce kişiyi taşı (geçici kanal boş kalırsa hemen silinir), sonra bot kendi kanalına döner
		await Ctx.Full.ClientMove(id, newId);
		if (home is { } h && Ctx.Full.Book.OwnClient?.Channel == newId)
			await Ctx.Full.ClientMove(Ctx.Full.ClientId, h);
	}
}

// ===================================================================== Aktiflik rütbeleri
public sealed class RanksPlugin : FrixPlugin
{
	public override string Id => "ranks";
	public override string NameTr => "Aktiflik rütbeleri";
	public override string NameEn => "Activity ranks";
	public override string DescTr => "Sunucuda kalma süresini sayar ve belirlenen saatlere ulaşanlara sunucu grubu verir. Süreler !sure ve !top ile görülür. Sadece bir botta açın.";
	public override string DescEn => "Counts time spent on the server and grants server groups at set hours. See !sure and !top. Enable on one bot only.";
	public override PluginField[] Schema =>
	[
		new("ranks", "textarea", "Rütbeler (her satır: saat=grupID)", "Ranks (one per line: hours=groupID)", "10=\n50=\n200=",
			null, "Örn: 10=12 → 10 saate ulaşana 12 numaralı grup verilir", "E.g. 10=12 → group 12 at 10 hours"),
		new("removeLower", "bool", "Yeni rütbe gelince eskisini kaldır", "Remove the previous rank when promoted", true),
		new("excludeGroups", "text", "Rütbe verilmeyecek grup ID'leri", "Group IDs that never get ranks", ""),
		new("message", "text", "Rütbe atlayana özel mesaj", "Private message on rank up", "🎖️ Tebrikler {user}! {hours} saate ulaştın ve yeni rütben verildi."),
	];

	private DateTime next = DateTime.MinValue;
	private DateTime lastCount = DateTime.MinValue;
	private readonly HashSet<string> granted = [];

	private List<(int hours, ulong group)> Ranks()
		=> S("ranks").Split('\n').Select(l => l.Split('=')).Where(p => p.Length == 2)
			.Select(p => (h: int.TryParse(p[0].Trim(), out var h) ? h : -1, g: ulong.TryParse(p[1].Trim(), out var g) ? g : 0))
			.Where(x => x.h > 0 && x.g > 0).OrderBy(x => x.h).ToList();

	public override async Task OnTick(DateTime now)
	{
		if (now < next) return;
		next = now.AddSeconds(60);
		var elapsed = lastCount == DateTime.MinValue ? 60 : (int)Math.Clamp((now - lastCount).TotalSeconds, 0, 120);
		lastCount = now;

		var clients = Ctx.RealClients().Where(c => c.Uid is { } u && !string.IsNullOrEmpty(u.Value)).ToList();
		FrixData.AddOnline(clients.Select(c => (c.Uid!.Value.Value, c.Name)).DistinctBy(x => x.Item1), elapsed);

		var ranks = Ranks();
		if (ranks.Count == 0) return;
		var excluded = PluginContext.ParseIds(S("excludeGroups"));
		foreach (var c in clients)
		{
			if (c.ServerGroups.Any(g => excluded.Contains(g.Value))) continue;
			var hours = FrixData.OnlineSeconds(c.Uid!.Value.Value) / 3600;
			var target = ranks.LastOrDefault(r => hours >= r.hours);
			if (target.group == 0) continue;
			var key = c.Uid.Value.Value + ":" + target.group;
			if (c.ServerGroups.Any(g => g.Value == target.group) || granted.Contains(key)) continue;
			granted.Add(key);
			var res = await Ctx.Full.ServerGroupAddClient((ServerGroupId)target.group, c.DatabaseId);
			if (!res.Ok && res.Error.Id != TsErrorCode.database_duplicate_entry)
			{
				NLog.LogManager.GetCurrentClassLogger().Warn("Rütbe verilemedi ({0}): {1}", c.Name, res.Error.ErrorFormat());
				continue;
			}
			if (B("removeLower"))
				foreach (var r in ranks.Where(r => r.group != target.group && c.ServerGroups.Any(g => g.Value == r.group)))
					await Ctx.Full.ServerGroupDelClient((ServerGroupId)r.group, c.DatabaseId);
			var msg = Fill(S("message"), ("user", c.Name), ("hours", target.hours.ToString()));
			if (msg.Length > 0) await Ctx.RunOnBot(() => Ctx.Client.SendMessage(msg, c.Id));
		}
	}
}

// ===================================================================== Kanal koruma
public sealed class ChannelGuardPlugin : FrixPlugin
{
	public override string Id => "channelguard";
	public override string NameTr => "Kanal koruma";
	public override string NameEn => "Channel guard";
	public override string DescTr => "Korunan kanallara izinli gruplar dışından girenleri dışarı taşır ya da kanaldan atar.";
	public override string DescEn => "Moves or kicks users without an allowed group out of protected channels.";
	public override PluginField[] Schema =>
	[
		new("channels", "textarea", "Korunan kanallar (her satıra ad ya da ID)", "Protected channels (one name or ID per line)", ""),
		new("allowedGroups", "text", "İzinli grup ID'leri (virgülle)", "Allowed group IDs (comma separated)", ""),
		new("action", "select", "Ne yapılsın", "Action", "kick", ["kick", "move"]),
		new("moveTo", "text", "Taşınacak kanal (taşı seçiliyse)", "Move to channel (for move)", ""),
		new("message", "text", "Kişiye mesaj", "Message to the user", "⛔ Bu kanala girme yetkin yok."),
	];

	public override async Task OnClientEnteredChannel(ClientId id, string name, Uid uid, ChannelId channel)
	{
		var protectedIds = S("channels").Split('\n').Select(l => Ctx.FindChannel(l)).Where(c => c != null).Select(c => c!.Value).ToHashSet();
		if (!protectedIds.Contains(channel) || InGroups(id, "allowedGroups")) return;
		var msg = Fill(S("message"), ("user", name));
		if (S("action") == "move" && Ctx.FindChannel(S("moveTo")) is { } to)
			await Ctx.Full.ClientMove(id, to);
		else
			await Ctx.Full.KickClientFromChannel(id, msg.Length > 80 ? msg[..80] : msg);
		if (msg.Length > 0) await Ctx.RunOnBot(() => Ctx.Client.SendMessage(msg, id));
	}
}

// ===================================================================== Flood koruması
public sealed class AntiFloodPlugin : FrixPlugin
{
	public override string Id => "antiflood";
	public override string NameTr => "Flood koruması";
	public override string NameEn => "Anti-flood";
	public override string DescTr => "Botun gördüğü sohbette kısa sürede çok mesaj atanı uyarır, tekrarlarsa atar (bot kanal ve özel mesajları görür).";
	public override string DescEn => "Warns users who send too many messages in a short time and kicks repeat offenders (covers chats the bot can see).";
	public override PluginField[] Schema =>
	[
		new("maxMessages", "number", "En fazla mesaj", "Max messages", 5),
		new("seconds", "number", "Kaç saniyede", "Within seconds", 8),
		new("warnings", "number", "Atmadan önce uyarı sayısı", "Warnings before kick", 2),
		new("action", "select", "Son adım", "Final action", "channel", ["channel", "server"],
			"channel: kanaldan at · server: sunucudan at", "channel: kick from channel · server: kick from server"),
		new("warnMessage", "text", "Uyarı (dürtme)", "Warning (poke)", "⚠️ Lütfen flood yapma! ({count}/{max})"),
		new("kickMessage", "text", "Atma sebebi", "Kick reason", "Flood"),
		new("excludeGroups", "text", "Hariç tutulan grup ID'leri", "Excluded group IDs", ""),
	];

	private readonly ConcurrentDictionary<string, Queue<DateTime>> times = new();
	private readonly ConcurrentDictionary<string, (int strikes, DateTime at)> strikes = new();

	public override async Task OnTextMessage(ClientId id, Uid uid, string name, string message)
	{
		if (InGroups(id, "excludeGroups")) return;
		var key = uid.Value ?? id.ToString();
		var now = DateTime.UtcNow;
		var window = TimeSpan.FromSeconds(Math.Max(1, I("seconds")));
		var q = times.GetOrAdd(key, _ => new Queue<DateTime>());
		int count;
		lock (q)
		{
			q.Enqueue(now);
			while (q.Count > 0 && now - q.Peek() > window) q.Dequeue();
			count = q.Count;
			if (count > Math.Max(2, I("maxMessages"))) q.Clear();
			else return;
		}
		var st = strikes.GetValueOrDefault(key);
		if (now - st.at > TimeSpan.FromMinutes(10)) st = (0, now);
		st = (st.strikes + 1, now);
		strikes[key] = st;
		var max = Math.Max(0, I("warnings"));
		if (st.strikes <= max)
		{
			var warn = Fill(S("warnMessage"), ("user", name), ("count", st.strikes.ToString()), ("max", max.ToString()));
			await Ctx.RunOnBot(() => Ctx.Client.SendPoke(warn.Length > 100 ? warn[..100] : warn, id));
			return;
		}
		strikes.TryRemove(key, out _);
		var reason = S("kickMessage");
		if (S("action") == "server") await Ctx.Full.KickClientFromServer(id, reason);
		else await Ctx.Full.KickClientFromChannel(id, reason);
	}
}

// ===================================================================== Oylamayla geç
public sealed class VoteSkipPlugin : FrixPlugin
{
	public override string Id => "voteskip";
	public override string NameTr => "Oylamayla geç";
	public override string NameEn => "Vote skip";
	public override string DescTr => "!gec yazıldığında şarkı hemen geçilmez; botun kanalındakilerin belirli bir oranı oy verince geçilir.";
	public override string DescEn => "!gec doesn't skip immediately; the song is skipped once a share of the listeners in the bot's channel voted.";
	public override PluginField[] Schema =>
	[
		new("percent", "number", "Gereken oy oranı (%)", "Required vote share (%)", 50),
		new("requesterSkips", "bool", "Şarkıyı açan kişi oylamasız geçebilsin", "The requester can skip without a vote", true),
		new("exemptGroups", "text", "Oylamasız geçebilen grup ID'leri", "Group IDs that skip without a vote", ""),
	];

	private string? song;
	private readonly HashSet<string> votes = [];

	public override Task OnSongStarted() { lock (votes) { votes.Clear(); song = null; } return Task.CompletedTask; }

	/// <summary>(geçilsin mi, mesaj)</summary>
	public (bool skip, string? message) Vote(ClientCall caller, Audio.PlayManager pm)
	{
		var data = pm.CurrentPlayData;
		if (data is null) return (true, null);
		if (caller.ClientId is { } cid && InGroups(cid, "exemptGroups")) return (true, null);
		if (B("requesterSkips") && data.EffectiveOwnerUid == caller.ClientUid) return (true, null);

		var me = Ctx.Full.Book.OwnClient;
		var listeners = me is null ? 1 : Math.Max(1, Ctx.RealClients().Count(c => c.Channel == me.Channel));
		var needed = Math.Max(1, (int)Math.Ceiling(listeners * Math.Clamp(I("percent"), 1, 100) / 100.0));
		lock (votes)
		{
			var key = data.ResourceData.UniqueId;
			if (song != key) { votes.Clear(); song = key; }
			votes.Add(caller.ClientUid.Value ?? caller.ClientId?.ToString() ?? "?");
			if (votes.Count >= needed) { votes.Clear(); return (true, null); }
			return (false, FrixText.Get("vote.count", ("count", votes.Count.ToString()), ("needed", needed.ToString())));
		}
	}
}

// ===================================================================== DJ modu ve kuyruk limiti
public sealed class DjModePlugin : FrixPlugin
{
	public override string Id => "djmode";
	public override string NameTr => "DJ yetkisi ve kuyruk limiti";
	public override string NameEn => "DJ mode & queue limit";
	public override string DescTr => "Sadece belirli grupların şarkı eklemesine izin verir ve kişi başına kuyruk sınırı koyar.";
	public override string DescEn => "Only lets certain groups add songs and limits how many songs each user can queue.";
	public override PluginField[] Schema =>
	[
		new("djGroups", "text", "DJ grup ID'leri (boş = herkes ekleyebilir)", "DJ group IDs (empty = everyone can add)", ""),
		new("maxPerUser", "number", "Kişi başına sıradaki en fazla şarkı (0 = sınırsız)", "Max queued songs per user (0 = unlimited)", 3),
		new("controlDjOnly", "bool", "Geç/durdur/ses/bitir de sadece DJ'ler", "Skip/pause/volume/stop for DJs only too", false),
	];

	private bool IsDj(ClientCall caller)
	{
		var groups = PluginContext.ParseIds(S("djGroups"));
		return groups.Count == 0 || (caller.ServerGroups?.Any(g => groups.Contains(g.Value)) ?? false);
	}

	public string? CheckAdd(ClientCall caller, Playlists.PlaylistManager pl)
	{
		if (!IsDj(caller)) return FrixText.Get("dj.notdj");
		var max = I("maxPerUser");
		if (max <= 0) return null;
		var mine = pl.CurrentList.Items.Skip(Math.Max(0, pl.Index + 1)).Count(i => i.PlayInfo?.ResourceOwnerUid == caller.ClientUid);
		return mine >= max ? FrixText.Get("dj.limit", ("max", max.ToString())) : null;
	}

	public string? CheckControl(ClientCall caller) => B("controlDjOnly") && !IsDj(caller) ? FrixText.Get("dj.notdj") : null;
}

// ===================================================================== Zamanlı duyuru
public sealed class ScheduledPlugin : FrixPlugin
{
	public override string Id => "scheduled";
	public override string NameTr => "Zamanlı duyuru";
	public override string NameEn => "Scheduled messages";
	public override string DescTr => "Belirli gün ve saatlerde mesaj gönderir.";
	public override string DescEn => "Sends messages on specific days and times.";
	public override PluginField[] Schema =>
	[
		new("entries", "textarea", "Zamanlar (her satır: [günler] SS:DD | mesaj)", "Schedule (one per line: [days] HH:MM | message)",
			"21:00 | 🎮 Akşam etkinliği başlıyor!\nCmt,Paz 12:00 | ☀️ İyi hafta sonları!",
			null, "Günler: pzt sal çar per cum cmt paz (ya da mon…sun); boş = her gün", "Days: mon tue wed thu fri sat sun (or Turkish); empty = every day"),
		new("target", "select", "Nereye", "Target", "channel", ["channel", "server"]),
	];

	private string lastMinute = "";

	private static readonly Dictionary<string, DayOfWeek> Days = new(StringComparer.OrdinalIgnoreCase)
	{
		["pzt"] = DayOfWeek.Monday, ["sal"] = DayOfWeek.Tuesday, ["çar"] = DayOfWeek.Wednesday, ["car"] = DayOfWeek.Wednesday,
		["per"] = DayOfWeek.Thursday, ["cum"] = DayOfWeek.Friday, ["cmt"] = DayOfWeek.Saturday, ["paz"] = DayOfWeek.Sunday,
		["mon"] = DayOfWeek.Monday, ["tue"] = DayOfWeek.Tuesday, ["wed"] = DayOfWeek.Wednesday, ["thu"] = DayOfWeek.Thursday,
		["fri"] = DayOfWeek.Friday, ["sat"] = DayOfWeek.Saturday, ["sun"] = DayOfWeek.Sunday,
	};

	public override async Task OnTick(DateTime utcNow)
	{
		var now = DateTime.Now;
		var minute = now.ToString("yyyyMMddHHmm");
		if (minute == lastMinute) return;
		lastMinute = minute;
		foreach (var raw in S("entries").Split('\n'))
		{
			var bar = raw.IndexOf('|');
			if (bar < 0) continue;
			var when = raw[..bar].Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
			var msg = raw[(bar + 1)..].Trim();
			if (when.Length == 0 || msg.Length == 0) continue;
			var time = when[^1];
			if (time != now.ToString("HH:mm")) continue;
			if (when.Length > 1)
			{
				var days = when[0].Split(',').Select(d => Days.TryGetValue(d.Trim(), out var dw) ? (DayOfWeek?)dw : null).Where(d => d != null).ToList();
				if (days.Count > 0 && !days.Contains(now.DayOfWeek)) continue;
			}
			msg = Fill(msg, ("server", Ctx.ServerName), ("time", now.ToString("HH:mm")), ("date", now.ToString("dd.MM.yyyy")));
			await Ctx.RunOnBot(() => S("target") == "server" ? Ctx.Client.SendServerMessage(msg) : Ctx.Client.SendChannelMessage(msg));
		}
	}
}

// ===================================================================== Eğlence ve araçlar
public sealed class FunPlugin : FrixPlugin
{
	public override string Id => "fun";
	public override bool DefaultEnabled => true;
	public override string NameTr => "Eğlence ve araçlar";
	public override string NameEn => "Fun & tools";
	public override string DescTr => "!zar, !yazitura, !sec, !anket, !hava, !doviz ve !soz komutları. İstemediklerini kapatabilirsin.";
	public override string DescEn => "!zar, !yazitura, !sec, !anket, !hava, !doviz and !soz commands. Turn off the ones you don't want.";
	public override PluginField[] Schema =>
	[
		new("dice", "bool", "!zar, !yazitura, !sec", "!zar, !yazitura, !sec", true),
		new("poll", "bool", "!anket ve !oy", "!anket and !oy", true),
		new("pollMinutes", "number", "Anket süresi (dakika)", "Poll duration (minutes)", 2),
		new("weather", "bool", "!hava", "!hava", true),
		new("currency", "bool", "!doviz, !dolar, !euro", "!doviz, !dolar, !euro", true),
		new("currencyBase", "text", "Döviz karşılığı para birimi", "Currency to convert into", "TRY"),
		new("lyrics", "bool", "!soz (şarkı sözleri)", "!soz (lyrics)", true),
	];

	public bool Allows(string key) => B(key);
	public string Base => S("currencyBase").Trim().ToUpperInvariant() is { Length: 3 } b ? b : "TRY";
	public int PollMinutes => Math.Clamp(I("pollMinutes"), 1, 60);

	// ---- anket
	public sealed class Poll
	{
		public required string Question;
		public required string[] Options;
		public required DateTime Ends;
		public required string Creator;
		public readonly ConcurrentDictionary<string, int> Votes = new();
	}

	public Poll? Current { get; private set; }

	public string StartPoll(string question, string[] options, string creator)
	{
		if (Current != null) return FrixText.Get("poll.running");
		Current = new Poll { Question = question, Options = options, Ends = DateTime.UtcNow.AddMinutes(PollMinutes), Creator = creator };
		return PollText(Current, false);
	}

	public string PollText(Poll p, bool final)
	{
		var sb = new StringBuilder();
		sb.Append(FrixText.Get(final ? "poll.result" : "poll.header", ("question", p.Question)));
		var total = p.Votes.Count;
		var counts = p.Options.Select((_, i) => p.Votes.Values.Count(v => v == i)).ToArray();
		var top = counts.Length > 0 ? counts.Max() : 0;
		for (int i = 0; i < p.Options.Length; i++)
		{
			var pct = total == 0 ? 0 : counts[i] * 100 / total;
			var bar = new string('█', pct / 10) + new string('░', 10 - pct / 10);
			var win = final && top > 0 && counts[i] == top ? " 🏆" : "";
			sb.Append('\n').Append($"{i + 1}. {p.Options[i]}  {bar} {counts[i]} ({pct}%){win}");
		}
		if (!final) sb.Append('\n').Append(FrixText.Get("poll.howto", ("minutes", Math.Max(1, (int)Math.Ceiling((p.Ends - DateTime.UtcNow).TotalMinutes)).ToString())));
		return sb.ToString();
	}

	public string Vote(string voter, int option)
	{
		var p = Current;
		if (p is null) return FrixText.Get("poll.none");
		if (option < 1 || option > p.Options.Length) return FrixText.Get("poll.badoption", ("max", p.Options.Length.ToString()));
		var changed = p.Votes.ContainsKey(voter);
		p.Votes[voter] = option - 1;
		return FrixText.Get(changed ? "poll.changed" : "poll.voted", ("option", p.Options[option - 1]));
	}

	public string? EndPoll()
	{
		var p = Current;
		if (p is null) return null;
		Current = null;
		return PollText(p, true);
	}

	public override async Task OnTick(DateTime now)
	{
		if (Current is { } p && now >= p.Ends && EndPoll() is { } result)
			await Ctx.RunOnBot(() => Ctx.Client.SendChannelMessage(FrixChat.Colorize(result)));
	}
}
