using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;

namespace Frix.IntegrationTests;

/// <summary>Talks to the Frix web panel API like the browser does (session cookie + X-Panel header).</summary>
public sealed class Panel(string baseUrl)
{
	private readonly HttpClient http = new(new HttpClientHandler { CookieContainer = new CookieContainer() })
	{
		BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/panel/api/"),
		Timeout = TimeSpan.FromSeconds(60),
	};

	public async Task<JsonNode?> Send(HttpMethod method, string path, object? body = null, string? rawBody = null, IDictionary<string, string>? headers = null)
	{
		using var req = new HttpRequestMessage(method, path.TrimStart('/'));
		req.Headers.Add("X-Panel", "1");
		if (headers != null) foreach (var (k, v) in headers) req.Headers.TryAddWithoutValidation(k, v);
		if (rawBody != null) req.Content = new StringContent(rawBody, Encoding.UTF8, "text/plain");
		else if (body != null)
		{
			var json = body is JsonNode n ? n.ToJsonString() : System.Text.Json.JsonSerializer.Serialize(body);
			req.Content = new StringContent(json, Encoding.UTF8, "application/json");
		}
		using var res = await http.SendAsync(req);
		var text = await res.Content.ReadAsStringAsync();
		if (!res.IsSuccessStatusCode)
			throw new PanelException((int)res.StatusCode, $"{method} {path} -> {(int)res.StatusCode} {text}");
		if (text.Length == 0) return null;
		try { return JsonNode.Parse(text); } catch { return JsonValue.Create(text); }
	}

	public Task<JsonNode?> Get(string path) => Send(HttpMethod.Get, path);
	public Task<JsonNode?> Post(string path, object? body = null) => Send(HttpMethod.Post, path, body ?? new { });
	public Task<JsonNode?> Put(string path, object? body = null) => Send(HttpMethod.Put, path, body ?? new { });
	public Task<JsonNode?> Delete(string path) => Send(HttpMethod.Delete, path);

	public async Task<JsonNode?> Upload(string path, byte[] data, string fileName)
	{
		using var req = new HttpRequestMessage(HttpMethod.Post, path.TrimStart('/'));
		req.Headers.Add("X-Panel", "1");
		req.Headers.Add("X-File-Name", Uri.EscapeDataString(fileName));
		req.Content = new ByteArrayContent(data);
		req.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
		using var res = await http.SendAsync(req);
		var text = await res.Content.ReadAsStringAsync();
		if (!res.IsSuccessStatusCode) throw new PanelException((int)res.StatusCode, $"upload -> {(int)res.StatusCode} {text}");
		return JsonNode.Parse(text);
	}

	/// <summary>Turns a plugin group on and sets the given config keys ("part" = on/off, "part.field" = value).</summary>
	public async Task SetPlugin(string bot, string group, bool enabled, Dictionary<string, object?> config)
	{
		var list = (await Get("plugins/" + Uri.EscapeDataString(bot)))!.AsArray();
		var g = list.First(x => x!["id"]!.ToString() == group)!;
		var cfg = g["config"]?.DeepClone() as JsonObject ?? [];
		foreach (var (k, v) in config) cfg[k] = v is null ? null : JsonValue.Create(v);
		await Put($"plugins/{Uri.EscapeDataString(bot)}/{group}", new JsonObject { ["enabled"] = enabled, ["config"] = cfg });
	}
}

public sealed class PanelException(int status, string message) : Exception(message)
{
	public int Status { get; } = status;
}
