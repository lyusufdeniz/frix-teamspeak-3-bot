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
		results.Add((name, false, ex.Message, sw.Elapsed.TotalSeconds));
		Console.WriteLine($"  FAIL  {name} ({sw.Elapsed.TotalSeconds:0.0}s)\n        {ex.Message.Replace("\n", "\n        ")}");
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

// Sends a command to the bot in private chat and waits for the answer.
async Task<string> Ask(TsUser u, string command, Func<string, bool>? match = null, int seconds = 15)
{
	var since = DateTime.UtcNow;
	await u.SendPrivate(await ClidOf(BotNick), command);
	var m = await u.WaitFor(since, r => r.From == BotNick && r.Kind != "Poke" && (match?.Invoke(r.Text) ?? true), TimeSpan.FromSeconds(seconds), $"an answer to '{command}'");
	return m.Text;
}

Console.WriteLine($"Frix integration tests\n  panel {panelUrl}\n  teamspeak {tsAddr} (bot connects to {botAddr})\n");

// ------------------------------------------------------------------ environment
Console.WriteLine("Setup");
await Until(async () => { await panel.Get("state"); return true; }, TimeSpan.FromMinutes(3), "the panel did not come up");
var state = await panel.Get("state");
if (state?["setupRequired"]?.GetValue<bool>() == true)
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
	// server admin, so it may move users, create channels and rename them
	try { await q.Cmd($"servergroupaddclient sgid=6 cldbid={bot!["client_database_id"]}"); } catch (InvalidOperationException) { }
	await Until(async () => (await panel.Get("bots"))!.AsArray().Any(b => b!["name"]!.ToString() == Bot && b["status"]!.ToString() == "connected"),
		TimeSpan.FromSeconds(20), "the panel does not show the bot as connected");
});
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
	await tester.SendPrivate(await ClidOf(BotNick), "!come");
	try { await Until(async () => await ChannelOf(BotNick) == lobby, TimeSpan.FromSeconds(10), "the bot did not come to Lobby"); }
	catch (TimeoutException ex)
	{
		var said = string.Join(" | ", tester.Since(since).Where(m => m.From == BotNick).Select(m => m.Text));
		throw new Exception(ex.Message + (said.Length > 0 ? ". The bot said: " + said : ""));
	}
});
await Test("poll in the channel with live percentages", async () =>
{
	var since = DateTime.UtcNow;
	await tester.SendChannel("!poll Best colour? | red | blue");
	await tester.WaitFor(since, r => r.From == BotNick && r.Text.Contains("Best colour"), TimeSpan.FromSeconds(10), "the poll in the channel");
	since = DateTime.UtcNow;
	await tester.SendChannel("!vote 1");
	var m = await tester.WaitFor(since, r => r.From == BotNick && r.Text.Contains('%'), TimeSpan.FromSeconds(10), "the poll percentages");
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
	await guest.WaitFor(since, r => r.From == BotNick && r.Text.Contains("WELCOME Guest"), TimeSpan.FromSeconds(15), "the welcome message");
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
	await Until(async () => await ChannelOf("Tester") == afk, TimeSpan.FromSeconds(45), "Tester was not moved to AFK");
	await tester.SetAway(false);
	await Until(async () => await ChannelOf("Tester") == lobby, TimeSpan.FromSeconds(45), "Tester was not moved back to Lobby");
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
