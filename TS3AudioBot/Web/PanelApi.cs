// Frix web panel REST API'si (/panel/api/...). Ayrıntılar: docs/API.md

using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using TS3AudioBot.Audio;
using TS3AudioBot.Config;
using TS3AudioBot.Dependency;
using TS3AudioBot.Frix;
using TS3AudioBot.Helper;
using TS3AudioBot.Playlists;
using TS3AudioBot.ResourceFactories;
using TSLib;

namespace TS3AudioBot.Web;

public static class PanelApi
{
	private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();
	private static CoreInjector? injector;
	private const string CookieFile = "youtube_cookies.txt";

	public static void Init(CoreInjector coreInjector)
	{
		injector = coreInjector;
		FrixAvatar.RefreshAllBots = () => Bots.IterateAll(b => _ = b.RefreshIdleAvatar());
	}

	private static BotManager Bots => injector!.GetModuleOrThrow<BotManager>();
	private static ConfRoot Conf => injector!.GetModuleOrThrow<ConfRoot>();

	private sealed class ApiError(int status, string code) : Exception(code)
	{
		public int Status { get; } = status;
	}

	private static ApiError Bad(string code) => new(400, code);
	private static ApiError NotFound(string code = "not_found") => new(404, code);

	// ================================================================== yönlendirme

