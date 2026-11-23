// Frix: GitHub Releases üzerinden güncelleme.
// Yeni sürüm _update/ klasörüne indirilir; bot 4 çıkış koduyla kapanır ve
// başlatıcı (Windows: Frix.exe, Linux: frix.sh) dosyaları kopyalayıp yeniden başlatır.
// Kullanıcı verileri (ayarlar, veritabanı, botlar, çerezler) asla üzerine yazılmaz.

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;

namespace TS3AudioBot.Frix;

public static class FrixUpdater
{
	private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();
	public const string DefaultRepo = "lyusufdeniz/frix-teamspeak-bot";
	public const int UpdateExitCode = 4;

	public static string CurrentVersion { get; } = FindVersion();

	private static string FindVersion()
	{
		foreach (var attr in typeof(FrixUpdater).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>())
			if (attr.Key == "FrixVersion")
				return attr.Value ?? "dev";
		return "dev";
	}

	/// <summary>Güncellemeler her zaman resmi depodan gelir (adres değiştirilemez).</summary>
	public static string Repo => DefaultRepo;

	public static string AssetName
	{
		get
		{
			var os = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "win" : "linux";
			var arch = RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "arm64" : "x64";
			return $"Frix-{os}-{arch}.zip";
		}
	}

	private static HttpClient Http()
	{
		var h = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
		h.DefaultRequestHeaders.UserAgent.ParseAdd("Frix-Updater");
		h.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
		return h;
	}

	public sealed record UpdateInfo(string Current, string? Latest, bool Available, string? Notes, string? Url, string? DownloadUrl, string? Error);

	public static async Task<UpdateInfo> Check()
	{
		if (!Repo.Contains('/'))
			return new UpdateInfo(CurrentVersion, null, false, null, null, null, "no_repo");
		try
		{
			using var http = Http();
			var json = await http.GetStringAsync($"https://api.github.com/repos/{Repo}/releases/latest");
			using var doc = JsonDocument.Parse(json);
			var root = doc.RootElement;
			var tag = root.GetProperty("tag_name").GetString()?.TrimStart('v');
			var notes = root.TryGetProperty("body", out var b) ? b.GetString() : null;
			var page = root.TryGetProperty("html_url", out var u) ? u.GetString() : null;
			string? download = null;
			foreach (var asset in root.GetProperty("assets").EnumerateArray())
				if (asset.GetProperty("name").GetString() == AssetName)
					download = asset.GetProperty("browser_download_url").GetString();
			var available = tag != null && IsNewer(tag, CurrentVersion) && download != null;
			return new UpdateInfo(CurrentVersion, tag, available, notes, page, download, download == null ? "no_asset" : null);
		}
		catch (Exception ex)
		{
			return new UpdateInfo(CurrentVersion, null, false, null, null, null, ex.Message);
		}
	}

	public static bool IsNewer(string latest, string current)
	{
		static Version Parse(string v) => Version.TryParse(new string(v.TakeWhile(c => char.IsDigit(c) || c == '.').ToArray()), out var r) ? r : new Version(0, 0);
		return current == "dev" || Parse(latest) > Parse(current);
	}

	// ---- ilerleme (panel bunu sorgular)
	public sealed record UpdateProgress(string Stage, string? Version, long Received, long Total, int Percent, string? Error);

	private static readonly object ProgressLock = new();
	private static UpdateProgress progress = new("idle", null, 0, 0, 0, null);
	public static UpdateProgress Progress { get { lock (ProgressLock) return progress; } }
	private static void SetProgress(UpdateProgress p) { lock (ProgressLock) progress = p; }
	public static bool Busy => Progress.Stage is "checking" or "downloading" or "extracting" or "restarting";

