// Frix: kayıt, çalma listeleri, istatistikler, sözler, anket, hava, döviz ve küçük eğlence komutları.

using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TS3AudioBot.Audio;
using TS3AudioBot.CommandSystem;
using TS3AudioBot.Dependency;
using TS3AudioBot.Config;
using TS3AudioBot.Frix;
using TS3AudioBot.Playlists;
using TS3AudioBot.ResourceFactories;

namespace TS3AudioBot;

public static partial class MainCommands
{
	private static T RequirePlugin<T>(ConfBot conf) where T : FrixPlugin
		=> FrixPluginHost.Enabled<T>(conf.Name) ?? throw new CommandException(FrixText.Get("plugin.off"), CommandExceptionReason.CommandError);

	private static FunPlugin RequireFun(ConfBot conf, string key)
	{
		var fun = RequirePlugin<FunPlugin>(conf);
		if (!fun.Allows(key)) throw new CommandException(FrixText.Get("plugin.off"), CommandExceptionReason.CommandError);
		return fun;
	}

	private static string CallerName(InvokerData invoker) => invoker is ClientCall cc ? cc.NickName ?? "?" : FrixText.Raw("user.webpanel");

	// ================================================================== kayıt

	[Command("kayit")]
	public static async Task<string> CommandKayit(ConfBot conf, ClientCall caller, string isim)
		=> await RequirePlugin<RegistrationPlugin>(conf).Register(caller, isim);

	// ================================================================== çalma listeleri

	// Not: "liste" ve "anket" bilerek tek komut; alt komut grubu olsaydı esnek eşleştirici
	// (ic3) serbest metni yanlış alt komuta yönlendirebilirdi (örn. "!anket Soru…" -> "bitir").
	[Command("liste")]
	[Usage("[kaydet|cal|ekle|goster|sil] <ad>", "Çalma listeleri")]
	public static async Task<string> CommandListe(ExecutionInformation info, ConfBot conf, PlayManager pm, PlaylistManager pl, InvokerData invoker, string? islem = null)
	{
		var text = (islem ?? "").Trim();
		var space = text.IndexOf(' ');
		var verb = (space < 0 ? text : text[..space]).ToLowerInvariant();
		var name = space < 0 ? "" : text[(space + 1)..].Trim();
		ListVerb? v = verb switch
		{
			"kaydet" or "save" => ListVerb.Save,
			"cal" or "çal" or "play" => ListVerb.Play,
			"ekle" or "add" => ListVerb.Add,
			"goster" or "göster" or "show" => ListVerb.Show,
			"sil" or "delete" or "del" => ListVerb.Delete,
			_ => null,
		};
		if (v is null)
		{
			if (text.Length > 0 && FrixData.FindPlaylist(text) is not null)
				return ListeGoster(text); // "!liste rock" -> listeyi göster
			var lists = FrixData.Playlists();
			if (lists.Count == 0) return FrixText.Get("pl.none");
			return FrixText.Get("pl.list", ("list", string.Join(", ", lists.Select(l => $"{l.Name} ({l.Count})"))));
		}
		if (name.Length == 0) return FrixText.Get("pl.usage");
		var cc = invoker as ClientCall;
		var dj = FrixPluginHost.Enabled<DjModePlugin>(conf.Name);
		switch (v)
		{
		case ListVerb.Save:
			{
				var items = pl.CurrentList.Items.Select(i => new PlaylistEntry(i.AudioResource.AudioType, i.AudioResource.ResourceId, i.AudioResource.ResourceTitle))
					.Where(i => i.Type != "media").ToList();
				if (items.Count == 0) return FrixText.Get("pl.emptyqueue");
				FrixData.SavePlaylist(name, items, CallerName(invoker));
				return FrixText.Get("pl.saved", ("name", FrixChat.Escape(name)), ("count", items.Count.ToString()));
			}
		case ListVerb.Add:
			{
				var data = pm.CurrentPlayData ?? throw new CommandException(FrixText.Get("cmd.np.none"), CommandExceptionReason.CommandError);
				var r = data.ResourceData;
				if (r.AudioType == "media") throw new CommandException(FrixText.Get("pl.noradio"), CommandExceptionReason.CommandError);
				FrixData.SavePlaylist(name, [new PlaylistEntry(r.AudioType, r.ResourceId, r.ResourceTitle)], CallerName(invoker), append: true);
				return FrixText.Get("pl.added", ("title", FrixChat.Escape(r.ResourceTitle ?? r.ResourceId)), ("name", FrixChat.Escape(name)));
			}
		case ListVerb.Play:
			{
				if (cc != null && dj?.CheckAdd(cc, pl) is { } err) throw new CommandException(err, CommandExceptionReason.CommandError);
				var list = FrixData.FindPlaylist(name) ?? throw new CommandException(FrixText.Get("pl.notfound", ("name", FrixChat.Escape(name))), CommandExceptionReason.CommandError);
				var items = FrixData.PlaylistItems(list.Id);
				if (items.Count == 0) return FrixText.Get("pl.empty");
				await pm.Enqueue(invoker, items.Select(i => new PlaylistItem(new AudioResource(i.ResourceId, i.Title, i.Type))));
				return FrixText.Get("pl.queued", ("name", FrixChat.Escape(list.Name)), ("count", items.Count.ToString()));
			}
		case ListVerb.Show:
			return ListeGoster(name);
		case ListVerb.Delete:
			{
				if (cc != null && dj?.CheckControl(cc) is { } err) throw new CommandException(err, CommandExceptionReason.CommandError);
				var list = FrixData.FindPlaylist(name) ?? throw new CommandException(FrixText.Get("pl.notfound", ("name", FrixChat.Escape(name))), CommandExceptionReason.CommandError);
				FrixData.DeletePlaylist(list.Id);
				return FrixText.Get("pl.deleted", ("name", FrixChat.Escape(list.Name)));
			}
		}
		return "";
	}

