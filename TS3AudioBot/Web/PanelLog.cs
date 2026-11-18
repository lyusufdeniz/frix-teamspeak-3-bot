// Frix: logları bellekte tutar, web panel "Loglar" sekmesi buradan okur.

using NLog;
using NLog.Config;
using NLog.Targets;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TS3AudioBot.Web;

[Target("FrixPanelLog")]
public sealed class PanelLog : TargetWithLayout
{
	private const int Capacity = 5000;
	private static readonly object Sync = new();
	private static readonly LinkedList<Entry> Entries = new();
	private static long nextSeq = 1;
	private static bool installed;

	public sealed record Entry(long Seq, DateTime Time, string Level, string Source, string Message);

	public static void Install()
	{
		lock (Sync)
		{
			if (installed)
				return;
			installed = true;
		}

		var config = LogManager.Configuration ?? new LoggingConfiguration();
		var target = new PanelLog { Name = "frixPanel" };
		config.AddTarget(target);
		config.AddRule(NLog.LogLevel.Info, NLog.LogLevel.Fatal, target);
		LogManager.Configuration = config;
		LogManager.ReconfigExistingLoggers();
	}

	protected override void Write(LogEventInfo logEvent)
	{
		var message = logEvent.FormattedMessage ?? "";
		if (logEvent.Exception != null)
			message += " — " + logEvent.Exception.GetType().Name + ": " + logEvent.Exception.Message;
		if (message.Length > 2000)
			message = message[..2000] + "…";

		var source = logEvent.LoggerName ?? "";
		var dot = source.LastIndexOf('.');
		if (dot >= 0) source = source[(dot + 1)..];

		lock (Sync)
		{
			Entries.AddLast(new Entry(nextSeq++, logEvent.TimeStamp, logEvent.Level.Name, source, message));
			while (Entries.Count > Capacity)
				Entries.RemoveFirst();
		}
	}

	public static (long last, List<Entry> items) Since(long seq, int max = 500)
	{
		lock (Sync)
		{
			var items = Entries.Where(e => e.Seq > seq).TakeLast(max).ToList();
			var last = Entries.Last?.Value.Seq ?? seq;
			return (last, items);
		}
	}

	/// <summary>Sayfalı okuma: en yeni kayıt ilk sayfadadır.</summary>
	public static (int total, List<Entry> items) Page(int page, int size, string? level, string? query)
	{
		lock (Sync)
		{
			IEnumerable<Entry> q = Entries.Reverse();
			if (!string.IsNullOrEmpty(level))
			{
				var levels = level.Split(',', StringSplitOptions.RemoveEmptyEntries);
				q = q.Where(e => levels.Contains(e.Level, StringComparer.OrdinalIgnoreCase));
			}
			if (!string.IsNullOrWhiteSpace(query))
				q = q.Where(e => e.Message.Contains(query, StringComparison.OrdinalIgnoreCase));
			var all = q.ToList();
			return (all.Count, all.Skip(Math.Max(0, page - 1) * size).Take(size).ToList());
		}
	}

	public static void Clear()
	{
		lock (Sync)
			Entries.Clear();
	}
}
