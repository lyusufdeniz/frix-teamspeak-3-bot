// Frix: kullanıcı scriptleri (JavaScript, Jint motoru).
// Scriptler uygulama klasöründeki scripts/ altında .js dosyalarıdır; panelden yazılır, açılıp kapatılır.
// Her bot her açık scripti kendi motorunda çalıştırır. Scriptler sadece "frix" nesnesini görür:
// dosya sistemine, ağa ya da .NET'e erişemez. Her çağrı süre, bellek ve adım sınırıyla çalışır.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Jint;
using Jint.Native;
using TSLib;

namespace TS3AudioBot.Frix;

public sealed record ScriptInfo(string Name, bool Enabled, long Size, string Modified, string? Error, string? ErrorAt);
public sealed record ScriptLogLine(string Time, string Script, string Bot, string Level, string Text);

/// <summary>Script dosyaları, açık/kapalı durumları, hatalar ve script logları.</summary>
public static class FrixScripts
{
	private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();
	public static string Dir => Path.Combine(AppContext.BaseDirectory, "scripts");
	private static readonly Regex NameRx = new("^[A-Za-z0-9_-]{1,40}$", RegexOptions.Compiled);
	private static readonly ConcurrentDictionary<string, (string at, string msg)> Errors = new();
	private static readonly ConcurrentQueue<ScriptLogLine> Logs = new();

	/// <summary>Scriptler değişince çalışan botlar yeniden yükler.</summary>
	public static event Action? Changed;

	public static bool ValidName(string name) => NameRx.IsMatch(name);
	private static string PathOf(string name) => ValidName(name) ? Path.Combine(Dir, name + ".js") : throw new ArgumentException("bad_script_name");
	private static bool IsEnabled(string name) => FrixDb.GetMeta("script.enabled:" + name) == "1";

	public static List<ScriptInfo> List()
	{
		if (!Directory.Exists(Dir)) return [];
		return Directory.EnumerateFiles(Dir, "*.js").Select(f =>
		{
			var n = Path.GetFileNameWithoutExtension(f);
			var fi = new FileInfo(f);
			Errors.TryGetValue(n, out var e);
			return new ScriptInfo(n, IsEnabled(n), fi.Length, fi.LastWriteTimeUtc.ToString("O"), e.msg, e.at);
		}).Where(s => ValidName(s.Name)).OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase).ToList();
	}

	public static string Read(string name) => File.ReadAllText(PathOf(name));

	public static IEnumerable<(string Name, string Code)> EnabledScripts()
		=> List().Where(s => s.Enabled).Select(s => (s.Name, SafeRead(s.Name))).Where(x => x.Item2 is not null)!;

	private static string? SafeRead(string name) { try { return Read(name); } catch { return null; } }

	public static void Save(string name, string code, bool? enabled = null)
	{
		if (code.Length > 200_000) throw new ArgumentException("script_too_big");
		Directory.CreateDirectory(Dir);
		File.WriteAllText(PathOf(name), code);
		if (enabled is { } e) FrixDb.SetMeta("script.enabled:" + name, e ? "1" : "0");
		Errors.TryRemove(name, out _);
		Changed?.Invoke();
	}

	public static void SetEnabled(string name, bool enabled)
	{
		if (!File.Exists(PathOf(name))) throw new FileNotFoundException();
		FrixDb.SetMeta("script.enabled:" + name, enabled ? "1" : "0");
		Errors.TryRemove(name, out _);
		Changed?.Invoke();
	}

	public static void Delete(string name)
	{
		File.Delete(PathOf(name));
		FrixDb.SetMeta("script.enabled:" + name, null);
		Errors.TryRemove(name, out _);
		Changed?.Invoke();
	}

	public static void ReportError(string script, string bot, string message)
	{
		Errors[script] = (DateTime.UtcNow.ToString("O"), message);
		AddLog(script, bot, "error", message);
		Log.Warn("Script hatası ({0}, {1}): {2}", script, bot, message);
	}

	public static void AddLog(string script, string bot, string level, string text)
	{
		if (text.Length > 1000) text = text[..1000];
		Logs.Enqueue(new ScriptLogLine(DateTime.UtcNow.ToString("O"), script, bot, level, text));
		while (Logs.Count > 300) Logs.TryDequeue(out _);
	}

	public static ScriptLogLine[] RecentLogs() => [.. Logs.Reverse()];
	public static void ClearLogs() { while (Logs.TryDequeue(out _)) { } }

	public const string Template = """
// Örnek script — "frix" nesnesiyle bota erişirsin. Ayrıntılar: Panel → Scriptler → API.

// !selam yazana cevap verir
frix.command('selam', (args, ctx) => {
  return 'Merhaba ' + ctx.user + '!' + (args ? ' (' + args + ')' : '');
});

// sunucuya bağlanan herkese özel mesaj
frix.on('connect', (e) => {
  frix.sendPrivate(e.id, 'Hoş geldin ' + e.user + '! Kanalda !selam yazabilirsin.');
});

// her şarkı başladığında kaç şarkı çalındığını say
frix.on('song', (e) => {
  const n = (frix.store.get('songs') || 0) + 1;
  frix.store.set('songs', n);
  frix.log('Çalınan şarkı sayısı: ' + n);
});
""";
}

