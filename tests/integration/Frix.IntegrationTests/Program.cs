// Frix integration tests: a real TeamSpeak server, Frix (Docker) and fake users.
// Start the environment with run.ps1 / run.sh, or set the addresses with environment variables:
//   FRIX_PANEL (http://127.0.0.1:18913), FRIX_TS (127.0.0.1:19987), FRIX_QUERY (127.0.0.1:20011),
//   FRIX_QUERY_PASS (frixtest), FRIX_BOT_ADDRESS (ts3:9987 = the TS server as Frix sees it),
//   FRIX_SETUP_CMD (command that prints the panel setup code).
using System.Diagnostics;
using System.Text.Json.Nodes;
using Frix.IntegrationTests;
using TSLib;

string Env(string k, string d) => Environment.GetEnvironmentVariable(k) is { Length: > 0 } v ? v : d;
var panelUrl = Env("FRIX_PANEL", "http://127.0.0.1:18913");
var tsAddr = Env("FRIX_TS", "127.0.0.1:19987");
var queryAddr = Env("FRIX_QUERY", "127.0.0.1:20011");
var queryPass = Env("FRIX_QUERY_PASS", "frixtest");
var botAddr = Env("FRIX_BOT_ADDRESS", "ts3:9987");
var setupCmd = Env("FRIX_SETUP_CMD", "docker compose exec -T frix cat /data/KURULUM_KODU.txt");
var only = args.Length > 0 ? args.ToHashSet(StringComparer.OrdinalIgnoreCase) : null;

const string Bot = "testbot";
const string BotNick = "FrixBot";
var results = new List<(string name, bool ok, string detail, double sec)>();
var panel = new Panel(panelUrl);
using var q = new Query();
var users = new List<TsUser>();
var botUid = ""; // the bot is found by its identity: its nickname changes with the song
var hostFromBot = Env("FRIX_HOST_FROM_BOT", "host.docker.internal"); // this machine as Frix sees it (radio stream)

async Task Test(string name, Func<Task> body)
{
	if (only != null && !only.Contains(name)) return;
	var sw = Stopwatch.StartNew();
	try
	{
		await body();
		results.Add((name, true, "", sw.Elapsed.TotalSeconds));
		Console.WriteLine($"  PASS  {name} ({sw.Elapsed.TotalSeconds:0.0}s)");
	}
	catch (Exception ex)
	{
		var where = "";
		try
		{
			var clients = (await q.Clients()).Where(c => c.GetValueOrDefault("client_type") == "0")
				.Select(c => $"{c["client_nickname"]}@{c["cid"]}");
			var known = (await panel.Get($"bots/{Bot}/channels"))?["channels"]?.AsArray().Select(c => $"{c?["name"]}={c?["id"]}") ?? [];
			var log = (await panel.Get("logs"))?["items"]?.AsArray().TakeLast(30).Select(l => $"{l?["level"]}: {l?["message"]}") ?? [];
			where = $"\n        clients: {string.Join(", ", clients)}\n        channels the bot knows: {string.Join(", ", known)}"
				+ $"\n        frix log:\n          {string.Join("\n          ", log)}";
		}
		catch { }
		results.Add((name, false, ex.Message + where, sw.Elapsed.TotalSeconds));
		Console.WriteLine($"  FAIL  {name} ({sw.Elapsed.TotalSeconds:0.0}s)\n        {ex.Message.Replace("\n", "\n        ")}{where}");
	}
}

async Task Until(Func<Task<bool>> cond, TimeSpan timeout, string what)
{
	var end = DateTime.UtcNow + timeout;
	Exception? last = null;
	while (DateTime.UtcNow < end)
	{
		try { if (await cond()) return; } catch (Exception ex) { last = ex; }
		await Task.Delay(500);
	}
	throw new TimeoutException($"{what} (waited {timeout.TotalSeconds:0}s){(last is null ? "" : ": " + last.Message)}");
}

void Check(bool cond, string message) { if (!cond) throw new Exception(message); }

async Task<TsUser> NewUser(string name)
{
	var u = new TsUser(name);
	await u.Connect(tsAddr);
	users.Add(u);
	return u;
}

async Task<ClientId> ClidOf(string nick)
	=> (ClientId)ushort.Parse((await q.Client(nick) ?? throw new Exception($"{nick} is not on the server"))["clid"]);
async Task<string> ChannelOf(string nick) => (await q.Client(nick) ?? throw new Exception($"{nick} is not on the server"))["cid"];

async Task<Dictionary<string, string>> BotClient()
	=> (await q.Clients()).FirstOrDefault(c => c.GetValueOrDefault("client_unique_identifier") == botUid) ?? throw new Exception("the bot is not on the server");