	private enum ListVerb { Save, Play, Add, Show, Delete }

	private static string ListeGoster(string ad)
	{
		var list = FrixData.FindPlaylist(ad) ?? throw new CommandException(FrixText.Get("pl.notfound", ("name", FrixChat.Escape(ad))), CommandExceptionReason.CommandError);
		var items = FrixData.PlaylistItems(list.Id);
		var sb = new StringBuilder(FrixText.Get("pl.header", ("name", FrixChat.Escape(list.Name)), ("count", items.Count.ToString())));
		foreach (var (it, i) in items.Take(15).Select((it, i) => (it, i)))
			sb.Append('\n').Append($"{i + 1}. {FrixChat.Escape(it.Title ?? it.ResourceId)}");
		if (items.Count > 15) sb.Append("\n…");
		return sb.ToString();
	}

	// ================================================================== favoriler

	[Command("fav")]
	public static string CommandFav(ConfBot conf, PlayManager pm, InvokerData invoker, string? islem = null)
	{
		var fav = RequirePlugin<FavoritesPlugin>(conf);
		var uid = invoker.ClientUid.Value;
		if (string.IsNullOrEmpty(uid)) return FrixText.Get("fav.nouser");
		var text = (islem ?? "").Trim();
		var sp = text.IndexOf(' ');
		var verb = (sp < 0 ? text : text[..sp]).ToLowerInvariant();
		if (verb is "sil" or "del" or "delete" or "remove")
		{
			var removed = int.TryParse(sp < 0 ? "" : text[(sp + 1)..].Trim(), out var no) ? FrixData.RemoveFavorite(uid, no) : null;
			return removed is null ? FrixText.Get("fav.badno") : FrixText.Get("fav.removed", ("title", removed.Title ?? removed.ResourceId));
		}
		var res = pm.CurrentPlayData?.ResourceData;
		if (res is null || res.AudioType == "media") return FrixText.Get("fav.nothing");
		try
		{
			return FrixData.AddFavorite(uid, res.AudioType, res.ResourceId, res.ResourceTitle, fav.Max)
				? FrixText.Get("fav.added", ("title", res.ResourceTitle ?? "?"))
				: FrixText.Get("fav.exists", ("title", res.ResourceTitle ?? "?"));
		}
		catch (InvalidOperationException) { return FrixText.Get("fav.limit", ("max", fav.Max.ToString())); }
	}

	[Command("favs")]
	public static async Task<string> CommandFavs(ExecutionInformation info, ConfBot conf, PlayManager pm, PlaylistManager pl, InvokerData invoker, string? islem = null)
	{
		var fav = RequirePlugin<FavoritesPlugin>(conf);
		var uid = invoker.ClientUid.Value;
		if (string.IsNullOrEmpty(uid)) return FrixText.Get("fav.nouser");
		var all = FrixData.Favorites(uid);
		if (all.Count == 0) return FrixText.Get("fav.none");
		var verb = (islem ?? "").Trim().ToLowerInvariant();
		if (verb is "liste" or "list" or "goster" or "göster" or "show")
		{
			var sb = new StringBuilder(FrixText.Get("fav.header", ("count", all.Count.ToString())));
			foreach (var (f, i) in all.Take(30).Select((f, i) => (f, i)))
				sb.Append('\n').Append($"{i + 1}. {FrixChat.Escape(f.Title ?? f.ResourceId)}");
			if (all.Count > 30) sb.Append("\n…");
			return sb.ToString();
		}
		if (invoker is ClientCall cc && FrixPluginHost.Enabled<DjModePlugin>(conf.Name)?.CheckAdd(cc, pl) is { } err)
			throw new CommandException(err, CommandExceptionReason.CommandError);
		var items = (fav.Shuffle ? all.OrderBy(_ => Random.Shared.Next()) : all.AsEnumerable())
			.Select(f => new PlaylistItem(new AudioResource(f.ResourceId, f.Title, f.Type))).ToList();
		await pm.Enqueue(invoker, items);
		return FrixText.Get("fav.queued", ("count", items.Count.ToString()));
	}