	/// <summary>
	/// Güncellemeyi arka planda başlatır: kontrol → indirme → açma → yeniden başlatma.
	/// Panel ilerlemeyi <see cref="Progress"/> ile izler. Zaten çalışıyorsa false.
	/// </summary>
	public static bool StartApply(Action restart)
	{
		lock (ProgressLock)
		{
			if (progress.Stage is "checking" or "downloading" or "extracting" or "restarting") return false;
			progress = new("checking", null, 0, 0, 0, null);
		}
		_ = Task.Run(async () =>
		{
			var info = await Check();
			if (!info.Available || info.DownloadUrl is null)
			{
				SetProgress(new("failed", info.Latest, 0, 0, 0, info.Error ?? "no_update"));
				return;
			}
			if (!await Download(info.DownloadUrl, info.Latest))
			{
				if (Progress.Stage != "failed") SetProgress(new("failed", info.Latest, 0, 0, 0, "download_failed"));
				return;
			}
			SetProgress(new("restarting", info.Latest, 0, 0, 100, null));
			await Task.Delay(1500); // panel "yeniden başlatılıyor" adımını görsün
			restart();
		});
		return true;
	}

	/// <summary>Güncellemeyi indirir ve _update/ klasörüne açar. Başarılıysa true.</summary>
	public static async Task<bool> Download(string url, string? version = null)
	{
		var baseDir = AppContext.BaseDirectory;
		var updateDir = Path.Combine(baseDir, "_update");
		var zipPath = Path.Combine(baseDir, "_update.zip");
		try
		{
			if (Directory.Exists(updateDir)) Directory.Delete(updateDir, true);
			using (var http = Http())
			using (var res = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
			{
				res.EnsureSuccessStatusCode();
				var total = res.Content.Headers.ContentLength ?? 0;
				SetProgress(new("downloading", version, 0, total, 0, null));
				await using var fs = File.Create(zipPath);
				await using var src = await res.Content.ReadAsStreamAsync();
				var buffer = new byte[81920];
				long received = 0;
				int read;
				while ((read = await src.ReadAsync(buffer)) > 0)
				{
					await fs.WriteAsync(buffer.AsMemory(0, read));
					received += read;
					SetProgress(new("downloading", version, received, total, total > 0 ? (int)(received * 100 / total) : 0, null));
				}
			}

			// Açma: dosya dosya, ilerleme ile
			SetProgress(new("extracting", version, 0, 0, 0, null));
			using (var zip = ZipFile.OpenRead(zipPath))
			{
				var root = Path.GetFullPath(updateDir) + Path.DirectorySeparatorChar;
				var files = zip.Entries.Where(e => e.Name.Length > 0).ToArray();
				for (var i = 0; i < files.Length; i++)
				{
					var target = Path.GetFullPath(Path.Combine(updateDir, files[i].FullName));
					if (!target.StartsWith(root, StringComparison.Ordinal))
						throw new InvalidDataException("Güncelleme paketinde geçersiz yol var.");
					Directory.CreateDirectory(Path.GetDirectoryName(target)!);
					files[i].ExtractToFile(target, true);
					if (i % 10 == 0 || i == files.Length - 1)
						SetProgress(new("extracting", version, i + 1, files.Length, (i + 1) * 100 / files.Length, null));
				}
			}
			File.Delete(zipPath);

			// Zip tek bir üst klasör içeriyorsa onu kök yap
			var entries = Directory.GetFileSystemEntries(updateDir);
			if (entries.Length == 1 && Directory.Exists(entries[0]))
			{
				var inner = entries[0];
				var tmp = updateDir + "_tmp";
				Directory.Move(inner, tmp);
				Directory.Delete(updateDir, true);
				Directory.Move(tmp, updateDir);
			}
			if (!File.Exists(Path.Combine(updateDir, RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "TS3AudioBot.exe" : "TS3AudioBot")))
				throw new InvalidDataException("Güncelleme paketi beklenen dosyaları içermiyor.");
			Log.Info("Güncelleme indirildi, yeniden başlatılınca uygulanacak.");
			return true;
		}
		catch (Exception ex)
		{
			Log.Error(ex, "Güncelleme indirilemedi");
			SetProgress(new("failed", version, 0, 0, 0, ex.Message));
			try { if (File.Exists(zipPath)) File.Delete(zipPath); } catch { }
			try { if (Directory.Exists(updateDir)) Directory.Delete(updateDir, true); } catch { }
			return false;
		}
	}
}