async Task<ClientId> BotClid() => (ClientId)ushort.Parse((await BotClient())["clid"]);
async Task<string> BotChannel() => (await BotClient())["cid"];
bool FromBot(TsUser.Received r) => r.Uid == botUid;

// Sends a command to the bot in private chat and waits for the answer.
async Task<string> Ask(TsUser u, string command, Func<string, bool>? match = null, int seconds = 15)
{
	var since = DateTime.UtcNow;
	await u.SendPrivate(await BotClid(), command);
	var m = await u.WaitFor(since, r => FromBot(r) && r.Kind != "Poke" && (match?.Invoke(r.Text) ?? true), TimeSpan.FromSeconds(seconds), $"an answer to '{command}'");
	return m.Text;
}

async Task<JsonNode> BotState() => (await panel.Get("bots"))!.AsArray().First(b => b!["name"]!.ToString() == Bot)!;
async Task<string[]> GroupsOf(string nick)
{
	var c = await q.Client(nick) ?? throw new Exception($"{nick} is not on the server");
	return c.GetValueOrDefault("client_servergroups", "").Split(',', StringSplitOptions.RemoveEmptyEntries);
}
async Task<string> Group(string name)
{
	var g = (await q.Cmd("servergrouplist")).FirstOrDefault(x => x["name"] == name && x["type"] == "1");
	if (g != null) return g["sgid"];
	var guestGroup = (await q.Cmd("serverinfo"))[0]["virtualserver_default_server_group"];
	await q.Cmd($"servergroupcopy ssgid={guestGroup} tsgid=0 name={Query.Esc(name)} type=1");
	var sgid = (await q.Cmd("servergrouplist")).First(x => x["name"] == name && x["type"] == "1")["sgid"];
	// the bot (server admin) must be allowed to add and remove members
	await q.Cmd($"servergroupaddperm sgid={sgid} permsid=i_group_needed_member_add_power permvalue=0 permnegated=0 permskip=0|permsid=i_group_needed_member_remove_power permvalue=0 permnegated=0 permskip=0");
	return sgid;
}

Console.WriteLine($"Frix integration tests\n  panel {panelUrl}\n  teamspeak {tsAddr} (bot connects to {botAddr})\n");

// ------------------------------------------------------------------ environment
Console.WriteLine("Setup");
await Until(async () => { await panel.Get("state"); return true; }, TimeSpan.FromMinutes(3), "the panel did not come up");
var state = await panel.Get("state");
if (state?["setupRequired"]?.GetValue<bool>() == true && state?["setupNeedsCode"]?.GetValue<bool>() != true)
	await panel.Post("setup", new { user = "admin", pass = "frix-test-pass", lang = "en" }); // local: no setup code needed
else if (state?["setupRequired"]?.GetValue<bool>() == true)
{
	var psi = new ProcessStartInfo(OperatingSystem.IsWindows() ? "cmd" : "sh") { RedirectStandardOutput = true, UseShellExecute = false };
	psi.ArgumentList.Add(OperatingSystem.IsWindows() ? "/c" : "-c"); psi.ArgumentList.Add(setupCmd);
	string code = "";
	await Until(async () =>
	{
		using var p = Process.Start(psi)!;
		code = (await p.StandardOutput.ReadToEndAsync()).Trim();
		await p.WaitForExitAsync();
		return code.Length > 0;
	}, TimeSpan.FromSeconds(60), "could not read the setup code");
	try { await panel.Post("setup", new { user = "admin", pass = "frix-test-pass", code, lang = "en" }); }
	catch (PanelException ex) when (ex.Status == 409) { }
}
try { await panel.Post("login", new { user = "admin", pass = "frix-test-pass" }); } catch (PanelException ex) when (ex.Status == 409) { }

var qParts = queryAddr.Split(':');
await Until(async () => { await q.Connect(qParts[0], int.Parse(qParts[1]), "serveradmin", queryPass); return true; }, TimeSpan.FromSeconds(60), "server query not reachable");
// the fake users send commands much faster than people; relax the server's anti-flood for them
await q.Cmd("serveredit virtualserver_antiflood_points_tick_reduce=500 virtualserver_antiflood_points_needed_command_block=5000 virtualserver_antiflood_points_needed_ip_block=10000");
foreach (var ch in new[] { "AFK", "Lobby", "Create channel", "Info" })
	if (await q.ChannelId(ch) is null) await q.CreateChannel(ch);

var bots = (await panel.Get("bots"))!.AsArray();
if (!bots.Any(b => b!["name"]!.ToString() == Bot))
	await panel.Post("bots", new { name = Bot, address = botAddr, nickname = BotNick, start = true });
