// Frix: ffmpeg / yt-dlp yollarını Windows ve Linux'ta otomatik bulur.

using System;
using System.IO;
using System.Runtime.InteropServices;

namespace TS3AudioBot.Frix;

public static class FrixTools
{
	public static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
	private static string Exe(string name) => IsWindows ? name + ".exe" : name;

	public static string YtdlpDir => Path.Combine(AppContext.BaseDirectory, "ytdlp");
	public static string YtdlpConf => Path.Combine(YtdlpDir, "yt-dlp.conf");

	/// <summary>Önce bot klasöründeki ytdlp/ klasörü, sonra bot klasörü, sonra PATH.</summary>
	public static string? FindYtdlp()
	{
		foreach (var p in new[] { Path.Combine(YtdlpDir, Exe("yt-dlp")), Path.Combine(AppContext.BaseDirectory, Exe("yt-dlp")) })
			if (File.Exists(p))
				return p;
		return FindOnPath(Exe("yt-dlp"));
	}

	public static string ResolveFfmpeg(string configured)
	{
		if (string.IsNullOrWhiteSpace(configured))
			configured = "ffmpeg";
		if (Path.IsPathRooted(configured))
			return configured;
		var local = Path.Combine(AppContext.BaseDirectory, configured);
		if (File.Exists(local))
			return local;
		if (File.Exists(local + ".exe"))
			return local + ".exe";
		return FindOnPath(Exe(Path.GetFileNameWithoutExtension(configured))) ?? configured;
	}

	public static string? FindOnPath(string file)
	{
		var path = System.Environment.GetEnvironmentVariable("PATH") ?? "";
		foreach (var dir in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
		{
			try
			{
				var full = Path.Combine(dir.Trim('"'), file);
				if (File.Exists(full))
					return full;
			}
			catch { }
		}
		return null;
	}
}