	public static async Task Handle(HttpContext ctx, string path)
	{
		var m = ctx.Request.Method;
		var p = path.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries).Select(Uri.UnescapeDataString).ToArray();
		try
		{
			switch (p)
			{
			// ---- herkese açık
			case ["state"] when m == "GET": await Ok(ctx, PanelAuth.State(ctx)); return;
			case ["setup"] when m == "POST": await PanelAuth.Setup(ctx); return;
			case ["login"] when m == "POST": await PanelAuth.Login(ctx); return;
			case ["logout"] when m == "POST": await PanelAuth.Logout(ctx); return;
			}

			// ---- izleyici
			var user = await PanelAuth.Require(ctx, "viewer");
			if (user is null) return;

			switch (p)
			{
			case ["me", "password"] when m == "POST": await ChangeOwnPassword(ctx, user); return;
			case ["bots"] when m == "GET": await Ok(ctx, await ListBots()); return;
			case ["bots", var b, "queue"] when m == "GET": await Ok(ctx, await Queue(b)); return;
			case ["radios"] when m == "GET": await Ok(ctx, FrixRadio.List()); return;
			case ["stats"] when m == "GET": await Ok(ctx, Stats(ctx)); return;
			case ["playlists"] when m == "GET": await Ok(ctx, FrixData.Playlists()); return;
			case ["playlists", var pid] when m == "GET":
				await Ok(ctx, FrixData.PlaylistItems(long.Parse(pid)).Select((i, n) => new
				{
					index = n, i.Type, id = i.ResourceId, i.Title,
					link = i.Type == "youtube" ? "https://youtu.be/" + i.ResourceId : null,
					thumbnail = i.Type == "youtube" ? $"https://i.ytimg.com/vi/{i.ResourceId}/mqdefault.jpg" : null,
				}));
				return;
			}

			// ---- DJ
			if (!await Need(ctx, user, "dj")) return;
			switch (p)
			{
			case ["search"] when m == "GET": await Ok(ctx, await Search(ctx.Request.Query["q"].ToString(), ctx.Request.Query["bot"].ToString())); return;
			case ["bots", var b, "play"] when m == "POST": await Ok(ctx, await PlayQuery(b, user, await PanelAuth.ReadJson(ctx))); return;
			case ["bots", var b, "radio"] when m == "POST": await Ok(ctx, await PlayRadio(b, user, await PanelAuth.ReadJson(ctx))); return;
			case ["bots", var b, "control"] when m == "POST": await Ok(ctx, await Control(b, await PanelAuth.ReadJson(ctx))); return;
			case ["playlists"] when m == "POST": await Ok(ctx, await SaveQueueAsPlaylist(user, await PanelAuth.ReadJson(ctx))); return;
			case ["playlists", var pid, "play"] when m == "POST": await Ok(ctx, await PlayPlaylist(long.Parse(pid), user, await PanelAuth.ReadJson(ctx))); return;
			case ["playlists", var pid, "items", var idx] when m == "DELETE": FrixData.RemovePlaylistItem(long.Parse(pid), int.Parse(idx)); await Ok(ctx, new { ok = true }); return;
			case ["playlists", var pid] when m == "DELETE": FrixData.DeletePlaylist(long.Parse(pid)); await Ok(ctx, new { ok = true }); return;
			}

			// ---- yönetici
			if (!await Need(ctx, user, "admin")) return;
			var body = m is "POST" or "PUT" && ctx.Request.ContentType?.Contains("json") == true ? await PanelAuth.ReadJson(ctx) : [];
			switch (p)
			{
			// botlar
			case ["bots"] when m == "POST": await Ok(ctx, await CreateBot(body)); break;
			case ["bots", var b] when m == "PUT": await Ok(ctx, await UpdateBot(b, body)); break;
			case ["bots", var b] when m == "DELETE": await Ok(ctx, await DeleteBot(b)); break;
			case ["bots", var b, var action] when m == "POST" && action is "start" or "stop" or "restart": await Ok(ctx, await BotPower(b, action)); break;
			// radyolar
			case ["radios"] when m == "POST": await Ok(ctx, new { id = FrixRadio.Add(body.Str("name") ?? "", body.Str("url") ?? "", body.Str("genre")) }); FrixAvatar.Invalidate(); break;
			case ["radios", "import"] when m == "POST": await Ok(ctx, ImportRadios(body.Str("text") ?? "")); break;
			case ["radios", var id] when m == "PUT": FrixRadio.Update(long.Parse(id), body.Str("name") ?? "", body.Str("url") ?? "", body.Str("genre")); await Ok(ctx, new { ok = true }); break;
			case ["radios", var id] when m == "DELETE": FrixRadio.Delete(long.Parse(id)); FrixAvatar.Invalidate(); await Ok(ctx, new { ok = true }); break;
			// eklentiler
			case ["plugins", var b] when m == "GET": await Ok(ctx, ListPlugins(b)); break;
			case ["plugins", var b, var id] when m == "PUT": await Ok(ctx, SavePlugin(b, id, body)); break;
			// özel komutlar
			case ["commands"] when m == "GET": await Ok(ctx, ListCommands()); break;
			case ["commands"] when m == "POST": await Ok(ctx, SaveCommand(null, body)); break;
			case ["commands", "builtin", "defaults"] when m == "POST":
				var dl = body.Str("lang") ?? FrixText.Language;
				if (!FrixText.Languages.Contains(dl)) throw Bad("bad_lang");
				var skippedNames = FrixCommands.ApplyLanguageDefaults(dl);
				FrixAvatar.Invalidate();
				await Ok(ctx, new { ok = true, skipped = skippedNames });
				break;
			case ["commands", "builtin", var name] when m == "PUT":
				FrixCommands.SetBuiltinName(name, body.Str("alias"), body.Bool("keepOld") ?? true);
				FrixAvatar.Invalidate();
				await Ok(ctx, new { ok = true });
				break;
			case ["commands", var id] when m == "PUT": await Ok(ctx, SaveCommand(long.Parse(id), body)); break;
			case ["commands", var id] when m == "DELETE": FrixDb.Exec("DELETE FROM commands WHERE id=$i", ("$i", long.Parse(id))); FrixCommands.Load(); FrixAvatar.Invalidate(); await Ok(ctx, new { ok = true }); break;
			// metinler
			case ["texts"] when m == "GET": await Ok(ctx, FrixText.All(ctx.Request.Query["lang"].ToString() is { Length: > 0 } l ? l : FrixText.Language)); break;
			case ["texts"] when m == "PUT": FrixText.SetOverride(body.Str("lang") ?? FrixText.Language, body.Str("key") ?? throw Bad("key"), body.Str("value")); FrixAvatar.Invalidate(); await Ok(ctx, new { ok = true }); break;
			case ["avatar"] when m == "GET":
				ctx.Response.ContentType = "image/png";
				ctx.Response.Headers["Cache-Control"] = "no-store";
				await ctx.Response.Body.WriteAsync(FrixAvatar.Preview());
				return;
			// kullanıcılar
			case ["users"] when m == "GET": await Ok(ctx, ListUsers()); break;
			case ["users"] when m == "POST": await Ok(ctx, CreateUser(body)); break;
			case ["users", var id] when m == "PUT": await Ok(ctx, UpdateUser(ctx, user, long.Parse(id), body)); break;
			case ["users", var id] when m == "DELETE": await Ok(ctx, DeleteUser(user, long.Parse(id))); break;
			// ayarlar ve bakım
			case ["settings"] when m == "GET": await Ok(ctx, Settings()); break;
			case ["settings"] when m == "PUT": await Ok(ctx, SaveSettings(body)); break;
			case ["cookies"] when m == "POST": await Ok(ctx, await SaveCookies(await PanelAuth.ReadText(ctx))); break;
			case ["cookies"] when m == "DELETE": await Ok(ctx, await SaveCookies("")); break;
			case ["ytdlp", "update"] when m == "POST": await Ok(ctx, await UpdateYtdlp()); break;
			case ["restart"] when m == "POST": await Ok(ctx, Restart(FrixUpdater.UpdateExitCode - 1)); break;
			case ["update", "check"] when m == "GET": await Ok(ctx, await FrixUpdater.Check()); break;
			case ["update", "apply"] when m == "POST": await Ok(ctx, await ApplyUpdate()); break;
			case ["logs"] when m == "GET": await Ok(ctx, Logs(ctx.Request.Query["since"])); break;
			case ["logs", "page"] when m == "GET": await Ok(ctx, LogPage(ctx)); break;
			case ["logs", "clear"] when m == "POST": PanelLog.Clear(); await Ok(ctx, new { ok = true }); break;
			case ["audit"] when m == "GET": await Ok(ctx, AuditPage(ctx)); break;
			case ["audit"] when m == "DELETE":
				FrixDb.Exec("DELETE FROM audit");
				FrixDb.Audit(user.Name, PanelAuth.Ip(ctx), "audit_cleared");
				await Ok(ctx, new { ok = true });
				return;
			default: await PanelAuth.WriteJson(ctx, 404, new { error = "not_found" }); return;
			}

			if (m != "GET")
				FrixDb.Audit(user.Name, PanelAuth.Ip(ctx), m + " /" + string.Join('/', p));
		}
		catch (ApiError e)
		{
			if (!ctx.Response.HasStarted) await PanelAuth.WriteJson(ctx, e.Status, new { error = e.Message });
		}
		catch (ArgumentException e)
		{
			if (!ctx.Response.HasStarted) await PanelAuth.WriteJson(ctx, 400, new { error = e.Message });
		}
		catch (Microsoft.Data.Sqlite.SqliteException e) when (e.SqliteErrorCode == 19)
		{
			if (!ctx.Response.HasStarted) await PanelAuth.WriteJson(ctx, 409, new { error = "duplicate" });
		}
		catch (AudioBotException e)
		{
			if (!ctx.Response.HasStarted) await PanelAuth.WriteJson(ctx, 400, new { error = e.Message });
		}
		catch (Exception e)
		{
			Log.Warn(e, "Panel API hatası: {0}", path);
			if (!ctx.Response.HasStarted) await PanelAuth.WriteJson(ctx, 500, new { error = e.Message });
		}
	}

	private static Task Ok(HttpContext ctx, object? value) => PanelAuth.WriteJson(ctx, 200, value);

	private static async Task<bool> Need(HttpContext ctx, PanelUser user, string role)
	{
		if (PanelAuth.RoleRank(user.Role) >= PanelAuth.RoleRank(role)) return true;
		await PanelAuth.WriteJson(ctx, 403, new { error = "forbidden" });
		return false;
	}

	// ================================================================== botlar

	private static ConfBot[] AllConfigs() => Conf.GetAllBots() ?? [];

	private static Bot? Running(string name) => Bots.GetBotLock(name);

	private static Bot RequireRunning(string name) => Running(name) ?? throw new ApiError(409, "bot_offline");

	/// <summary>Çalışan botun canlı ayarını, yoksa dosyadaki ayarı döner.</summary>
	private static ConfBot ConfigOf(string name)
	{
		var bot = Running(name);
		if (bot != null && bot.Injector.TryGet<ConfBot>(out var live))
			return live;
		if (!Conf.GetBotConfig(name).Get(out var conf, out _))
			throw NotFound("bot_not_found");
		return conf;
	}

	private static async Task<object> ListBots()
	{
		var list = new List<object>();
		foreach (var conf in AllConfigs().OrderBy(c => c.Name))
		{
			var name = conf.Name ?? "?";
			var bot = Running(name);
			object? live = null;
			var status = "offline";
			if (bot != null)
			{
				status = bot.GetInfo().Status switch { BotStatus.Connected => "connected", BotStatus.Connecting => "connecting", _ => "offline" };
				try { live = await bot.Scheduler.Invoke(() => LiveState(bot)); }
				catch (Exception ex) { Log.Debug(ex, "Bot durumu okunamadı"); }
			}
			var c = bot != null && bot.Injector.TryGet<ConfBot>(out var lc) ? lc : conf;
			list.Add(new
			{
				name,
				status,
				run = c.Run.Value,
				address = c.Connect.Address.Value,
				nickname = c.Connect.Name.Value,
				channel = c.Connect.Channel.Value,
				commander = c.Connect.Commander.Value,
				bitrate = c.Audio.Bitrate.Value,
				hasPassword = !string.IsNullOrEmpty(c.Connect.ServerPassword.Password.Value),
				plugins = FrixDb.Query("SELECT id FROM plugins WHERE bot=$b AND enabled=1", ("$b", name)).Select(r => r["id"]).ToArray(),
				live,
			});
		}
		return list;
	}

	private static object LiveState(Bot bot)
	{
		var pm = bot.Injector.GetModuleOrThrow<PlayManager>();
		var player = bot.Injector.GetModuleOrThrow<Player>();
		var pl = bot.Injector.GetModuleOrThrow<PlaylistManager>();
		var data = pm.CurrentPlayData;
		object? playing = null;
		if (data != null)
		{
			var res = data.ResourceData;
			var radio = res.Get("radio");
			playing = new
			{
				title = res.ResourceTitle ?? res.ResourceId,
				link = radio != null ? null : res.AudioType == "youtube" ? "https://youtu.be/" + res.ResourceId : res.ResourceId.StartsWith("http") ? res.ResourceId : null,
				thumbnail = res.AudioType == "youtube" ? $"https://i.ytimg.com/vi/{res.ResourceId}/mqdefault.jpg" : null,
				radio,
				position = (player.Position ?? TimeSpan.Zero).TotalSeconds,
				length = (player.Length ?? data.PlayResource.SongInfo?.Length ?? TimeSpan.Zero).TotalSeconds,
				live = radio != null || ((player.Length ?? data.PlayResource.SongInfo?.Length) is null && (player.Position ?? TimeSpan.Zero) > TimeSpan.FromSeconds(8)),
				paused = player.Paused,
				requestedBy = FrixChat.NameOf(data.EffectiveOwnerUid),
			};
		}
		return new
		{
			playing,
			loading = pm.LoadingTitle is { } lt ? new { title = lt, seconds = (DateTime.UtcNow - pm.LoadingSince).TotalSeconds } : null,
			volume = player.Volume,
			queueCount = pl.CurrentList.Items.Count,
			queueIndex = pl.Index,
			repeat = pl.Loop.ToString().ToLowerInvariant(),
			random = pl.Random,
		};
	}

	private static async Task<object> Queue(string name)
	{
		var bot = Running(name);
		if (bot is null)
			return new { items = Array.Empty<object>(), index = -1 };
		return await bot.Scheduler.Invoke(() =>
		{
			var pl = bot.Injector.GetModuleOrThrow<PlaylistManager>();
			var items = pl.CurrentList.Items.Select((it, i) => new
			{
				index = i,
				title = it.AudioResource.ResourceTitle ?? it.AudioResource.ResourceId,
				type = it.AudioResource.AudioType,
				link = it.AudioResource.AudioType == "youtube" ? "https://youtu.be/" + it.AudioResource.ResourceId : null,
				requestedBy = it.PlayInfo?.ResourceOwnerUid is { } owner ? FrixChat.NameOf(owner) : null,
			}).ToArray();
			return (object)new { items, index = pl.Index };
		});
	}

	private static string BotNameCheck(string? name)
	{
		name = name?.Trim() ?? "";
		if (name.Length is < 2 or > 24 || !name.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-'))
			throw Bad("bad_bot_name");
		return name;
	}

	private static async Task<object> CreateBot(JsonObject body)
	{
		var name = BotNameCheck(body.Str("name"));
		if (AllConfigs().Any(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)))
			throw new ApiError(409, "duplicate");
		Conf.CreateBotConfig(name).UnwrapThrow();
		if (!Conf.GetBotConfig(name).Get(out var conf, out _))
			throw new ApiError(500, "create_failed");
		ApplyBotSettings(conf, body);
		if (body.Str("nickname") is null) conf.Connect.Name.Value = name;
		conf.Commands.Color.Value = true;
		conf.SaveWhenExists().UnwrapThrow();

		// Varsayılan botun avatar resmini ve kısayollarını kopyala
		try
		{
			var src = Path.Combine(AppContext.BaseDirectory, "bots", "default", "sleep.png");
			var dst = Path.Combine(AppContext.BaseDirectory, "bots", name, "sleep.png");
			if (File.Exists(src) && !File.Exists(dst)) File.Copy(src, dst);
		}
		catch { }

		if (body.Bool("start") != false)
			await BotPower(name, "start");
		return new { ok = true, name };
	}

	private static void ApplyBotSettings(ConfBot conf, JsonObject body)
	{
		if (body.Str("address") is { } a) conf.Connect.Address.Value = a.Trim();
		if (body.Str("nickname") is { } n)
		{
			n = n.Trim();
			if (n.Length is < 3 or > 30) throw Bad("bad_nickname");
			conf.Connect.Name.Value = n;
		}
		if (body.Str("channel") is { } ch) conf.Connect.Channel.Value = ch.Trim();
		if (body.Str("serverPassword") is { } pw) { conf.Connect.ServerPassword.Password.Value = pw; conf.Connect.ServerPassword.Hashed.Value = false; }
		if (body.Bool("commander") is { } cm) conf.Connect.Commander.Value = cm;
		if (body.Bool("run") is { } run) conf.Run.Value = run;
		if (body.Num("bitrate") is { } br) conf.Audio.Bitrate.Value = Math.Clamp((int)br, 8, 98);
	}

	private static async Task<object> UpdateBot(string name, JsonObject body)
	{
		var conf = ConfigOf(name);
		var oldAddress = conf.Connect.Address.Value;
		var oldChannel = conf.Connect.Channel.Value;
		var oldNick = conf.Connect.Name.Value;
		var oldCommander = conf.Connect.Commander.Value;
		ApplyBotSettings(conf, body);
		conf.SaveWhenExists().UnwrapThrow();

		var bot = Running(name);
		var reconnect = false;
		if (bot != null)
		{
			reconnect = oldAddress != conf.Connect.Address.Value || body.Str("serverPassword") != null;
			if (!reconnect)
			{
				await bot.Scheduler.InvokeAsync(async () =>
				{
					var ts = bot.Injector.GetModuleOrThrow<Ts3Client>();
					try
					{
						if (oldNick != conf.Connect.Name.Value) await ts.ChangeName(conf.Connect.Name.Value);
						if (oldCommander != conf.Connect.Commander.Value) await ts.SetChannelCommander(conf.Connect.Commander.Value);
					}
					catch (Exception ex) { Log.Warn("Canlı ayar uygulanamadı: {0}", ex.Message); }
				});
				reconnect = oldChannel != conf.Connect.Channel.Value && body.Bool("moveNow") == true;
			}
			if (reconnect)
				await BotPower(name, "restart");
		}
		return new { ok = true, restarted = reconnect };
	}

	private static async Task<object> DeleteBot(string name)
	{
		var bot = Running(name);
		if (bot != null) await Bots.StopBot(bot);
		Conf.DeleteBotConfig(name).UnwrapThrow();
		FrixDb.Exec("DELETE FROM plugins WHERE bot=$b", ("$b", name));
		return new { ok = true };
	}

	private static async Task<object> BotPower(string name, string action)
	{
		var bot = Running(name);
		if (action is "stop" or "restart" && bot != null)
			await Bots.StopBot(bot);
		if (action is "start" or "restart")
		{
			if (action == "restart") await Task.Delay(1500);
			var r = await Bots.RunBotTemplate(name);
			if (!r.Ok) throw new ApiError(400, r.Error);
		}
		return new { ok = true };
	}

	// ================================================================== oynatıcı

	private static InvokerData PanelInvoker(PanelUser user)
	{
		var uid = new Uid("frix-panel:" + user.Name);
		FrixChat.RememberName(uid, user.Name + " (panel)");
		return new InvokerData(uid);
	}

	private static async Task<object> Search(string q, string botName)
	{
		if (string.IsNullOrWhiteSpace(q)) throw Bad("empty");
		var bot = (string.IsNullOrEmpty(botName) ? null : Running(botName))
			?? AllConfigs().Select(c => Running(c.Name ?? "")).FirstOrDefault(b => b != null)
			?? throw new ApiError(409, "bot_offline");
		var resolver = bot.Injector.GetModuleOrThrow<ResolveContext>();
		var list = await resolver.Search("youtube", q.Trim(), CancellationToken.None);
		return list.Take(15).Select(r => new
		{
			id = r.ResourceId,
			title = r.ResourceTitle,
			type = r.AudioType,
			thumbnail = r.AudioType == "youtube" ? $"https://i.ytimg.com/vi/{r.ResourceId}/mqdefault.jpg" : null,
		});
	}

	private static async Task<object> PlayQuery(string name, PanelUser user, JsonObject body)
	{
		var bot = RequireRunning(name);
		var query = (body.Str("query") ?? "").Trim();
		var id = body.Str("id");
		if (query.Length == 0 && string.IsNullOrEmpty(id)) throw Bad("empty");
		var invoker = PanelInvoker(user);
		var now = body.Bool("now") == true;

		AudioResource? res = null;
		string title = body.Str("title") ?? query;
		if (!string.IsNullOrEmpty(id))
			res = new AudioResource(id, body.Str("title"), "youtube");
		else if (query.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || query.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
		{
			if (FrixChat.YoutubeId(query) is { } yt)
			{
				var quick = await ResourceFactories.Youtube.YtdlpStream.QuickTitle(yt, CancellationToken.None);
				res = new AudioResource(yt, quick, "youtube");
				title = quick ?? query;
			}
		}
		else
		{
			var resolver = bot.Injector.GetModuleOrThrow<ResolveContext>();
			res = (await resolver.Search("youtube", query, CancellationToken.None)).FirstOrDefault() ?? throw NotFound("song_not_found");
			title = res.ResourceTitle ?? query;
		}

		// Yükleme arka planda; panel ilerlemeyi /bots üzerinden izler
		_ = bot.Scheduler.InvokeAsync(async () =>
		{
			var pm = bot.Injector.GetModuleOrThrow<PlayManager>();
			try
			{
				if (now && res != null) await pm.PlayNow(invoker, res);
				else if (res != null) await pm.Enqueue(invoker, res);
				else await pm.Enqueue(invoker, query);
			}
			catch (Exception ex) { Log.Warn("Panelden eklenen şarkı çalınamadı: {0}", ex.Message); }
		});
		return new { ok = true, title };
	}

	private static async Task<object> PlayRadio(string name, PanelUser user, JsonObject body)
	{
		var bot = RequireRunning(name);
		var idNum = body.Num("id");
		var station = FrixRadio.List().FirstOrDefault(r => r.Id == (long)(idNum ?? -1)) ?? throw NotFound("radio_not_found");
		var url = await FrixRadio.ResolveStreamUrl(station.Url);
		var invoker = PanelInvoker(user);
		var res = new AudioResource(url, station.Name, "media").Add("radio", station.Name);
		_ = bot.Scheduler.InvokeAsync(async () =>
		{
			try { await bot.Injector.GetModuleOrThrow<PlayManager>().PlayNow(invoker, res); }
			catch (Exception ex) { Log.Warn("Radyo açılamadı: {0}", ex.Message); }
		});
		return new { ok = true, name = station.Name };
	}

	private static async Task<object> Control(string name, JsonObject body)
	{
		var bot = RequireRunning(name);
		var action = body.Str("action") ?? throw Bad("action");
		var value = body.Str("value");
		await bot.Scheduler.InvokeAsync(async () =>
		{
			var pm = bot.Injector.GetModuleOrThrow<PlayManager>();
			var player = bot.Injector.GetModuleOrThrow<Player>();
			var pl = bot.Injector.GetModuleOrThrow<PlaylistManager>();
			var inv = InvokerData.Anonymous;
			switch (action)
			{
			case "pause": player.Paused = value is null ? !player.Paused : value == "true"; break;
			case "next": await pm.Next(inv); break;
			case "previous": await pm.Previous(inv); break;
			case "stop": await pm.Stop(); break;
			case "clear": pl.Clear(); break;
			case "seek":
				var secs = body.Num("value") ?? throw Bad("value");
				await player.Seek(TimeSpan.FromSeconds(Math.Max(0, secs)));
				break;
			case "volume": player.Volume = (float)Math.Clamp(body.Num("value") ?? throw Bad("value"), 0, 100); break;
			case "jump":
				pl.Index = (int)(body.Num("value") ?? throw Bad("value"));
				await pm.Play(inv);
				break;
			case "remove":
				var idx = (int)(body.Num("value") ?? throw Bad("value"));
				var cur = pl.Index;
				if (idx == cur && pm.IsPlaying) throw Bad("cannot_remove_current");
				pl.ModifyPlaylist(".mix", list =>
				{
					if (idx < 0 || idx >= list.Items.Count) throw Bad("bad_index");
					list.RemoveAt(idx);
				}).UnwrapThrow();
				if (idx < cur) pl.Index = cur - 1;
				break;
			case "move":
				var from = (int)(body.Num("from") ?? throw Bad("from"));
				var to = (int)(body.Num("to") ?? throw Bad("to"));
				var current = pl.Index;
				pl.ModifyPlaylist(".mix", list =>
				{
					if (from < 0 || from >= list.Items.Count || to < 0 || to >= list.Items.Count) throw Bad("bad_index");
					var item = list[from];
					list.RemoveAt(from);
					list.Insert(to, item).UnwrapThrow();
				}).UnwrapThrow();
				if (from == current) pl.Index = to;
				else if (from < current && to >= current) pl.Index = current - 1;
				else if (from > current && to <= current) pl.Index = current + 1;
				break;
			case "repeat": pl.Loop = value switch { "one" => LoopMode.One, "all" => LoopMode.All, _ => LoopMode.Off }; break;
			case "random": pl.Random = value == "true"; break;
			default: throw Bad("bad_action");
			}
		});
		return new { ok = true };
	}

	// ================================================================== istatistik ve çalma listeleri

	private static object Stats(HttpContext ctx)
	{
		var days = int.TryParse(ctx.Request.Query["days"], out var d) ? Math.Clamp(d, 0, 3650) : 30;
		var bot = ctx.Request.Query["bot"].ToString() is { Length: > 0 } b ? b : null;
		return new
		{
			days,
			plays = FrixData.PlayCount(days, bot),
			topSongs = FrixData.TopSongs(days, 20, bot).Select(x => new
			{
				title = x.Title, type = x.Type, id = x.ResourceId, count = x.Count,
				link = x.Type == "youtube" ? "https://youtu.be/" + x.ResourceId : null,
				thumbnail = x.Type == "youtube" ? $"https://i.ytimg.com/vi/{x.ResourceId}/mqdefault.jpg" : null,
			}),
			topUsers = FrixData.TopRequesters(days, 20, bot).Select(x => new { user = x.User, count = x.Count }),
			online = FrixData.OnlineTop(50).Select(x => new { name = x.Name, seconds = x.Seconds, lastSeen = x.LastSeen }),
		};
	}

	private static async Task<object> SaveQueueAsPlaylist(PanelUser user, JsonObject body)
	{
		var name = FrixData.NormalizeListName(body.Str("name") ?? "");
		var bot = RequireRunning(body.Str("bot") ?? "");
		var items = await bot.Scheduler.Invoke(() => bot.Injector.GetModuleOrThrow<PlaylistManager>().CurrentList.Items
			.Where(i => i.AudioResource.AudioType != "media")
			.Select(i => new PlaylistEntry(i.AudioResource.AudioType, i.AudioResource.ResourceId, i.AudioResource.ResourceTitle)).ToList());
		if (items.Count == 0) throw Bad("empty_queue");
		var id = FrixData.SavePlaylist(name, items, user.Name);
		return new { ok = true, id, count = items.Count };
	}

	private static async Task<object> PlayPlaylist(long id, PanelUser user, JsonObject body)
	{
		var bot = RequireRunning(body.Str("bot") ?? "");
		var items = FrixData.PlaylistItems(id);
		if (items.Count == 0) throw Bad("empty_playlist");
		var invoker = PanelInvoker(user);
		await bot.Scheduler.InvokeAsync(() => bot.Injector.GetModuleOrThrow<PlayManager>()
			.Enqueue(invoker, items.Select(i => new PlaylistItem(new AudioResource(i.ResourceId, i.Title, i.Type)))));
		return new { ok = true, count = items.Count };
	}

	// ================================================================== radyolar

	private static object ImportRadios(string text)
	{
		int added = 0, skipped = 0;
		string? pendingName = null;
		foreach (var raw in text.Split('\n'))
		{
			var line = raw.Trim();
			if (line.StartsWith("#EXTINF", StringComparison.OrdinalIgnoreCase))
			{
				var comma = line.IndexOf(',');
				pendingName = comma >= 0 ? line[(comma + 1)..].Trim() : null;
				continue;
			}
			if (!line.StartsWith("http", StringComparison.OrdinalIgnoreCase)) continue;
			var name = pendingName ?? new Uri(line).Host;
			pendingName = null;
			try { FrixRadio.Add(name.Length > 40 ? name[..40] : name, line, null); added++; }
			catch { skipped++; }
		}
		return new { added, skipped };
	}

	// ================================================================== eklentiler

	private static object ListPlugins(string bot)
	{
		var rows = FrixDb.Query("SELECT id, enabled, config FROM plugins WHERE bot=$b", ("$b", bot)).ToDictionary(r => (string)r["id"]!);
		return FrixPluginHost.Catalog.Select(p => new
		{
			id = p.Id,
			name = new { tr = p.NameTr, en = p.NameEn },
			description = new { tr = FrixCommands.ApplyNames(p.DescTr), en = FrixCommands.ApplyNames(p.DescEn) },
			enabled = rows.TryGetValue(p.Id, out var r) ? (long)r["enabled"]! != 0 : p.DefaultEnabled,
			config = rows.TryGetValue(p.Id, out var r2) ? JsonNode.Parse((r2["config"] as string) ?? "{}") : new JsonObject(),
			schema = p.Schema.Select(f => new
			{
				key = f.Key,
				type = f.Type,
				label = new { tr = FrixCommands.ApplyNames(f.LabelTr), en = FrixCommands.ApplyNames(f.LabelEn) },
				help = f.HelpTr is null ? null : new { tr = FrixCommands.ApplyNames(f.HelpTr), en = FrixCommands.ApplyNames(f.HelpEn!) },
				@default = f.Default,
				options = f.Options,
			}),
		});
	}

	private static object SavePlugin(string bot, string id, JsonObject body)
	{
		if (!FrixPluginHost.Catalog.Any(p => p.Id == id)) throw NotFound("plugin_not_found");
		if (!AllConfigs().Any(c => c.Name == bot)) throw NotFound("bot_not_found");
		var config = body["config"] as JsonObject ?? [];
		FrixPluginHost.Save(bot, id, body.Bool("enabled") ?? false, (JsonObject)config.DeepClone());
		FrixAvatar.Invalidate();
		return new { ok = true };
	}

	// ================================================================== komutlar

	private static object ListCommands()
	{
		var bots = AllConfigs().Select(c => c.Name).Where(n => n != null).Cast<string>().ToArray();
		return new
		{
			custom = FrixCommands.List(),
			builtin = FrixCommands.BuiltinList().Select(b => new
			{
				trigger = b.Name, usage = b.Usage, alias = b.Alias, keepOld = b.KeepOld,
				plugins = FrixCommands.Requirements.GetValueOrDefault(b.Name)?.Select(r => r.Split(':')[0]).Distinct().ToArray(),
				available = FrixCommands.IsAvailableAny(b.Name, bots),
			}),
		};
	}

	private static object SaveCommand(long? id, JsonObject body)
	{
		var trigger = FrixCommands.NormalizeTrigger(body.Str("trigger") ?? "");
		FrixCommands.ValidateCustomTrigger(trigger);
		var kind = body.Str("kind") ?? "text";
		var value = body.Str("value") ?? "";
		FrixCommands.Validate(kind, value);
		var enabled = body.Bool("enabled") ?? true;
		if (id is null)
			FrixDb.Exec("INSERT INTO commands(trigger,kind,value,enabled,description) VALUES($t,$k,$v,$e,$d)",
				("$t", trigger), ("$k", kind), ("$v", value), ("$e", enabled ? 1 : 0), ("$d", body.Str("description")));
		else
			FrixDb.Exec("UPDATE commands SET trigger=$t, kind=$k, value=$v, enabled=$e, description=$d WHERE id=$i",
				("$t", trigger), ("$k", kind), ("$v", value), ("$e", enabled ? 1 : 0), ("$d", body.Str("description")), ("$i", id));
		FrixCommands.Load();
		FrixAvatar.Invalidate();
		return new { ok = true };
	}

	// ================================================================== kullanıcılar

	private static object ListUsers()
		=> FrixDb.Query("SELECT id, name, role, created, last_login AS lastLogin FROM users ORDER BY name COLLATE NOCASE");

	private static object CreateUser(JsonObject body)
	{
		var name = body.Str("name")?.Trim() ?? "";
		var pass = body.Str("pass") ?? "";
		var role = body.Str("role") ?? "dj";
		if (PanelAuth.ValidateName(name) is { } e1) throw Bad(e1);
		if (PanelAuth.ValidatePassword(pass) is { } e2) throw Bad(e2);
		if (!PanelAuth.Roles.Contains(role)) throw Bad("bad_role");
		PanelAuth.CreateUser(name, pass, role);
		return new { ok = true };
	}

	private static object UpdateUser(HttpContext ctx, PanelUser me, long id, JsonObject body)
	{
		if (body.Str("role") is { } role)
		{
			if (!PanelAuth.Roles.Contains(role)) throw Bad("bad_role");
			if (id == me.Id && role != "admin") throw Bad("cannot_demote_self");
			FrixDb.Exec("UPDATE users SET role=$r WHERE id=$i", ("$r", role), ("$i", id));
		}
		if (body.Str("pass") is { Length: > 0 } pass)
		{
			if (PanelAuth.ValidatePassword(pass) is { } e) throw Bad(e);
			PanelAuth.SetPassword(id, pass, id == me.Id ? PanelAuth.CurrentToken(ctx) : null);
		}
		return new { ok = true };
	}

	private static object DeleteUser(PanelUser me, long id)
	{
		if (id == me.Id) throw Bad("cannot_delete_self");
		FrixDb.Exec("DELETE FROM users WHERE id=$i", ("$i", id));
		return new { ok = true };
	}

	private static async Task ChangeOwnPassword(HttpContext ctx, PanelUser me)
	{
		var body = await PanelAuth.ReadJson(ctx);
		if (!PanelAuth.CheckPassword(me.Id, body.Str("old") ?? ""))
		{
			await PanelAuth.WriteJson(ctx, 400, new { error = "wrong_password" });
			return;
		}
		if (PanelAuth.ValidatePassword(body.Str("new") ?? "") is { } e)
		{
			await PanelAuth.WriteJson(ctx, 400, new { error = e });
			return;
		}
		PanelAuth.SetPassword(me.Id, body.Str("new")!, PanelAuth.CurrentToken(ctx));
		FrixDb.Audit(me.Name, PanelAuth.Ip(ctx), "password_changed");
		await Ok(ctx, new { ok = true });
	}

	// ================================================================== ayarlar / bakım

	private static object Settings()
	{
		var cookie = Path.GetFullPath(CookieFile);
		return new
		{
			lang = FrixText.Language,
			languages = FrixText.Languages,
			commandLang = FrixDb.GetMeta("cmd_names_lang") ?? "en",
			chat = new { color = FrixChat.DefaultColor, errorColor = FrixChat.ErrorColor, bold = FrixChat.Bold },
			version = FrixUpdater.CurrentVersion,
			updateRepo = FrixUpdater.Repo,
			os = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
			ytdlp = FrixTools.FindYtdlp() != null,
			cookies = new { exists = File.Exists(cookie), updated = File.Exists(cookie) ? File.GetLastWriteTimeUtc(cookie).ToString("O") : null },
		};
	}

	private static object SaveSettings(JsonObject body)
	{
		if (body.Str("lang") is { } lang) { FrixText.SetLanguage(lang); FrixAvatar.Invalidate(); }
		if (body["chat"] is JsonObject chat)
		{
			foreach (var (key, meta) in new[] { ("color", "chat_color"), ("errorColor", "chat_error_color") })
				if (chat.Str(key) is { } c)
				{
					c = c.Trim();
					if (c.Length > 0 && !FrixChat.IsValidColor(c)) throw Bad("bad_color");
					FrixDb.SetMeta(meta, c.Length == 0 ? null : c);
				}
			if (chat.Bool("bold") is { } bold) FrixDb.SetMeta("chat_bold", bold ? "1" : "0");
		}
		if (body.Str("updateRepo") is { } repo)
		{
			repo = repo.Trim().Replace("https://github.com/", "").Trim('/');
			if (repo.Length > 0 && (repo.Count(c => c == '/') != 1 || repo.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '/' or '-' or '_' or '.'))))
				throw Bad("bad_repo");
			FrixDb.SetMeta("update_repo", repo.Length == 0 ? null : repo);
		}
		return new { ok = true };
	}

	private static async Task<object> SaveCookies(string text)
	{
		var remove = text.Trim().Length == 0;
		if (!remove && !(text.Contains("# Netscape HTTP Cookie File") || text.Contains("# HTTP Cookie File") || text.Contains(".youtube.com\t")))
			throw Bad("bad_cookie_file");
		if (remove) { if (File.Exists(CookieFile)) File.Delete(CookieFile); }
		else await File.WriteAllTextAsync(CookieFile, text);

		var conf = FrixTools.YtdlpConf;
		Directory.CreateDirectory(Path.GetDirectoryName(conf)!);
		var lines = File.Exists(conf) ? File.ReadAllLines(conf).ToList() : ["--js-runtimes deno", "--remote-components ejs:github"];
		lines.RemoveAll(l => l.TrimStart().StartsWith("--cookies", StringComparison.Ordinal));
		if (!remove) lines.Add("--cookies \"" + Path.GetFullPath(CookieFile) + "\"");
		File.WriteAllLines(conf, lines);
		Log.Info(remove ? "YouTube çerez dosyası kaldırıldı." : "YouTube çerez dosyası güncellendi.");
		return new { ok = true };
	}

	private static async Task<object> UpdateYtdlp()
	{
		var exe = FrixTools.FindYtdlp() ?? throw NotFound("ytdlp_not_found");
		using var proc = new Process
		{
			StartInfo = new ProcessStartInfo(exe, "-U")
			{
				UseShellExecute = false, CreateNoWindow = true,
				RedirectStandardOutput = true, RedirectStandardError = true,
				WorkingDirectory = Path.GetDirectoryName(exe)!,
			},
		};
		proc.Start();
		var o = proc.StandardOutput.ReadToEndAsync();
		var e = proc.StandardError.ReadToEndAsync();
		using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
		try { await proc.WaitForExitAsync(cts.Token); }
		catch (OperationCanceledException) { try { proc.Kill(true); } catch { } throw new ApiError(504, "timeout"); }
		return new { ok = proc.ExitCode == 0, output = ((await o) + "\n" + (await e)).Trim() };
	}

	private static object Restart(int code)
	{
		Log.Warn("Web panelden yeniden başlatma istendi.");
		_ = Task.Run(async () =>
		{
			await Task.Delay(800);
			System.Environment.Exit(code);
		});
		return new { ok = true };
	}

	private static async Task<object> ApplyUpdate()
	{
		var info = await FrixUpdater.Check();
		if (!info.Available || info.DownloadUrl is null) throw Bad(info.Error ?? "no_update");
		if (!await FrixUpdater.Download(info.DownloadUrl)) throw new ApiError(500, "download_failed");
		return Restart(FrixUpdater.UpdateExitCode);
	}

	private static (int page, int size) Paging(HttpContext ctx, int defSize = 50)
	{
		var page = int.TryParse(ctx.Request.Query["page"], out var p) ? Math.Max(1, p) : 1;
		var size = int.TryParse(ctx.Request.Query["size"], out var s) ? Math.Clamp(s, 10, 200) : defSize;
		return (page, size);
	}

	private static object LogPage(HttpContext ctx)
	{
		var (page, size) = Paging(ctx, 100);
		var (total, items) = PanelLog.Page(page, size, ctx.Request.Query["level"], ctx.Request.Query["q"]);
		return new
		{
			page, size, total, pages = Math.Max(1, (total + size - 1) / size),
			items = items.Select(e => new { seq = e.Seq, time = e.Time.ToString("yyyy-MM-dd HH:mm:ss"), level = e.Level, source = e.Source, message = e.Message }),
		};
	}

	private static object AuditPage(HttpContext ctx)
	{
		var (page, size) = Paging(ctx);
		var q = ctx.Request.Query["q"].ToString();
		var like = "%" + q.Replace("%", "").Replace("_", "") + "%";
		const string where = "WHERE ($q = '' OR user LIKE $l OR action LIKE $l OR detail LIKE $l OR ip LIKE $l)";
		var total = (long)(FrixDb.Scalar("SELECT COUNT(*) FROM audit " + where, ("$q", q), ("$l", like)) ?? 0L);
		var items = FrixDb.Query("SELECT time, user, ip, action, detail FROM audit " + where + " ORDER BY id DESC LIMIT $s OFFSET $o",
			("$q", q), ("$l", like), ("$s", size), ("$o", (page - 1) * size));
		return new { page, size, total, pages = Math.Max(1, (int)((total + size - 1) / size)), items };
	}

	private static object Logs(string? since)
	{
		long.TryParse(since, out var s);
		var (last, items) = PanelLog.Since(s);
		return new { last, items = items.Select(e => new { seq = e.Seq, time = e.Time.ToString("HH:mm:ss"), level = e.Level, message = e.Message }) };
	}
}