await Test("bot connects to the server", async () =>
{
	await Until(async () => await q.Client(BotNick) != null, TimeSpan.FromSeconds(120), "the bot did not join the server");
	var bot = await q.Client(BotNick);
	botUid = bot!["client_unique_identifier"];
	// server admin, so it may move users, create channels and rename them
	try { await q.Cmd($"servergroupaddclient sgid=6 cldbid={bot!["client_database_id"]}"); } catch (InvalidOperationException) { }
	await Until(async () => (await panel.Get("bots"))!.AsArray().Any(b => b!["name"]!.ToString() == Bot && b["status"]!.ToString() == "connected"),
		TimeSpan.FromSeconds(20), "the panel does not show the bot as connected");
});
if (botUid.Length == 0 && await q.Client(BotNick) is { } botNow) botUid = botNow["client_unique_identifier"];
if (await q.Client(BotNick) is null)
{
	Console.WriteLine("\nThe bot is not on the server, the other tests cannot run.");
	return 1;
}
await Task.Delay(16_000); // plugins ignore joins in the first 15 seconds after connecting

var tester = await NewUser("Tester");

// ------------------------------------------------------------------ commands
Console.WriteLine("\nCommands");
await Test("!help lists commands", async () =>
{
	var t = await Ask(tester, "!help");
	Check(t.Contains("play", StringComparison.OrdinalIgnoreCase), "help does not mention !play: " + t);
});
await Test("!dice (fun plugin)", async () =>
{
	await panel.SetPlugin(Bot, "g_fun", true, []);
	var t = await Ask(tester, "!dice");
	Check(t.Contains("rolled"), "unexpected answer: " + t);
});
await Test("disabled plugin blocks its command", async () =>
{
	await panel.SetPlugin(Bot, "g_fun", false, []);
	try
	{
		var t = await Ask(tester, "!dice");
		Check(t.Contains("disabled", StringComparison.OrdinalIgnoreCase), "expected the 'feature is off' answer, got: " + t);
	}
	finally { await panel.SetPlugin(Bot, "g_fun", true, []); }
});
await Test("disabled music blocks native !play", async () =>
{
	await panel.SetPlugin(Bot, "g_music", false, []);
	try
	{
		var t = await Ask(tester, "!play test");
		Check(t.Contains("disabled", StringComparison.OrdinalIgnoreCase), "expected the 'feature is off' answer, got: " + t);
	}
	finally { await panel.SetPlugin(Bot, "g_music", true, []); }
});
await Test("bot text override", async () =>
{
	await panel.Put("texts", new { lang = "en", key = "dice", value = "DICE-OVERRIDE {result}" });
	try
	{
		var t = await Ask(tester, "!dice");
		Check(t.Contains("DICE-OVERRIDE"), "override not used: " + t);
	}
	finally { await panel.Put("texts", new { lang = "en", key = "dice", value = (string?)null }); }
});
await Test("command rename", async () =>
{
	await panel.Put("commands/builtin/zar", new { alias = "roll" });
	try
	{
		var t = await Ask(tester, "!roll");
		Check(t.Contains("rolled"), "!roll did not run the dice: " + t);
	}
	finally { await panel.Put("commands/builtin/zar", new { alias = "dice" }); }
});
await Test("custom command", async () =>
{
	async Task RemoveOld()
	{
		foreach (var c in (await panel.Get("commands"))!["custom"]!.AsArray())
			if (c!["trigger"]?.ToString().TrimStart('!') == "ittest") await panel.Delete("commands/" + c["id"]);
	}
	await RemoveOld();
	await panel.Post("commands", new { trigger = "ittest", kind = "text", value = "custom-answer-ok", enabled = true });
	try
	{
		var t = await Ask(tester, "!ittest");
		Check(t.Contains("custom-answer-ok"), "unexpected answer: " + t);
	}
	finally { await RemoveOld(); }
});
await Test("!lastseen", async () =>
{
	var t = await Ask(tester, "!lastseen Tester");
	Check(t.Contains("online right now"), "unexpected answer: " + t);
	t = await Ask(tester, "!lastseen NobodyWithThisName");
	Check(t.Contains("never seen"), "unexpected answer: " + t);
});
await Test("script command", async () =>
{
	await panel.Put("scripts/ittest", new { code = "frix.command('ping', (args, ctx) => 'pong ' + ctx.user);", enabled = true });
	try
	{
		var t = await Ask(tester, "!ping", seconds: 20);
		Check(t.Contains("pong Tester"), "unexpected answer: " + t);
	}
	finally { await panel.Delete("scripts/ittest"); }
});
await Test("script syntax error is rejected", async () =>
{
	try { await panel.Put("scripts/broken", new { code = "frix.command('x', (", enabled = true }); }
	catch (PanelException ex) when (ex.Status == 400 && ex.Message.Contains("script_syntax")) { return; }
	throw new Exception("a broken script was saved");
});

