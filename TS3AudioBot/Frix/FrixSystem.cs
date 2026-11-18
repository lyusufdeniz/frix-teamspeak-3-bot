// Frix: panel dashboard'u için sistem ölçümleri (CPU, RAM, uygulamanın belleği).
// Windows: GetSystemTimes + GlobalMemoryStatusEx, Linux: /proc/stat + /proc/meminfo.
// Her 2 saniyede bir örnek alınır, son 90 örnek (3 dk) grafik için tutulur.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace TS3AudioBot.Frix;

public static class FrixSystem
{
	public sealed record Sample(long Time, double Cpu, double AppCpu, long RamUsed, long RamTotal, long AppMem);

	private const int Keep = 90;
	private static readonly object Sync = new();
	private static readonly LinkedList<Sample> History = new();
	private static Timer? timer;
	private static (ulong idle, ulong total)? lastCpu;
	private static (TimeSpan cpu, DateTime at)? lastApp;
	public static DateTime StartedAt { get; } = Process.GetCurrentProcess().StartTime.ToUniversalTime();

	public static void Start()
	{
		if (timer != null) return;
		timer = new Timer(_ => { try { Take(); } catch { /* ölçüm kritik değil */ } }, null, 0, 2000);
	}

	public static Sample[] Samples() { lock (Sync) return [.. History]; }
	public static Sample? Last() { lock (Sync) return History.Last?.Value; }

	private static void Take()
	{
		var cpu = SystemCpu();
		var (used, total) = Memory();
		using var p = Process.GetCurrentProcess();
		var now = DateTime.UtcNow;
		double appCpu = 0;
		if (lastApp is { } la)
		{
			var wall = (now - la.at).TotalMilliseconds * System.Environment.ProcessorCount;
			if (wall > 0) appCpu = Math.Clamp((p.TotalProcessorTime - la.cpu).TotalMilliseconds / wall * 100, 0, 100);
		}
		lastApp = (p.TotalProcessorTime, now);
		var s = new Sample(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), Math.Round(cpu, 1), Math.Round(appCpu, 1), used, total, p.WorkingSet64);
		lock (Sync)
		{
			History.AddLast(s);
			while (History.Count > Keep) History.RemoveFirst();
		}
	}

	// ---------------------------------------------------------------- CPU
	private static double SystemCpu()
	{
		(ulong idle, ulong total)? cur = null;
		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
		{
			if (GetSystemTimes(out var idle, out var kernel, out var user))
				cur = (idle, kernel + user); // kernel süresi idle'ı içerir
		}
		else if (File.Exists("/proc/stat"))
		{
			var parts = File.ReadLines("/proc/stat").First().Split(' ', StringSplitOptions.RemoveEmptyEntries).Skip(1).Select(ulong.Parse).ToArray();
			var idle = parts[3] + (parts.Length > 4 ? parts[4] : 0);
			cur = (idle, (ulong)parts.Aggregate(0UL, (a, b) => a + b));
		}
		if (cur is null) return 0;
		var prev = lastCpu;
		lastCpu = cur;
		if (prev is null) return 0;
		var dTotal = cur.Value.total - prev.Value.total;
		var dIdle = cur.Value.idle - prev.Value.idle;
		return dTotal == 0 ? 0 : Math.Clamp((1 - (double)dIdle / dTotal) * 100, 0, 100);
	}

	// ---------------------------------------------------------------- RAM
	private static (long used, long total) Memory()
	{
		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
		{
			var st = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
			if (GlobalMemoryStatusEx(ref st))
				return ((long)(st.ullTotalPhys - st.ullAvailPhys), (long)st.ullTotalPhys);
		}
		else if (File.Exists("/proc/meminfo"))
		{
			long Get(string key) => File.ReadLines("/proc/meminfo").Where(l => l.StartsWith(key + ":"))
				.Select(l => long.Parse(l.Split(' ', StringSplitOptions.RemoveEmptyEntries)[1]) * 1024).FirstOrDefault();
			var total = Get("MemTotal");
			var avail = Get("MemAvailable");
			return (total - avail, total);
		}
		var gc = GC.GetGCMemoryInfo();
		return (gc.MemoryLoadBytes, gc.TotalAvailableMemoryBytes);
	}

	// ---------------------------------------------------------------- Win32
	private static ulong ToULong(FILETIME f) => ((ulong)f.High << 32) | f.Low;
	private static bool GetSystemTimes(out ulong idle, out ulong kernel, out ulong user)
	{
		idle = kernel = user = 0;
		if (!GetSystemTimesNative(out var i, out var k, out var u)) return false;
		idle = ToULong(i); kernel = ToULong(k); user = ToULong(u);
		return true;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct FILETIME { public uint Low; public uint High; }

	[StructLayout(LayoutKind.Sequential)]
	private struct MEMORYSTATUSEX
	{
		public uint dwLength, dwMemoryLoad;
		public ulong ullTotalPhys, ullAvailPhys, ullTotalPageFile, ullAvailPageFile, ullTotalVirtual, ullAvailVirtual, ullAvailExtendedVirtual;
	}

	[DllImport("kernel32.dll", EntryPoint = "GetSystemTimes", SetLastError = true)]
	private static extern bool GetSystemTimesNative(out FILETIME idle, out FILETIME kernel, out FILETIME user);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX buffer);
}
