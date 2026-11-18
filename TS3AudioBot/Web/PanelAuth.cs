// Frix web panel: kimlik doğrulama, oturumlar, roller ve ilk kurulum (SQLite).

using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using TS3AudioBot.Frix;

namespace TS3AudioBot.Web;

public sealed record PanelUser(long Id, string Name, string Role);

public static class PanelAuth
{
	private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();

	public const string UserItemKey = "PanelUser";
	private const string CookieName = "frix_session";
	private const string LegacyUsersFile = "panel_users.json";
	private const string SetupCodeFile = "KURULUM_KODU.txt";
	private const int Iterations = 210_000;
	private const int MaxFailures = 5;
	private static readonly TimeSpan SessionLifetime = TimeSpan.FromDays(14);
	private static readonly TimeSpan LockoutTime = TimeSpan.FromMinutes(10);

	public static readonly string[] Roles = ["admin", "dj", "viewer"];
	public static int RoleRank(string role) => role switch { "admin" => 3, "dj" => 2, "viewer" => 1, _ => 0 };

	private static readonly ConcurrentDictionary<string, Failure> Failures = new();
	private static string? setupCode;
	private sealed class Failure { public int Count; public DateTime Since; public DateTime LockedUntil; }

	public static readonly JsonSerializerOptions Json = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		PropertyNameCaseInsensitive = true,
	};

	// ---------------------------------------------------------------- başlatma

	public static void Initialize()
	{
		FrixDb.Initialize();
		MigrateLegacyUsers();
		FrixDb.Exec("DELETE FROM sessions WHERE expires < $n", ("$n", DateTime.UtcNow.ToString("O")));

		if (UserCount() == 0)
		{
			setupCode = File.Exists(SetupCodeFile) ? File.ReadAllText(SetupCodeFile).Trim() : null;
			if (string.IsNullOrEmpty(setupCode) || setupCode.Length != 8)
			{
				setupCode = RandomCode(8);
				File.WriteAllText(SetupCodeFile, setupCode);
			}
			Log.Warn("Panel henüz kurulmadı. Tarayıcıdan paneli açıp yönetici hesabını oluşturun. " +
				"Uzaktan kurulum için kod: {0} (ayrıca {1} dosyasında)", setupCode, SetupCodeFile);
		}
		else
		{
			try { if (File.Exists(SetupCodeFile)) File.Delete(SetupCodeFile); } catch { }
		}
	}

	private static long UserCount() => (long)(FrixDb.Scalar("SELECT COUNT(*) FROM users") ?? 0L);

	private static void MigrateLegacyUsers()
	{
		try
		{
			if (!File.Exists(LegacyUsersFile) || UserCount() > 0)
				return;
			var doc = JsonNode.Parse(File.ReadAllText(LegacyUsersFile));
			foreach (var u in doc?["Users"]?.AsArray() ?? [])
			{
				var name = u?["Name"]?.GetValue<string>();
				var salt = u?["Salt"]?.GetValue<string>();
				var hash = u?["Hash"]?.GetValue<string>();
				var iter = u?["Iterations"]?.GetValue<int>() ?? Iterations;
				if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(salt) || string.IsNullOrEmpty(hash))
					continue;
				FrixDb.Exec("INSERT OR IGNORE INTO users(name,salt,hash,iterations,role,created) VALUES($n,$s,$h,$i,'admin',$c)",
					("$n", name), ("$s", salt), ("$h", hash), ("$i", iter), ("$c", DateTime.UtcNow.ToString("O")));
			}
			File.Move(LegacyUsersFile, LegacyUsersFile + ".tasindi", true);
			foreach (var f in new[] { "panel_sessions.json", "PANEL_SIFRESI.txt" })
				try { if (File.Exists(f)) File.Delete(f); } catch { }
			Log.Info("Eski panel kullanıcıları veritabanına taşındı.");
		}
		catch (Exception ex)
		{
			Log.Warn(ex, "Eski panel kullanıcıları taşınamadı");
		}
	}

	// ---------------------------------------------------------------- ara katman

	/// <summary>true dönerse istek devam eder (statik dosya veya yetkili komut API isteği).</summary>
	public static async Task<bool> Handle(HttpContext ctx)
	{
		var path = ctx.Request.Path.Value ?? "/";
		var h = ctx.Response.Headers;
		h["X-Frame-Options"] = "DENY";
		h["X-Content-Type-Options"] = "nosniff";
		h["Referrer-Policy"] = "same-origin";
		h["Content-Security-Policy"] = "default-src 'self'; img-src 'self' data: https://i.ytimg.com; style-src 'self' 'unsafe-inline'; script-src 'self'; connect-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";

		if (path.StartsWith("/panel/api/", StringComparison.OrdinalIgnoreCase))
		{
			await PanelApi.Handle(ctx, path["/panel/api/".Length..]);
			return false;
		}

		// Eski komut API'si: sadece yönetici oturumu ya da TeamSpeak'ten alınan API anahtarı ile
		if (path.StartsWith("/api", StringComparison.OrdinalIgnoreCase))
		{
			var user = GetUser(ctx);
			if (user != null)
			{
				if (user.Role != "admin" || ctx.Request.Headers["X-Panel"] != "1")
				{
					await WriteJson(ctx, 403, new { error = "forbidden" });
					return false;
				}
				return true;
			}
			if (ctx.Request.Headers.ContainsKey("Authorization"))
				return true;
			await WriteJson(ctx, 401, new { error = "unauthorized" });
			return false;
		}

		// SPA: uzantısız yollar index.html'e döner (istemci tarafı yönlendirme)
		if (!Path.HasExtension(path))
			ctx.Request.Path = "/index.html";
		// Panel dosyaları her açılışta doğrulansın (güncellemeden sonra eski JS/CSS görünmesin)
		h["Cache-Control"] = "no-cache";
		return true;
	}

	// ---------------------------------------------------------------- oturum / kullanıcı

	public static PanelUser? GetUser(HttpContext ctx)
	{
		if (ctx.Items.TryGetValue(UserItemKey, out var cached) && cached is PanelUser pu)
			return pu;
		if (!ctx.Request.Cookies.TryGetValue(CookieName, out var token) || string.IsNullOrEmpty(token) || token.Length > 200)
			return null;
		var rows = FrixDb.Query("SELECT u.id, u.name, u.role, s.expires FROM sessions s JOIN users u ON u.id = s.user_id WHERE s.key = $k",
			("$k", Key(token)));
		if (rows.Count == 0)
			return null;
		var r = rows[0];
		if (DateTime.Parse((string)r["expires"]!, null, System.Globalization.DateTimeStyles.RoundtripKind) < DateTime.UtcNow)
		{
			FrixDb.Exec("DELETE FROM sessions WHERE key=$k", ("$k", Key(token)));
			return null;
		}
		var user = new PanelUser((long)r["id"]!, (string)r["name"]!, (string)r["role"]!);
		ctx.Items[UserItemKey] = user;
		return user;
	}

	/// <summary>Rol yetersizse yanıtı yazar ve null döner. GET dışındaki isteklerde X-Panel başlığı şarttır (CSRF).</summary>
	public static async Task<PanelUser?> Require(HttpContext ctx, string minRole)
	{
		var user = GetUser(ctx);
		if (user is null)
		{
			await WriteJson(ctx, 401, new { error = "unauthorized" });
			return null;
		}
		if (RoleRank(user.Role) < RoleRank(minRole))
		{
			await WriteJson(ctx, 403, new { error = "forbidden" });
			return null;
		}
		if (ctx.Request.Method != "GET" && ctx.Request.Headers["X-Panel"] != "1")
		{
			await WriteJson(ctx, 403, new { error = "csrf" });
			return null;
		}
		return user;
	}

	public static object State(HttpContext ctx)
	{
		var user = GetUser(ctx);
		var setup = UserCount() == 0;
		return new
		{
			setupRequired = setup,
			setupNeedsCode = setup && !IsLocal(ctx),
			user = user is null ? null : new { user.Name, user.Role },
			lang = FrixText.Language,
			version = FrixUpdater.CurrentVersion,
		};
	}

	public static async Task Setup(HttpContext ctx)
	{
		if (ctx.Request.Headers["X-Panel"] != "1") { await WriteJson(ctx, 403, new { error = "csrf" }); return; }
		if (UserCount() > 0) { await WriteJson(ctx, 409, new { error = "already_setup" }); return; }
		var body = await ReadJson(ctx);
		var name = body.Str("user")?.Trim() ?? "";
		var pass = body.Str("pass") ?? "";
		var code = (body.Str("code") ?? "").Trim().ToUpperInvariant();
		var lang = body.Str("lang");

		if (!IsLocal(ctx) && (setupCode is null || code.Length != setupCode.Length
			|| !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(code), Encoding.UTF8.GetBytes(setupCode))))
		{
			await Task.Delay(800);
			await WriteJson(ctx, 403, new { error = "bad_setup_code" });
			return;
		}
		if (ValidateName(name) is { } nameErr) { await WriteJson(ctx, 400, new { error = nameErr }); return; }
		if (ValidatePassword(pass) is { } passErr) { await WriteJson(ctx, 400, new { error = passErr }); return; }

		CreateUser(name, pass, "admin");
		if (lang != null && FrixText.Languages.Contains(lang))
		{
			FrixText.SetLanguage(lang);
			FrixCommands.ApplyLanguageDefaults(lang);
			FrixAvatar.Invalidate();
		}
		try { if (File.Exists(SetupCodeFile)) File.Delete(SetupCodeFile); } catch { }
		setupCode = null;
		FrixDb.Audit(name, Ip(ctx), "setup", "Yönetici hesabı oluşturuldu");
		Log.Info("Panel kurulumu tamamlandı. Yönetici: {0}", name);
		StartSession(ctx, name);
		await WriteJson(ctx, 200, new { ok = true });
	}

	public static async Task Login(HttpContext ctx)
	{
		var ip = Ip(ctx);
		var now = DateTime.UtcNow;
		var failure = Failures.GetOrAdd(ip, _ => new Failure());
		DateTime lockedUntil;
		lock (failure) lockedUntil = failure.LockedUntil;
		if (lockedUntil > now)
		{
			await WriteJson(ctx, 429, new { error = "locked", minutes = Math.Ceiling((lockedUntil - now).TotalMinutes) });
			return;
		}

		var body = await ReadJson(ctx);
		var name = body.Str("user")?.Trim() ?? "";
		var pass = body.Str("pass") ?? "";

		var row = FrixDb.Query("SELECT name, salt, hash, iterations FROM users WHERE name=$n", ("$n", name)).FirstOrDefault();
		bool ok;
		if (row is null)
		{
			Verify(DummySalt, DummyHash, Iterations, pass); // zamanlama farkı olmasın
			ok = false;
		}
		else
		{
			ok = Verify((string)row["salt"]!, (string)row["hash"]!, (int)(long)row["iterations"]!, pass);
		}

		if (!ok)
		{
			lock (failure)
			{
				if (now - failure.Since > LockoutTime) { failure.Count = 0; failure.Since = now; }
				if (++failure.Count >= MaxFailures)
				{
					failure.LockedUntil = now + LockoutTime;
					failure.Count = 0;
					Log.Warn("Web panel: {0} adresinden çok fazla hatalı giriş, 10 dakika kilitlendi.", ip);
				}
			}
			FrixDb.Audit(name, ip, "login_failed");
			await Task.Delay(400);
			await WriteJson(ctx, 401, new { error = "bad_credentials" });
			return;
		}

		Failures.TryRemove(ip, out _);
		var realName = (string)row!["name"]!;
		StartSession(ctx, realName);
		FrixDb.Exec("UPDATE users SET last_login=$t WHERE name=$n", ("$t", now.ToString("O")), ("$n", realName));
		FrixDb.Audit(realName, ip, "login");
		Log.Info("Web panel: {0} giriş yaptı ({1})", realName, ip);
		await WriteJson(ctx, 200, new { ok = true });
	}

	public static async Task Logout(HttpContext ctx)
	{
		if (ctx.Request.Cookies.TryGetValue(CookieName, out var token))
			FrixDb.Exec("DELETE FROM sessions WHERE key=$k", ("$k", Key(token)));
		ctx.Response.Cookies.Delete(CookieName);
		await WriteJson(ctx, 200, new { ok = true });
	}

	private static void StartSession(HttpContext ctx, string name)
	{
		var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
		var id = (long)FrixDb.Scalar("SELECT id FROM users WHERE name=$n", ("$n", name))!;
		var agent = ctx.Request.Headers["User-Agent"].ToString();
		FrixDb.Exec("INSERT INTO sessions(key,user_id,expires,created,ip,agent) VALUES($k,$u,$e,$c,$i,$a)",
			("$k", Key(token)), ("$u", (object)id), ("$e", (DateTime.UtcNow + SessionLifetime).ToString("O")),
			("$c", DateTime.UtcNow.ToString("O")), ("$i", Ip(ctx)), ("$a", agent.Length > 200 ? agent[..200] : agent));
		ctx.Response.Cookies.Append(CookieName, token, new CookieOptions
		{
			HttpOnly = true,
			SameSite = SameSiteMode.Strict,
			Secure = ctx.Request.IsHttps,
			Path = "/",
			MaxAge = SessionLifetime,
		});
	}

	// ---------------------------------------------------------------- kullanıcı yönetimi

	public static string? ValidateName(string name)
		=> name.Length is < 3 or > 32 || !name.All(c => char.IsLetterOrDigit(c) || c is '_' or '-' or '.') ? "bad_username" : null;

	public static string? ValidatePassword(string pass) => pass.Length is < 8 or > 200 ? "weak_password" : null;

	public static void CreateUser(string name, string pass, string role)
	{
		if (!Roles.Contains(role)) throw new ArgumentException("bad_role");
		var (salt, hash) = HashPassword(pass);
		FrixDb.Exec("INSERT INTO users(name,salt,hash,iterations,role,created) VALUES($n,$s,$h,$i,$r,$c)",
			("$n", name), ("$s", salt), ("$h", hash), ("$i", Iterations), ("$r", role), ("$c", DateTime.UtcNow.ToString("O")));
	}

	public static void SetPassword(long userId, string pass, string? keepSessionToken)
	{
		var (salt, hash) = HashPassword(pass);
		FrixDb.Exec("UPDATE users SET salt=$s, hash=$h, iterations=$i WHERE id=$id",
			("$s", salt), ("$h", hash), ("$i", Iterations), ("$id", userId));
		FrixDb.Exec("DELETE FROM sessions WHERE user_id=$id AND key <> $k",
			("$id", userId), ("$k", keepSessionToken is null ? "" : Key(keepSessionToken)));
	}

	public static bool CheckPassword(long userId, string pass)
	{
		var row = FrixDb.Query("SELECT salt, hash, iterations FROM users WHERE id=$id", ("$id", userId)).FirstOrDefault();
		return row != null && Verify((string)row["salt"]!, (string)row["hash"]!, (int)(long)row["iterations"]!, pass);
	}

	public static string? CurrentToken(HttpContext ctx) => ctx.Request.Cookies.TryGetValue(CookieName, out var t) ? t : null;

	public static long AdminCount() => (long)(FrixDb.Scalar("SELECT COUNT(*) FROM users WHERE role='admin'") ?? 0L);

	// ---------------------------------------------------------------- kripto

	private static readonly string DummySalt = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
	private static readonly string DummyHash = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

	private static (string salt, string hash) HashPassword(string password)
	{
		var salt = RandomNumberGenerator.GetBytes(16);
		var hash = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, Iterations, HashAlgorithmName.SHA256, 32);
		return (Convert.ToBase64String(salt), Convert.ToBase64String(hash));
	}

	private static bool Verify(string salt, string hash, int iterations, string password)
	{
		try
		{
			var expected = Convert.FromBase64String(hash);
			var actual = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), Convert.FromBase64String(salt),
				Math.Max(iterations, 10_000), HashAlgorithmName.SHA256, expected.Length);
			return CryptographicOperations.FixedTimeEquals(actual, expected);
		}
		catch { return false; }
	}

	private static string Key(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

	private static string RandomCode(int len)
	{
		const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
		return new string(Enumerable.Range(0, len).Select(_ => chars[RandomNumberGenerator.GetInt32(chars.Length)]).ToArray());
	}

	// ---------------------------------------------------------------- HTTP yardımcıları

	public static string Ip(HttpContext ctx) => ctx.Connection.RemoteIpAddress?.ToString() ?? "?";

	public static bool IsLocal(HttpContext ctx)
	{
		var ip = ctx.Connection.RemoteIpAddress;
		return ip != null && IPAddress.IsLoopback(ip)
			&& !ctx.Request.Headers.ContainsKey("X-Forwarded-For") && !ctx.Request.Headers.ContainsKey("X-Real-IP");
	}

	public static async Task<JsonObject> ReadJson(HttpContext ctx)
	{
		try
		{
			using var reader = new StreamReader(ctx.Request.Body, Encoding.UTF8);
			var text = await reader.ReadToEndAsync();
			if (text.Length == 0 || text.Length > 1_000_000)
				return [];
			return JsonNode.Parse(text) as JsonObject ?? [];
		}
		catch { return []; }
	}

	public static async Task<string> ReadText(HttpContext ctx, int max = 2_000_000)
	{
		using var reader = new StreamReader(ctx.Request.Body, Encoding.UTF8);
		var text = await reader.ReadToEndAsync();
		return text.Length > max ? text[..max] : text;
	}

	public static Task WriteJson(HttpContext ctx, int status, object? value)
	{
		ctx.Response.StatusCode = status;
		ctx.Response.ContentType = "application/json; charset=utf-8";
		ctx.Response.Headers["Cache-Control"] = "no-store";
		return ctx.Response.WriteAsync(JsonSerializer.Serialize(value, Json));
	}

	public static string? Str(this JsonObject o, string key)
		=> o.TryGetPropertyValue(key, out var v) && v is JsonValue jv && jv.TryGetValue<string>(out var s) ? s : v?.ToString();

	public static bool? Bool(this JsonObject o, string key)
		=> o.TryGetPropertyValue(key, out var v) && v is JsonValue jv && jv.TryGetValue<bool>(out var b) ? b : null;

	public static double? Num(this JsonObject o, string key)
	{
		if (!o.TryGetPropertyValue(key, out var v) || v is not JsonValue jv) return null;
		if (jv.TryGetValue<double>(out var d)) return d;
		if (jv.TryGetValue<long>(out var l)) return l;
		if (jv.TryGetValue<string>(out var s) && double.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out d)) return d;
		return null;
	}
}