// ------------------------------------------------------------------ channel features
Console.WriteLine("\nChannel features");
var lobby = await q.ChannelId("Lobby") ?? throw new Exception("no Lobby");
await tester.Move((ChannelId)ulong.Parse(lobby));
await Test("!come moves the bot to the user", async () =>
{
	var since = DateTime.UtcNow;
	await tester.SendPrivate(await BotClid(), "!come");
	try { await Until(async () => await BotChannel() == lobby, TimeSpan.FromSeconds(10), "the bot did not come to Lobby"); }
	catch (TimeoutException ex)
	{
		var said = string.Join(" | ", tester.Since(since).Where(FromBot).Select(m => m.Text));
		throw new Exception(ex.Message + (said.Length > 0 ? ". The bot said: " + said : ""));
	}
});
await Test("poll in the channel with live percentages", async () =>
{
	var since = DateTime.UtcNow;
	await tester.SendChannel("!poll Best colour? | red | blue");
	await tester.WaitFor(since, r => FromBot(r) && r.Text.Contains("Best colour"), TimeSpan.FromSeconds(10), "the poll in the channel");
	since = DateTime.UtcNow;
	await tester.SendChannel("!vote 1");
	var m = await tester.WaitFor(since, r => FromBot(r) && r.Text.Contains('%'), TimeSpan.FromSeconds(10), "the poll percentages");
	Check(m.Kind.Contains("Channel"), "the percentages were not sent to the channel: " + m.Kind);
	await tester.SendChannel("!poll end").ContinueWith(_ => { });
});

// ------------------------------------------------------------------ automatic features
Console.WriteLine("\nAutomatic features");
await Test("welcome message", async () =>
{
	await panel.SetPlugin(Bot, "g_welcome", true, new() { ["welcome"] = true, ["welcome.trigger"] = "server", ["welcome.mode"] = "private", ["welcome.message"] = "WELCOME {user}", ["welcome.cooldown"] = 0 });
	var since = DateTime.UtcNow;
	var guest = await NewUser("Guest");
	await guest.WaitFor(since, r => FromBot(r) && r.Text.Contains("WELCOME Guest"), TimeSpan.FromSeconds(15), "the welcome message");
});
await Test("nickname filter pokes", async () =>
{
	await panel.SetPlugin(Bot, "g_moderation", true, new() { ["namefilter"] = true, ["namefilter.words"] = "badword", ["namefilter.action"] = "poke", ["namefilter.message"] = "BADNAME {user}" });
	var since = DateTime.UtcNow;
	var bad = await NewUser("xbadwordx");
	await bad.WaitFor(since, r => r.Kind == "Poke" && r.Text.Contains("BADNAME"), TimeSpan.FromSeconds(15), "the nickname filter poke");
});
await Test("temporary channel", async () =>
{
	await panel.SetPlugin(Bot, "g_server", true, new() { ["tempchannel"] = true, ["tempchannel.channel"] = "Create channel", ["tempchannel.name"] = "Temp {user}", ["tempchannel.cooldown"] = 0 });
	var guest = users.First(u => u.Name == "Guest");
	await Task.Delay(2000);
	await guest.Move((ChannelId)ulong.Parse((await q.ChannelId("Create channel"))!));
	await Until(async () => await q.ChannelName(await ChannelOf("Guest")) == "Temp Guest", TimeSpan.FromSeconds(15), "Guest was not moved to a 'Temp Guest' channel");
});
await Test("channel name info", async () =>
{
	await panel.SetPlugin(Bot, "g_server", true, new() { ["channelinfo"] = true, ["channelinfo.channel"] = "Info", ["channelinfo.template"] = "Online {online}", ["channelinfo.intervalSec"] = 10 });
	await Until(async () => (await q.Channels()).Any(c => c["channel_name"].StartsWith("Online ")), TimeSpan.FromSeconds(30), "the Info channel was not renamed");
});
await Test("AFK mover and move back", async () =>
{
	await panel.SetPlugin(Bot, "g_moderation", true, new() { ["afkmover"] = true, ["afkmover.channel"] = "AFK", ["afkmover.away"] = true, ["afkmover.moveBack"] = true, ["afkmover.idleMinutes"] = 0 });
	var afk = (await q.ChannelId("AFK"))!;
	await tester.SetAway(true);
	await Until(async () => await ChannelOf("Tester") == afk, TimeSpan.FromSeconds(75), "Tester was not moved to AFK");
	await tester.SetAway(false);
	await Until(async () => await ChannelOf("Tester") == lobby, TimeSpan.FromSeconds(75), "Tester was not moved back to Lobby");
});