	// ================================================================== istatistik

	[Command("istatistik")]
	public static string CommandIstatistik(ConfBot conf)
	{
		var songs = FrixData.TopSongs(30, 5);
		var users = FrixData.TopRequesters(30, 5);
		var sb = new StringBuilder(FrixText.Get("stats.header", ("count", FrixData.PlayCount(30).ToString())));
		if (songs.Count > 0)
		{
			sb.Append('\n').Append(FrixText.Get("stats.songs"));
			foreach (var (s, i) in songs.Select((s, i) => (s, i)))
				sb.Append('\n').Append($"{i + 1}. {FrixChat.Escape(s.Title)} ×{s.Count}");
		}
		if (users.Count > 0)
		{
			sb.Append('\n').Append(FrixText.Get("stats.users"));
			foreach (var (u, i) in users.Select((u, i) => (u, i)))
				sb.Append('\n').Append($"{i + 1}. {FrixChat.Escape(u.User)} ×{u.Count}");
		}
		return sb.ToString();
	}

	[Command("top")]
	public static string CommandTop()
	{
		var top = FrixData.OnlineTop(10);
		if (top.Count == 0) return FrixText.Get("online.none");
		var sb = new StringBuilder(FrixText.Get("online.header"));
		foreach (var (t, i) in top.Select((t, i) => (t, i)))
			sb.Append('\n').Append($"{i + 1}. {FrixChat.Escape(t.Name)} — {FrixData.FormatDuration(t.Seconds)}");
		return sb.ToString();
	}

	[Command("dosya")]
	[Usage("<ad>", "Panelden yüklenen müzik dosyalarından birini kuyruğa ekler")]
	public static async Task<string> CommandDosya(PlayManager playManager, InvokerData invoker, string ad)
	{
		var item = FrixLibrary.Find(ad) ?? throw new CommandException(FrixText.Get("lib.notfound", ("query", FrixChat.Escape(ad))), CommandExceptionReason.CommandError);
		await playManager.Enqueue(invoker, item.Resource());
		return FrixText.Get("lib.added", ("title", FrixChat.Escape(item.Title)));
	}

	[Command("gel")]
	public static Task CommandGel(Ts3Client ts3Client, TSLib.Full.TsFullClient full, ClientCall invoker)
	{
		// private and server chat messages carry no channel: take it from the server state
		var channel = invoker.ChannelId
			?? (invoker.ClientId is { } id && full.Book.Clients.TryGetValue(id, out var c) ? (TSLib.ChannelId?)c.Channel : null)
			?? throw new CommandException(FrixText.Get("come.nochannel"), CommandExceptionReason.CommandError);
		return ts3Client.MoveTo(channel);
	}

	[Command("gorulme")]
	public static string CommandGorulme(TSLib.Full.TsFullClient full, string isim)
	{
		var name = isim.Trim();
		if (name.Length == 0) return FrixText.Get("seen.usage");
		// şu an sunucudaysa
		var now = full.Book.Clients.Values.FirstOrDefault(c => c.ClientType == TSLib.ClientType.Full && c.Name.Contains(name, StringComparison.OrdinalIgnoreCase));
		if (now is not null) return FrixText.Get("seen.now", ("user", FrixChat.Escape(now.Name)));
		if (FrixData.LastSeen(name) is not { } s) return FrixText.Get("seen.never", ("user", FrixChat.Escape(name)));
		var ago = (long)(DateTime.UtcNow - s.LastSeen.ToUniversalTime()).TotalSeconds;
		return FrixText.Get("seen.ago", ("user", FrixChat.Escape(s.Name)), ("ago", FrixData.FormatDuration(Math.Max(60, ago))),
			("date", s.LastSeen.ToLocalTime().ToString("dd.MM.yyyy HH:mm")));
	}

