using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Frix.IntegrationTests;

/// <summary>Serves a long WAV tone over plain HTTP so the bot can play it as a "radio station" without internet.</summary>
public sealed class StreamServer : IDisposable
{
	private readonly TcpListener listener;
	private readonly byte[] wav;
	private readonly CancellationTokenSource stop = new();
	public int Port { get; }

	public StreamServer(int port, byte[] wav)
	{
		this.wav = wav;
		Port = port;
		listener = new TcpListener(IPAddress.Any, port);
		listener.Start();
		_ = Loop();
	}

	private async Task Loop()
	{
		while (!stop.IsCancellationRequested)
		{
			TcpClient c;
			try { c = await listener.AcceptTcpClientAsync(stop.Token); }
			catch { return; }
			_ = Serve(c);
		}
	}

	private async Task Serve(TcpClient c)
	{
		using (c)
		{
			try
			{
				var s = c.GetStream();
				var reader = new StreamReader(s, Encoding.ASCII);
				var first = await reader.ReadLineAsync() ?? "";
				while (!string.IsNullOrEmpty(await reader.ReadLineAsync())) { }
				var head = first.StartsWith("HEAD ", StringComparison.Ordinal);
				var header = Encoding.ASCII.GetBytes(
					$"HTTP/1.1 200 OK\r\nContent-Type: audio/wav\r\nContent-Length: {wav.Length}\r\nConnection: close\r\n\r\n");
				await s.WriteAsync(header);
				if (!head) await s.WriteAsync(wav);
			}
			catch { /* client went away */ }
		}
	}

	public void Dispose() { stop.Cancel(); listener.Stop(); }
}