// ------------------------------------------------------------------ music and panel
Console.WriteLine("\nMusic and panel");
await Test("library upload and playback", async () =>
{
	var up = await panel.Upload("library/upload", Wav(6), "it-tone.wav");
	var id = up!["id"]?.ToString() ?? up["Id"]!.ToString();
	await panel.Post($"bots/{Bot}/library/{id}", new { now = true });
	await Until(async () => (await panel.Get("bots"))!.ToJsonString().Contains("it-tone"), TimeSpan.FromSeconds(20), "the panel does not show the file playing");
	await panel.Post($"bots/{Bot}/control", new { action = "stop" }).ContinueWith(_ => { });
	await panel.Delete("library/" + id).ContinueWith(_ => { });
});
await Test("dashboard", async () =>
{
	var d = (await panel.Get($"dashboard?bot={Bot}"))!;
	Check(d.ToJsonString().Length > 20, "empty dashboard");
	var online = (await panel.Get($"online?bot={Bot}"))!.ToJsonString();
	Check(online.Contains("Tester"), "the online list does not contain Tester: " + online);
});
await Test("connection status", async () =>
{
	var c = (await panel.Get($"bots/{Bot}/connection"))!.ToJsonString();
	Check(c.Contains("ping", StringComparison.OrdinalIgnoreCase), "no ping in: " + c);
});
await Test("backup export, inspect and restore", async () =>
{
	var raw = (await panel.Send(HttpMethod.Get, "backup"))!.ToJsonString();
	Check(raw.Contains("plugins"), "the backup has no plugins section");
	await panel.Send(HttpMethod.Post, "backup/inspect", rawBody: raw);
	// the panel sends the restore request as text (the body can be large)
	await panel.Send(HttpMethod.Post, "backup/restore", rawBody: new JsonObject { ["data"] = raw, ["sections"] = new JsonArray("texts", "commands") }.ToJsonString());
});

// ------------------------------------------------------------------ the remaining plugins
Console.WriteLine("\nPreparing groups, channels, a radio stream and the timed features");
var guest = users.First(u => u.Name == "Guest");
var staffGid = await Group("IT Staff");
var memberGid = await Group("IT Member");
var rankGid = await Group("IT Rank");
try { await q.Cmd($"servergroupaddclient sgid={staffGid} cldbid={(await q.Client("Tester"))!["client_database_id"]}"); } catch (InvalidOperationException) { }
foreach (var ch in new[] { "Other", "Support", "Register", "Guarded" })
	if (await q.ChannelId(ch) is null) await q.CreateChannel(ch);
async Task<ChannelId> Cid(string name) => (ChannelId)ulong.Parse(await q.ChannelId(name) ?? throw new Exception("no channel " + name));
var lobbyId = await Cid("Lobby");
JsonNode? Playing(JsonNode bot) => bot["live"]?["playing"];
async Task Stop() { try { await panel.Post($"bots/{Bot}/control", new { action = "stop" }); } catch (PanelException) { } }

// a radio station served by this program (no internet needed)
using var stream = new StreamServer(int.Parse(Env("FRIX_STREAM_PORT", "18980")), Tone(300, 16000));
foreach (var r in (await panel.Get("radios"))!.AsArray())
	if (r!["name"]?.ToString() == "TestRadio") await panel.Delete("radios/" + r["id"]);
await panel.Post("radios", new { name = "TestRadio", url = $"http://{hostFromBot}:{stream.Port}/radio.wav" });

// scheduled playback: set the volume in the next two minutes (local and UTC time, whichever Frix uses)
var times = new[] { DateTime.Now, DateTime.UtcNow }.SelectMany(t => new[] { t.AddMinutes(1), t.AddMinutes(2) }).Select(t => t.ToString("HH:mm")).Distinct();
await panel.SetPlugin(Bot, "g_schedule", true, new() { ["scheduleplay.entries"] = string.Join("\n", times.Select(t => $"{t} | volume 37")) });