	[Command("sure")]
	public static string CommandSure(InvokerData invoker)
	{
		var uid = invoker.ClientUid.Value;
		var secs = string.IsNullOrEmpty(uid) ? 0 : FrixData.OnlineSeconds(uid);
		if (secs == 0) return FrixText.Get("online.none");
		return FrixText.Get("online.me", ("time", FrixData.FormatDuration(secs)), ("rank", FrixData.OnlineRank(uid!).ToString()));
	}

	// ================================================================== şarkı sözleri

	[Command("soz")]
	public static async Task<string?> CommandSoz(ExecutionInformation info, ConfBot conf, PlayManager pm, InvokerData invoker, string? sarki = null)
	{
		RequireFun(conf, "lyrics");
		var query = sarki ?? pm.CurrentPlayData?.ResourceData.ResourceTitle
			?? throw new CommandException(FrixText.Get("cmd.np.none"), CommandExceptionReason.CommandError);
		FrixWeb.Lyrics? lyr;
		try { lyr = await FrixWeb.FindLyrics(query); }
		catch { throw new CommandException(FrixText.Get("web.error"), CommandExceptionReason.CommandError); }
		if (lyr is null) return FrixText.Get("lyrics.notfound", ("title", FrixChat.Escape(FrixWeb.CleanTitle(query))));

		// Sözler uzun: kanala değil, isteyene özel mesajla parça parça gider
		if (invoker is ClientCall cc && cc.ClientId is { } cid && info.TryGet<Ts3Client>(out var ts))
		{
			await ts.SendMessage($"[b]🎤 {FrixChat.Escape(lyr.Artist)} — {FrixChat.Escape(lyr.Track)}[/b]", cid);
			var chunk = new StringBuilder();
			foreach (var line in lyr.Text.Split('\n'))
			{
				if (chunk.Length + line.Length > 900) { await ts.SendMessage(FrixChat.Escape(chunk.ToString()), cid); chunk.Clear(); }
				chunk.Append(line).Append('\n');
			}
			if (chunk.Length > 0) await ts.SendMessage(FrixChat.Escape(chunk.ToString()), cid);
			return cc.Visibility == TSLib.TextMessageTargetMode.Private ? null : FrixText.Get("lyrics.sent", ("user", cc.NickName ?? ""));
		}
		return lyr.Text.Length > 1500 ? lyr.Text[..1500] + "…" : lyr.Text;
	}

	// ================================================================== anket

	[Command("anket")]
	[Usage("Soru? | Seçenek 1 | Seçenek 2", "Anket başlatır; \"!anket bitir\" bitirir")]
	public static string? CommandAnket(ConfBot conf, InvokerData invoker, string? metin = null)
	{
		var fun = RequireFun(conf, "poll");
		var text = (metin ?? "").Trim();
		if (text.Equals("bitir", StringComparison.OrdinalIgnoreCase) || text.Equals("end", StringComparison.OrdinalIgnoreCase))
			return Broadcast(fun, invoker, fun.EndPoll(), FrixText.Get("poll.none"));
		if (text.Length == 0)
			return fun.Current is { } p ? fun.PollText(p, false) : FrixText.Get("poll.usage");
		var parts = text.Split('|').Select(x => x.Trim()).Where(x => x.Length > 0).ToArray();
		if (parts.Length < 3 || parts.Length > 11) return FrixText.Get("poll.usage");
		if (fun.Current != null) return FrixText.Get("poll.running");
		return Broadcast(fun, invoker, fun.StartPoll(FrixChat.Escape(parts[0]), parts.Skip(1).Select(FrixChat.Escape).ToArray(), CallerName(invoker)), null);
	}

	/// <summary>
	/// Anket metni kanala gider. Komut kanaldan yazıldıysa cevap zaten kanala düşer;
	/// özel mesajdan yazıldıysa kanala ayrıca gönderilir ve yazana kısa bir onay döner.
	/// </summary>
	private static string? Broadcast(FunPlugin fun, InvokerData invoker, string? text, string? whenNull)
	{
		if (text is null) return whenNull;
		if (invoker is ClientCall { Visibility: TSLib.TextMessageTargetMode.Channel }) return text;
		fun.ToChannel(text);
		return FrixText.Get("poll.posted");
	}

	[Command("oy")]
	public static string CommandOy(ConfBot conf, InvokerData invoker, int secenek)
	{
		var fun = RequireFun(conf, "poll");
		var reply = fun.Vote(invoker.ClientUid.Value ?? "?", secenek);
		if (fun.Current is not { } p || !p.Votes.ContainsKey(invoker.ClientUid.Value ?? "?")) return reply;
		// her oydan sonra güncel yüzdeler kanalda paylaşılır
		var summary = fun.PollSummary(p);
		if (invoker is ClientCall { Visibility: TSLib.TextMessageTargetMode.Channel }) return reply + "\n" + summary;
		fun.ToChannel(summary);
		return reply;
	}