/// <summary>Bir bot için açık scriptleri çalıştıran gizli eklenti (panelde görünmez, her zaman açık).</summary>
public sealed class ScriptsPlugin : FrixPlugin
{
	public override string Id => "scripts";
	public override bool DefaultEnabled => true;
	public override string NameTr => "Scriptler";
	public override string NameEn => "Scripts";
	public override string DescTr => "Panelden yazılan JavaScript scriptleri.";
	public override string DescEn => "JavaScript scripts written in the panel.";
	public override PluginField[] Schema => [];

	private sealed class Loaded
	{
		public required string Name;
		public required Engine Engine;
		public readonly Dictionary<string, List<JsValue>> Handlers = new(StringComparer.OrdinalIgnoreCase);
		public readonly Dictionary<string, JsValue> Commands = new(StringComparer.OrdinalIgnoreCase);
	}

	private List<Loaded> scripts = [];
	private bool dirty = true;
	private readonly Action onChanged;

	public ScriptsPlugin() { onChanged = () => dirty = true; }

	protected override void OnEnable() { FrixScripts.Changed += onChanged; dirty = true; }
	protected override void OnDisable() { FrixScripts.Changed -= onChanged; scripts = []; }

	private void EnsureLoaded()
	{
		if (!dirty) return;
		dirty = false;
		var list = new List<Loaded>();
		foreach (var (name, code) in FrixScripts.EnabledScripts())
		{
			var engine = new Engine(o => o
				.TimeoutInterval(TimeSpan.FromSeconds(2))
				.LimitMemory(32_000_000)
				.LimitRecursion(64)
				.MaxStatements(500_000)
				.Strict(false));
			var l = new Loaded { Name = name, Engine = engine };
			engine.SetValue("frix", new ScriptApi(this, l));
			try
			{
				engine.Execute(code, name + ".js");
				list.Add(l);
			}
			catch (Exception ex) { FrixScripts.ReportError(name, Ctx.BotName, Describe(ex)); }
		}
		scripts = list;
	}

	private static string Describe(Exception ex) => ex switch
	{
		Jint.Runtime.JavaScriptException js => js.Error.ToString() + (js.Location.Start.Line > 0 ? $" (satır {js.Location.Start.Line})" : ""),
		TimeoutException => "Zaman aşımı: script 2 saniyeden uzun sürdü.",
		Jint.Runtime.MemoryLimitExceededException => "Bellek sınırı aşıldı (32 MB).",
		Jint.Runtime.StatementsCountOverflowException => "Çok fazla adım (sonsuz döngü olabilir).",
		Jint.Runtime.RecursionDepthOverflowException => "Çok derin özyineleme.",
		_ when ex.GetType().Name.Contains("Parse") => "Sözdizimi hatası: " + ex.Message,
		_ => ex.Message,
	};

	private void Emit(string evt, object payload)
	{
		EnsureLoaded();
		foreach (var s in scripts)
		{
			if (!s.Handlers.TryGetValue(evt, out var fns)) continue;
			foreach (var fn in fns.ToArray())
			{
				try { s.Engine.Invoke(fn, JsValue.FromObject(s.Engine, payload)); }
				catch (Exception ex) { FrixScripts.ReportError(s.Name, Ctx.BotName, $"'{evt}': " + Describe(ex)); }
			}
		}
	}

	/// <summary>Sohbette "!ad ..." bir script komutuysa çalıştırır ve cevabı döner; değilse null.</summary>
	public string? TryCommand(string name, string args, ClientId id, Uid uid, string user, string target)
	{
		EnsureLoaded();
		foreach (var s in scripts)
		{
			if (!s.Commands.TryGetValue(name, out var fn)) continue;
			try
			{
				var ctx = new { id = id.Value, uid = uid.Value, user, target, channel = ChannelOf(id) };
				var r = s.Engine.Invoke(fn, args, JsValue.FromObject(s.Engine, ctx));
				return r.IsNull() || r.IsUndefined() ? "" : r.ToString();
			}
			catch (Exception ex)
			{
				FrixScripts.ReportError(s.Name, Ctx.BotName, $"!{name}: " + Describe(ex));
				return "";
			}
		}
		return null;
	}

	private string ChannelOf(ClientId id)
		=> Ctx.Full.Book.Clients.TryGetValue(id, out var c) && Ctx.Full.Book.Channels.TryGetValue(c.Channel, out var ch) ? ch.Name : "";