// activity ranks: give Tester two hours of online time through a stats backup, then a rank at one hour
await Test("stats restore (online time)", async () =>
{
	var testerUid = (await q.Client("Tester"))!["client_unique_identifier"];
	var stats = (await panel.Get("backup?sections=stats"))!;
	JsonObject? owner = null;
	void Find(JsonNode? n) { if (n is JsonObject o) { if (o.ContainsKey("online_time")) owner = o; else foreach (var (_, v) in o) Find(v); } }
	Find(stats);
	Check(owner != null, "the stats backup has no online_time table");
	owner!["online_time"] = new JsonArray(new JsonObject { ["uid"] = testerUid, ["name"] = "Tester", ["seconds"] = 7300, ["last_seen"] = null });
	await panel.Send(HttpMethod.Post, "backup/restore", rawBody: new JsonObject { ["data"] = stats.ToJsonString(), ["sections"] = new JsonArray("stats") }.ToJsonString());
	await panel.SetPlugin(Bot, "g_activity", true, new() { ["activity"] = true, ["ranks"] = true, ["ranks.ranks"] = $"1={rankGid}", ["ranks.removeLower"] = false, ["ranks.message"] = "RANK-UP {hours}" });
	var t = await Ask(tester, "!time", m => m.Contains("total time"));
	Check(t.Contains("2 h "), "online time not restored (or not in English): " + t);
});
await panel.SetPlugin(Bot, "g_activity", true, new() { ["activity"] = true, ["ranks"] = true, ["ranks.ranks"] = $"1={rankGid}", ["ranks.removeLower"] = false, ["ranks.message"] = "RANK-UP {hours}" });

Console.WriteLine("\nMusic and radio");
await panel.SetPlugin(Bot, "g_music", true, new() { ["npstatus"] = true, ["npstatus.nick"] = true, ["npstatus.nickFormat"] = "NP {title}", ["npstatus.desc"] = false });
await Test("radio (!radio)", async () =>
{
	await Ask(tester, "!radio TestRadio", seconds: 20);
	await Until(async () => Playing(await BotState())?["radio"]?.ToString() == "TestRadio", TimeSpan.FromSeconds(25),
		"the radio is not playing");
});
await Test("radio name as the bot nickname", async () =>
	await Until(async () => (await BotClient())["client_nickname"] == "NP TestRadio", TimeSpan.FromSeconds(15), "the nickname does not show the radio"));
await Test("bot avatar is uploaded", async () =>
	await Until(async () => (await q.Cmd($"clientinfo clid={(await BotClient())["clid"]}"))[0].GetValueOrDefault("client_flag_avatar", "") is { Length: > 0 },
		TimeSpan.FromSeconds(30), "the bot has no avatar"));
await Stop();

var longUp = await panel.Upload("library/upload", Tone(150, 16000), "it-long.wav");
await Test("!file plays from the library", async () =>
{
	await Ask(tester, "!file it-long", seconds: 20);
	await Until(async () => Playing(await BotState())?["title"]?.ToString().Contains("it-long") == true, TimeSpan.FromSeconds(20), "it-long is not playing");
});
await Test("song name as the bot nickname", async () =>
	await Until(async () => (await BotClient())["client_nickname"].StartsWith("NP it-long", StringComparison.Ordinal), TimeSpan.FromSeconds(15),
		"the nickname does not show the song"));
await Test("!fav and !favs", async () =>
{
	await panel.SetPlugin(Bot, "g_music", true, new() { ["favorites"] = true });
	var t = await Ask(tester, "!fav");
	Check(t.Contains("Nothing is playing that could be added"), "library files should not be favourites: " + t);
	t = await Ask(tester, "!favs list");
	Check(t.Contains("No favourites yet"), "unexpected answer: " + t);
});
await Test("vote skip", async () =>
{
	await panel.SetPlugin(Bot, "g_music", true, new() { ["voteskip"] = true, ["voteskip.percent"] = 100, ["voteskip.requesterSkips"] = true });
	try
	{
		await guest.Move(lobbyId);
		await Task.Delay(1500);
		var t = await Ask(guest, "!next");
		Check(t.Contains("1/2"), "expected a vote count 1/2, got: " + t);
		Check(Playing(await BotState())?["title"]?.ToString().Contains("it-long") == true, "the song was skipped without enough votes");
	}
	finally { await panel.SetPlugin(Bot, "g_music", true, new() { ["voteskip"] = false }); }
});
await Test("DJ mode", async () =>
{
	await panel.SetPlugin(Bot, "g_music", true, new() { ["djmode"] = true, ["djmode.djGroups"] = staffGid });
	try
	{
		var t = await Ask(guest, "!file it-long");
		Check(t.Contains("DJ"), "a non-DJ could queue a song: " + t);
	}
	finally { await panel.SetPlugin(Bot, "g_music", true, new() { ["djmode"] = false }); }
});
await Test("pause when alone and resume", async () =>
{
	await panel.SetPlugin(Bot, "g_music", true, new() { ["alonepause"] = true, ["alonepause.resume"] = true });
	try
	{
		var other = await Cid("Other");
		await tester.Move(other);
		await guest.Move(other);
		await Until(async () => Playing(await BotState())?["paused"]?.GetValue<bool>() == true, TimeSpan.FromSeconds(15), "the bot did not pause when it was alone");
		await tester.Move(lobbyId);
		await Until(async () => Playing(await BotState())?["paused"]?.GetValue<bool>() == false, TimeSpan.FromSeconds(15), "the bot did not continue");
	}
	finally { await panel.SetPlugin(Bot, "g_music", true, new() { ["alonepause"] = false }); }
});
await Stop();
await panel.Delete("library/" + (longUp!["id"] ?? longUp["Id"])).ContinueWith(_ => { });