	// ================================================================== hava ve döviz

	[Command("hava")]
	public static async Task<string> CommandHava(ConfBot conf, string sehir)
	{
		RequireFun(conf, "weather");
		FrixWeb.Weather? w;
		try { w = await FrixWeb.GetWeather(sehir, FrixText.Language); }
		catch { throw new CommandException(FrixText.Get("web.error"), CommandExceptionReason.CommandError); }
		if (w is null) return FrixText.Get("weather.notfound", ("city", FrixChat.Escape(sehir)));
		return FrixText.Get("weather", ("place", FrixChat.Escape(w.Place)), ("desc", FrixWeb.WeatherText(w.Code, FrixText.Language)),
			("temp", w.Temp.ToString("0")), ("feels", w.Feels.ToString("0")), ("humidity", w.Humidity.ToString()), ("wind", w.Wind.ToString("0")));
	}

	[Command("doviz")]
	public static async Task<string> CommandDoviz(ConfBot conf, string? kod = null, double? miktar = null)
	{
		var fun = RequireFun(conf, "currency");
		var code = (kod ?? "USD").Trim().ToUpperInvariant();
		if (code.Length != 3 || !code.All(char.IsAsciiLetter)) return FrixText.Get("fx.usage");
		var amount = miktar is > 0 ? miktar.Value : 1;
		(double rate, string updated)? r;
		try { r = await FrixWeb.Rate(code, fun.Base); }
		catch { throw new CommandException(FrixText.Get("web.error"), CommandExceptionReason.CommandError); }
		if (r is null) return FrixText.Get("fx.unknown", ("code", code));
		var culture = FrixText.Language == "tr" ? CultureInfo.GetCultureInfo("tr-TR") : CultureInfo.InvariantCulture;
		return FrixText.Get("fx.rate", ("amount", amount.ToString("0.##", culture)), ("code", code),
			("value", (r.Value.rate * amount).ToString("N2", culture)), ("base", fun.Base));
	}

	[Command("dolar")]
	public static Task<string> CommandDolar(ConfBot conf, double? miktar = null) => CommandDoviz(conf, "USD", miktar);

	[Command("euro")]
	public static Task<string> CommandEuro(ConfBot conf, double? miktar = null) => CommandDoviz(conf, "EUR", miktar);

	// ================================================================== zar, yazı-tura, seç

	[Command("zar")]
	public static string CommandZar(ConfBot conf, InvokerData invoker, string? tip = null)
	{
		RequireFun(conf, "dice");
		int count = 1, sides = 6;
		if (!string.IsNullOrWhiteSpace(tip))
		{
			var t = tip.Trim().ToLowerInvariant();
			var d = t.IndexOf('d');
			if (d >= 0) { int.TryParse(t[..d], out count); int.TryParse(t[(d + 1)..], out sides); if (d == 0) count = 1; }
			else int.TryParse(t, out sides);
		}
		count = Math.Clamp(count, 1, 10);
		sides = Math.Clamp(sides, 2, 1000);
		var rolls = Enumerable.Range(0, count).Select(_ => Random.Shared.Next(1, sides + 1)).ToArray();
		var detail = count > 1 ? $"{string.Join(" + ", rolls)} = {rolls.Sum()}" : rolls[0].ToString();
		return FrixText.Get("dice", ("user", FrixChat.Escape(CallerName(invoker))), ("dice", $"{count}d{sides}"), ("result", detail));
	}

	[Command("yazitura")]
	public static string CommandYaziTura(ConfBot conf, InvokerData invoker)
	{
		RequireFun(conf, "dice");
		return FrixText.Get(Random.Shared.Next(2) == 0 ? "coin.heads" : "coin.tails", ("user", FrixChat.Escape(CallerName(invoker))));
	}

	[Command("sec")]
	public static string CommandSec(ConfBot conf, string secenekler)
	{
		RequireFun(conf, "dice");
		var opts = secenekler.Split(['|', ','], StringSplitOptions.RemoveEmptyEntries).Select(o => o.Trim()).Where(o => o.Length > 0).ToArray();
		if (opts.Length < 2) return FrixText.Get("pick.usage");
		return FrixText.Get("pick", ("choice", FrixChat.Escape(opts[Random.Shared.Next(opts.Length)])));
	}
}
