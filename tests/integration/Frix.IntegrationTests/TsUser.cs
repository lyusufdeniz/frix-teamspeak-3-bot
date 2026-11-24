using System.Collections.Concurrent;
using TSLib;
using TSLib.Commands;
using TSLib.Full;
using TSLib.Helper;
using TSLib.Messages;
using TSLib.Scheduler;

namespace Frix.IntegrationTests;

/// <summary>A real TeamSpeak client acting as a user: sends chat, moves, sets away and records what it receives.</summary>
public sealed class TsUser : IAsyncDisposable
{
	public sealed record Received(DateTime At, string Kind, string From, string Text, string Uid);

	private static int nextId = 100;
	private readonly DedicatedTaskScheduler scheduler;
	private readonly TsFullClient client;
	private readonly ConcurrentQueue<Received> inbox = new();
	public string Name { get; private set; }
	public ClientId Id => client.ClientId;

	public TsUser(string name)
	{
		Name = name;
		scheduler = new DedicatedTaskScheduler(new Id(Interlocked.Increment(ref nextId)));
		client = new TsFullClient(scheduler);
		client.OnEachTextMessage += (_, m) =>
		{
			if (m.InvokerId == client.ClientId) return;
			inbox.Enqueue(new Received(DateTime.UtcNow, m.Target.ToString(), m.InvokerName ?? "", m.Message ?? "", m.InvokerUid?.Value ?? ""));
		};
		client.OnEachClientPoke += (_, p) => inbox.Enqueue(new Received(DateTime.UtcNow, "Poke", p.InvokerName ?? "", p.Message ?? "", p.InvokerUid?.Value ?? ""));
	}

	public async Task Connect(string address)
	{
		var identity = IdentityData.GenerateNewIdentity(8);
		var data = new ConnectionDataFull(address, identity, versionSign: TsVersionSigned.VER_WIN_3_X_X, username: Name);
		var r = await scheduler.InvokeAsync(async () => await client.Connect(data));
		if (!r.Ok) throw new InvalidOperationException($"{Name} could not connect: {r.Error.ErrorFormat()}");
	}

	private DateTime lastCommand = DateTime.MinValue;

	// TeamSpeak's anti-flood: commands are spaced out and retried after a pause when the server complains
	private async Task Run(Func<Task<E<CommandError>>> action, string what)
	{
		for (var attempt = 0; ; attempt++)
		{
			var wait = lastCommand.AddMilliseconds(700) - DateTime.UtcNow;
			if (wait > TimeSpan.Zero) await Task.Delay(wait);
			lastCommand = DateTime.UtcNow;
			var r = await scheduler.InvokeAsync(action);
			if (r.Ok) return;
			if (r.Error.Id == TsErrorCode.client_is_flooding && attempt < 6) { await Task.Delay(3000); continue; }
			throw new InvalidOperationException($"{Name}: {what} failed: {r.Error.ErrorFormat()}");
		}
	}

	public Task SendPrivate(ClientId to, string text) => Run(() => client.SendPrivateMessage(text, to), "private message");
	public Task SendChannel(string text) => Run(() => client.SendChannelMessage(text), "channel message");
	public Task Move(ChannelId channel) => Run(() => client.ClientMove(client.ClientId, channel), "move");
	public async Task Rename(string name) { await Run(() => client.ChangeName(name), "rename"); Name = name; }
	public Task SetAway(bool away) => Run(() => client.SendVoid(new TsCommand("clientupdate") { { "client_away", away } }), "away");

	/// <summary>Messages and pokes received since <paramref name="since"/>.</summary>
	public IEnumerable<Received> Since(DateTime since) => inbox.Where(m => m.At >= since);

	/// <summary>Waits for a received message or poke that matches.</summary>
	public async Task<Received> WaitFor(DateTime since, Func<Received, bool> match, TimeSpan timeout, string what)
	{
		var end = DateTime.UtcNow + timeout;
		while (DateTime.UtcNow < end)
		{
			var hit = Since(since).FirstOrDefault(match);
			if (hit != null) return hit;
			await Task.Delay(150);
		}
		var got = string.Join(" | ", Since(since).Select(m => $"[{m.Kind}] {m.From}: {m.Text}"));
		throw new TimeoutException($"{Name} did not receive {what} in {timeout.TotalSeconds:0}s. Received: {(got.Length == 0 ? "(nothing)" : got)}");
	}

	public async ValueTask DisposeAsync()
	{
		try { await scheduler.InvokeAsync(() => client.Disconnect()); } catch { }
		try { client.Dispose(); } catch { }
		try { scheduler.Dispose(); } catch { }
	}
}