Console.WriteLine("\nServer tools");
await Test("support notification", async () =>
{
	await panel.SetPlugin(Bot, "g_server", true, new() { ["supportnotify"] = true, ["supportnotify.channel"] = "Support", ["supportnotify.groups"] = staffGid,
		["supportnotify.staffMessage"] = "STAFF-ALERT {user}", ["supportnotify.cooldown"] = 0 });
	var since = DateTime.UtcNow;
	await guest.Move(await Cid("Support"));
	await tester.WaitFor(since, r => FromBot(r) && r.Text.Contains("STAFF-ALERT Guest"), TimeSpan.FromSeconds(15), "the staff alert");
	await guest.WaitFor(since, FromBot, TimeSpan.FromSeconds(10), "the message to the waiting user");
});
await Test("registration", async () =>
{
	await panel.SetPlugin(Bot, "g_server", true, new() { ["registration"] = true, ["registration.channel"] = "Register", ["registration.memberGroup"] = memberGid,
		["registration.staffGroups"] = staffGid, ["registration.staffMessage"] = "REG-WAIT {user}", ["registration.cooldown"] = 0 });
	var since = DateTime.UtcNow;
	await guest.Move(await Cid("Register"));
	await tester.WaitFor(since, r => FromBot(r) && r.Text.Contains("REG-WAIT Guest"), TimeSpan.FromSeconds(15), "the registration notice to staff");
	var t = await Ask(tester, "!register Guest");
	Check(t.Contains("registered"), "unexpected answer: " + t);
	await Until(async () => (await GroupsOf("Guest")).Contains(memberGid), TimeSpan.FromSeconds(10), "Guest did not get the member group");
});
await Test("channel guard", async () =>
{
	await panel.SetPlugin(Bot, "g_moderation", true, new() { ["channelguard"] = true, ["channelguard.channels"] = "Guarded", ["channelguard.action"] = "move",
		["channelguard.moveTo"] = "Lobby", ["channelguard.message"] = "GUARD-MSG" });
	try
	{
		var since = DateTime.UtcNow;
		await guest.Move(await Cid("Guarded"));
		await Until(async () => await ChannelOf("Guest") == lobbyId.Value.ToString(), TimeSpan.FromSeconds(15), "Guest was not moved out of the guarded channel");
		await guest.WaitFor(since, r => FromBot(r) && r.Text.Contains("GUARD-MSG"), TimeSpan.FromSeconds(10), "the channel guard message");
	}
	finally { await panel.SetPlugin(Bot, "g_moderation", true, new() { ["channelguard"] = false }); }
});
await Test("anti-flood warning", async () =>
{
	if (await ChannelOf("Guest") != await BotChannel()) await guest.Move((ChannelId)ulong.Parse(await BotChannel()));
	await panel.SetPlugin(Bot, "g_moderation", true, new() { ["antiflood"] = true, ["antiflood.maxMessages"] = 3, ["antiflood.seconds"] = 20,
		["antiflood.warnings"] = 3, ["antiflood.warnMessage"] = "FLOOD-WARN" });
	try
	{
		var since = DateTime.UtcNow;
		for (var i = 0; i < 6; i++) await guest.SendChannel("spam " + i);
		await guest.WaitFor(since, r => FromBot(r) && r.Text.Contains("FLOOD-WARN"), TimeSpan.FromSeconds(15), "the flood warning");
	}
	finally { await panel.SetPlugin(Bot, "g_moderation", true, new() { ["antiflood"] = false }); }
});
await Test("rotating announcements", async () =>
{
	var since = DateTime.UtcNow;
	await panel.SetPlugin(Bot, "g_welcome", true, new() { ["advertisement"] = true, ["advertisement.messages"] = "AD-TEST", ["advertisement.intervalSec"] = 30,
		["advertisement.target"] = "channel" });
	try { await tester.WaitFor(since, r => FromBot(r) && r.Text.Contains("AD-TEST"), TimeSpan.FromSeconds(80), "the announcement in the channel"); }
	finally { await panel.SetPlugin(Bot, "g_welcome", true, new() { ["advertisement"] = false }); }
});

