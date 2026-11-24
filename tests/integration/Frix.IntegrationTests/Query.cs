using System.Net.Sockets;
using System.Text;

namespace Frix.IntegrationTests;

/// <summary>Minimal raw ServerQuery client, used as the server admin (channels, groups, checks).</summary>
public sealed class Query : IDisposable
{
	private readonly TcpClient tcp = new();
	private StreamReader reader = null!;
	private StreamWriter writer = null!;
	private readonly SemaphoreSlim gate = new(1, 1);

	public async Task Connect(string host, int port, string user, string pass)
	{
		await tcp.ConnectAsync(host, port);
		var s = tcp.GetStream();
		reader = new StreamReader(s, new UTF8Encoding(false));
		writer = new StreamWriter(s, new UTF8Encoding(false)) { NewLine = "\n", AutoFlush = true };
		await reader.ReadLineAsync(); // TS3
		await reader.ReadLineAsync(); // welcome text
		await Cmd($"login {Esc(user)} {Esc(pass)}");
		await Cmd("use 1");
		await Cmd("clientupdate client_nickname=TestAdmin");
	}

	/// <summary>Runs a command and returns its rows (key → value). Throws on a query error.</summary>
	public async Task<List<Dictionary<string, string>>> Cmd(string command)
	{
		// the server allows about 10 query commands per 3 seconds from non-allowlisted addresses
		for (var attempt = 0; ; attempt++)
		{
			try { return await CmdOnce(command); }
			catch (InvalidOperationException ex) when (ex.Message.Contains("(id 524)") && attempt < 5) { await Task.Delay(3000); }
		}
	}

	private DateTime lastCommand = DateTime.MinValue;

	private async Task<List<Dictionary<string, string>>> CmdOnce(string command)
	{
		await gate.WaitAsync();
		try
		{
			var wait = lastCommand.AddMilliseconds(350) - DateTime.UtcNow;
			if (wait > TimeSpan.Zero) await Task.Delay(wait);
			lastCommand = DateTime.UtcNow;
			await writer.WriteLineAsync(command);
			var rows = new List<Dictionary<string, string>>();
			while (true)
			{
				var line = await reader.ReadLineAsync() ?? throw new IOException("query closed");
				line = line.TrimEnd('\r');
				if (line.Length == 0 || line.StartsWith("notify", StringComparison.Ordinal)) continue;
				if (line.StartsWith("error ", StringComparison.Ordinal))
				{
					var e = Parse(line[6..])[0];
					if (e["id"] != "0") throw new InvalidOperationException($"query '{command}': {e.GetValueOrDefault("msg")} (id {e["id"]})");
					return rows;
				}
				rows.AddRange(Parse(line));
			}
		}
		finally { gate.Release(); }
	}

	private static List<Dictionary<string, string>> Parse(string line)
		=> line.Split('|').Select(item => item.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(kv =>
		{
			var i = kv.IndexOf('=');
			return i < 0 ? (kv, "") : (kv[..i], Unesc(kv[(i + 1)..]));
		}).GroupBy(x => x.Item1).ToDictionary(g => g.Key, g => g.First().Item2)).ToList();

	public static string Esc(string s) => s.Replace("\\", "\\\\").Replace("/", "\\/").Replace(" ", "\\s").Replace("|", "\\p").Replace("\n", "\\n");
	private static string Unesc(string s) => s.Replace("\\s", " ").Replace("\\p", "|").Replace("\\/", "/").Replace("\\n", "\n").Replace("\\\\", "\\");

	public async Task<List<Dictionary<string, string>>> Clients() => await Cmd("clientlist -uid -away -groups");
	public async Task<Dictionary<string, string>?> Client(string nick) => (await Clients()).FirstOrDefault(c => c.GetValueOrDefault("client_nickname") == nick);
	public async Task<List<Dictionary<string, string>>> Channels() => await Cmd("channellist");
	public async Task<string?> ChannelId(string name) => (await Channels()).FirstOrDefault(c => c["channel_name"] == name)?["cid"];
	public async Task<string> ChannelName(string cid) => (await Channels()).First(c => c["cid"] == cid)["channel_name"];

	public async Task<string> CreateChannel(string name, bool permanent = true)
		=> (await Cmd($"channelcreate channel_name={Esc(name)}" + (permanent ? " channel_flag_permanent=1" : "")))[0]["cid"];

	public void Dispose() { try { writer?.WriteLine("quit"); } catch { } tcp.Dispose(); }
}