	public override Task OnClientConnected(ClientId id, string name, Uid uid) { Emit("connect", new { id = id.Value, uid = uid.Value, user = name }); return Task.CompletedTask; }
	public override Task OnClientEnteredChannel(ClientId id, string name, Uid uid, ChannelId channel)
	{
		var chName = Ctx.Full.Book.Channels.TryGetValue(channel, out var ch) ? ch.Name : "";
		Emit("channel", new { id = id.Value, uid = uid.Value, user = name, channelId = channel.Value, channel = chName });
		return Task.CompletedTask;
	}
	public override Task OnTextMessage(ClientId id, Uid uid, string name, string message) { Emit("message", new { id = id.Value, uid = uid.Value, user = name, text = message }); return Task.CompletedTask; }
	public override Task OnSongStarted()
	{
		var cur = Ctx.PlayManager.CurrentPlayData;
		Emit("song", new { title = cur?.ResourceData?.ResourceTitle ?? "", radio = cur?.ResourceData?.Get("radio") != null });
		return Task.CompletedTask;
	}
	public override Task OnTick(DateTime now) { Emit("tick", new { time = DateTime.Now.ToString("HH:mm:ss") }); return Task.CompletedTask; }

	/// <summary>Scriptlerin gördüğü "frix" nesnesi.</summary>
	private sealed class ScriptApi(ScriptsPlugin p, Loaded l)
	{
		private PluginContext C => p.Ctx;
		private void Fire(Func<Task> f) => _ = C.RunOnBot(async () => { try { await f(); } catch (Exception ex) { FrixScripts.ReportError(l.Name, C.BotName, ex.Message); } });

		public string botName => C.BotName;
		public string serverName => C.ServerName;
		public readonly ScriptStore store = new(l.Name, l.Engine);

		public void on(string evt, JsValue fn)
		{
			if (fn is not Jint.Native.Function.Function) throw new ArgumentException("frix.on: ikinci değer bir fonksiyon olmalı");
			if (!l.Handlers.TryGetValue(evt, out var list)) l.Handlers[evt] = list = [];
			list.Add(fn);
		}

		public void command(string name, JsValue fn)
		{
			name = name.Trim().TrimStart('!');
			if (!FrixScripts.ValidName(name)) throw new ArgumentException("frix.command: geçersiz komut adı: " + name);
			if (FrixCommands.Builtins.Any(b => b.name.Equals(name, StringComparison.OrdinalIgnoreCase)) || FrixCommands.Display(name) != name)
				throw new ArgumentException("frix.command: !" + name + " yerleşik bir komut");
			l.Commands[name] = fn;
		}

		public void sendChannel(string text) => Fire(() => C.Client.SendChannelMessage(FrixChat.Colorize(text)));
		public void sendServer(string text) => Fire(() => C.Client.SendServerMessage(FrixChat.Colorize(text)));
		public void sendPrivate(double clientId, string text) => Fire(() => C.Client.SendMessage(FrixChat.Colorize(text), (ClientId)(ushort)clientId));
		public void poke(double clientId, string text) => Fire(() => C.Client.SendPoke(text, (ClientId)(ushort)clientId));

		public object[] clients() => C.RealClients().Select(c => (object)new
		{
			id = c.Id.Value, uid = c.Uid?.Value ?? "", name = c.Name, channelId = c.Channel.Value,
			channel = C.Full.Book.Channels.TryGetValue(c.Channel, out var ch) ? ch.Name : "",
			away = c.AwayMessage is not null, groups = c.ServerGroups.Select(g => g.Value).ToArray(),
		}).ToArray();

		public void log(JsValue value) => FrixScripts.AddLog(l.Name, C.BotName, "info", value.ToString());
	}

	/// <summary>Script başına kalıcı küçük veri (meta tablosu, JSON).</summary>
	private sealed class ScriptStore(string script, Engine engine)
	{
		private string Key(string k) => "script.store:" + script + ":" + k;
		public object? get(string key)
		{
			var raw = FrixDb.GetMeta(Key(key));
			if (raw is null) return null;
			try { return System.Text.Json.JsonSerializer.Deserialize<object>(raw) switch { System.Text.Json.JsonElement e => ToPlain(e), var o => o }; }
			catch { return raw; }
		}
		public void set(string key, JsValue value)
		{
			var json = value.IsUndefined() || value.IsNull() ? null : new Jint.Native.Json.JsonSerializer(engine).Serialize(value).ToString();
			if (json is { Length: > 10_000 }) throw new ArgumentException("frix.store: değer çok büyük (en fazla 10 KB)");
			FrixDb.SetMeta(Key(key), json);
		}
		private static object? ToPlain(System.Text.Json.JsonElement e) => e.ValueKind switch
		{
			System.Text.Json.JsonValueKind.Number => e.GetDouble(),
			System.Text.Json.JsonValueKind.String => e.GetString(),
			System.Text.Json.JsonValueKind.True => true,
			System.Text.Json.JsonValueKind.False => false,
			System.Text.Json.JsonValueKind.Array => e.EnumerateArray().Select(ToPlain).ToArray(),
			System.Text.Json.JsonValueKind.Object => e.EnumerateObject().ToDictionary(x => x.Name, x => ToPlain(x.Value)),
			_ => null,
		};
	}
}