Console.WriteLine("\nConnection");
await Test("auto reconnect after a kick", async () =>
{
	await panel.SetPlugin(Bot, "g_reconnect", true, new() { ["autoreconnect.onKick"] = true, ["autoreconnect.delay"] = 2, ["autoreconnect.sameChannel"] = true });
	var channelBefore = await BotChannel();
	await q.Cmd($"clientkick clid={(await BotClient())["clid"]} reasonid=5 reasonmsg=integration\\stest");
	await Until(async () => !(await q.Clients()).Any(c => c.GetValueOrDefault("client_unique_identifier") == botUid), TimeSpan.FromSeconds(10), "the kick did not work");
	await Until(async () => (await q.Clients()).Any(c => c.GetValueOrDefault("client_unique_identifier") == botUid), TimeSpan.FromSeconds(60), "the bot did not come back");
	await Until(async () => await BotChannel() == channelBefore, TimeSpan.FromSeconds(15), "the bot did not rejoin its channel");
});

Console.WriteLine("\nTimed features");
await Test("scheduled playback (volume)", async () =>
	await Until(async () => (await BotState())["live"]?["volume"]?.ToString() == "37", TimeSpan.FromSeconds(150), "the scheduled volume change did not happen"));
await Test("activity ranks", async () =>
{
	await Until(async () => (await GroupsOf("Tester")).Contains(rankGid), TimeSpan.FromSeconds(90), "Tester did not get the rank group");
	await tester.WaitFor(DateTime.MinValue, r => FromBot(r) && r.Text.Contains("RANK-UP 1"), TimeSpan.FromSeconds(10), "the rank-up message");
});
await Test("activity statistics", async () =>
	await Until(async () => (await panel.Get($"dashboard?bot={Bot}"))!["activity"]?["peakAt"] is not null, TimeSpan.FromSeconds(100), "no activity sample was recorded"));
await Test("idle radio", async () =>
{
	await Stop();
	await panel.SetPlugin(Bot, "g_radio", true, new() { ["idleradio"] = true, ["idleradio.radio"] = "TestRadio", ["idleradio.delay"] = 1 });
	try
	{
		await Until(async () => Playing(await BotState())?["radio"]?.ToString() == "TestRadio", TimeSpan.FromSeconds(110), "the idle radio did not start");
	}
	finally { await panel.SetPlugin(Bot, "g_radio", true, new() { ["idleradio"] = false }); await Stop(); }
});

// 16-bit mono sine tone at the given sample rate
static byte[] Tone(int seconds, int rate)
{
	var samples = rate * seconds;
	using var ms = new MemoryStream();
	using var w = new BinaryWriter(ms);
	w.Write("RIFF"u8); w.Write(36 + samples * 2); w.Write("WAVE"u8);
	w.Write("fmt "u8); w.Write(16); w.Write((short)1); w.Write((short)1); w.Write(rate); w.Write(rate * 2); w.Write((short)2); w.Write((short)16);
	w.Write("data"u8); w.Write(samples * 2);
	for (var i = 0; i < samples; i++) w.Write((short)(Math.Sin(2 * Math.PI * 440 * i / rate) * 8000));
	w.Flush();
	return ms.ToArray();
}

// ------------------------------------------------------------------ summary
foreach (var u in users) await u.DisposeAsync();
var failed = results.Where(r => !r.ok).ToList();
Console.WriteLine($"\n{results.Count - failed.Count}/{results.Count} passed");
foreach (var f in failed) Console.WriteLine($"  FAIL  {f.name}: {f.detail.Split('\n')[0]}");
return failed.Count == 0 ? 0 : 1;

// 16-bit mono 48 kHz sine tone
static byte[] Wav(int seconds)
{
	const int rate = 48000;
	var samples = rate * seconds;
	using var ms = new MemoryStream();
	using var w = new BinaryWriter(ms);
	w.Write("RIFF"u8); w.Write(36 + samples * 2); w.Write("WAVE"u8);
	w.Write("fmt "u8); w.Write(16); w.Write((short)1); w.Write((short)1); w.Write(rate); w.Write(rate * 2); w.Write((short)2); w.Write((short)16);
	w.Write("data"u8); w.Write(samples * 2);
	for (var i = 0; i < samples; i++) w.Write((short)(Math.Sin(2 * Math.PI * 440 * i / rate) * 8000));
	w.Flush();
	return ms.ToArray();
}
