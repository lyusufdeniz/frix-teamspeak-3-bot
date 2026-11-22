// Frix eklentileri (4): ses eşitleme, otomatik devam, favoriler, zamanlanmış çalma, sunucu aktivitesi.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TS3AudioBot.Playlists;
using TS3AudioBot.ResourceFactories;

namespace TS3AudioBot.Frix;

/// <summary>Her şarkıyı ffmpeg loudnorm ile aynı ses seviyesine çeker.</summary>
public sealed class NormalizePlugin : FrixPlugin
{
	public override string Id => "normalize";
	public override bool DefaultEnabled => true;
	public override string NameTr => "Ses seviyesi eşitleme";
	public override string NameEn => "Volume levelling";
	public override string DescTr => "Çok yüksek ya da çok kısık kaydedilmiş şarkıları aynı ses seviyesine çeker. Değişiklik bir sonraki şarkıda başlar.";
	public override string DescEn => "Brings songs recorded too loud or too quiet to the same level. Changes apply from the next song.";
	public override PluginField[] Schema =>
	[
		new("level", "select", "Hedef seviye", "Target level", "normal", ["quiet", "normal", "loud"],
			"quiet: daha kısık, normal: önerilen, loud: daha yüksek", "quiet: softer, normal: recommended, loud: louder"),
	];

	private string Filter => S("level") switch
	{
		"quiet" => "loudnorm=I=-20:TP=-2:LRA=11",
		"loud" => "loudnorm=I=-13:TP=-1:LRA=9",
		_ => "loudnorm=I=-16:TP=-1.5:LRA=11",
	};

	protected override void OnEnable() => Ctx.Player.FfmpegProducer.AudioFilter = Filter;
	protected override void OnConfigChanged() => Ctx.Player.FfmpegProducer.AudioFilter = Filter;
	protected override void OnDisable() => Ctx.Player.FfmpegProducer.AudioFilter = null;
}

/// <summary>Kuyruk bitince son şarkıya benzer şarkıları (YouTube mix) çalmaya devam eder.</summary>
public sealed class AutoplayPlugin : FrixPlugin
{
	private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();
	public override string Id => "autoplay";
	public override string NameTr => "Otomatik devam";
	public override string NameEn => "Autoplay";
	public override string DescTr => "Kuyruk bitince bot susmaz; son çalan şarkıya benzer şarkıları kendisi seçip çalar. !dur/!bitir ile durdurulursa devam etmez.";
	public override string DescEn => "When the queue ends the bot keeps going with songs similar to the last one. It does not continue after !stop.";
	public override PluginField[] Schema =>
	[
		new("onlyListeners", "bool", "Sadece kanalda biri varken", "Only when someone is in the channel", true),
		new("announce", "bool", "Kanala 'otomatik devam' mesajı yaz", "Announce in the channel", true),
		new("noRepeat", "number", "Son kaç şarkıyı tekrar çalma", "Don't repeat the last N songs", 30),
	];

	private readonly LinkedList<string> recent = new();
	private string? lastId;
	private string? lastTitle;

	public override Task OnSongStarted()
	{
		var res = Ctx.PlayManager.CurrentPlayData?.ResourceData;
		if (res is { AudioType: "youtube" })
		{
			lastId = res.ResourceId;
			lastTitle = res.ResourceTitle;
			recent.Remove(res.ResourceId);
			recent.AddFirst(res.ResourceId);
			while (recent.Count > Math.Clamp(I("noRepeat"), 1, 200)) recent.RemoveLast();
		}
		return Task.CompletedTask;
	}

	public override async Task OnPlaybackStopped()
	{
		if (!Ctx.PlayManager.LastStopWasQueueEnd || lastId is null) return;
		if (B("onlyListeners") && !HasListeners()) return;
		var seed = lastId;
		var next = await Task.Run(() => Related(seed));
		if (next is null) { Log.Info("Otomatik devam: benzer şarkı bulunamadı ({0})", lastTitle); return; }
		if (Ctx.PlayManager.IsPlaying || Ctx.PlayManager.IsLoading) return; // bu arada biri şarkı açtı
		await Ctx.RunOnBot(async () =>
		{
			if (B("announce"))
				await Ctx.Client.SendChannelMessage(FrixChat.Colorize(FrixText.Get("autoplay.next", ("title", next.Value.title))));
			await Ctx.PlayManager.PlayNow(FrixChat.SystemInvoker("autoplay"), new AudioResource(next.Value.id, next.Value.title, "youtube"));
		});
	}

	private bool HasListeners()
	{
		var own = Ctx.Full.Book.OwnClient?.Channel;
		return own != null && Ctx.RealClients().Any(c => c.Channel == own);
	}

	/// <summary>YouTube "mix" listesinden daha önce çalınmamış bir şarkı seçer.</summary>
	private (string id, string title)? Related(string videoId)
	{
		var exe = ResourceFactories.Youtube.YtdlpStream.FindExe();
		if (exe is null) return null;
		var url = $"https://www.youtube.com/watch?v={videoId}&list=RD{videoId}";
		var psi = new ProcessStartInfo(exe, $"--no-warnings --flat-playlist --playlist-end 25 --print \"%(id)s\t%(title)s\" \"{url}\"")
		{
			UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
			StandardOutputEncoding = Encoding.UTF8, WorkingDirectory = AppContext.BaseDirectory,
		};
		try
		{
			using var p = Process.Start(psi)!;
			var output = p.StandardOutput.ReadToEnd();
			p.WaitForExit(30000);
			var items = output.Split('\n').Select(l => l.TrimEnd('\r').Split('\t')).Where(x => x.Length >= 2 && x[0].Length == 11)
				.Select(x => (id: x[0], title: x[1])).Where(x => x.id != videoId && !recent.Contains(x.id)).Take(8).ToList();
			if (items.Count == 0) return null;
			return items[Random.Shared.Next(Math.Min(items.Count, 4))];
		}
		catch (Exception ex)
		{
			Log.Debug(ex, "Otomatik devam listesi alınamadı");
			return null;
		}
	}
}

/// <summary>!fav / !favs komutları.</summary>
public sealed class FavoritesPlugin : FrixPlugin
{
	public override string Id => "favorites";
	public override bool DefaultEnabled => true;
	public override string NameTr => "Favoriler";
	public override string NameEn => "Favourites";
	public override string DescTr => "!fav çalan şarkıyı kişinin favorilerine ekler, !favs kişinin favorilerini kuyruğa ekler. !favs liste ile listelenir, !fav sil <no> ile silinir.";
	public override string DescEn => "!fav adds the playing song to your favourites, !favs queues your favourites. !favs list shows them, !fav del <no> removes one.";
	public override PluginField[] Schema =>
	[
		new("max", "number", "Kişi başı en fazla favori", "Max favourites per user", 200),
		new("shuffle", "bool", "!favs karışık sırayla eklesin", "!favs queues in random order", false),
	];

	public int Max => Math.Clamp(I("max"), 1, 5000);
	public bool Shuffle => B("shuffle");
}

/// <summary>Belirli saatlerde radyo / çalma listesi başlatır, durdurur ya da sesi ayarlar.</summary>
public sealed class ScheduledPlayPlugin : FrixPlugin
{
	private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();
	public override string Id => "scheduleplay";
	public override string NameTr => "Zamanlanmış çalma";
	public override string NameEn => "Scheduled playback";
	public override string DescTr => "Belirli gün ve saatlerde radyo ya da çalma listesi başlatır, durdurur veya sesi ayarlar.";
	public override string DescEn => "Starts a radio or playlist, stops playback or sets the volume at specific days and times.";
	public override PluginField[] Schema =>
	[
		new("entries", "schedule", "Plan", "Plan",
			"09:00 | radyo Power Türk\npzt,sal,çar,per,cum 18:30 | liste akşam\n00:30 | dur",
			null,
			"İşlemler: radyo <ad>, liste <ad>, dur, ses <0-100>. Günler: pzt sal çar per cum cmt paz; boş = her gün.",
			"Actions: radio <name>, playlist <name>, stop, volume <0-100>. Days: mon tue wed thu fri sat sun; empty = every day."),
	];

	private string lastMinute = "";

	public override async Task OnTick(DateTime utcNow)
	{
		var now = DateTime.Now;
		var minute = now.ToString("yyyyMMddHHmm");
		if (minute == lastMinute) return;
		lastMinute = minute;
		foreach (var raw in S("entries").Split('\n'))
		{
			var bar = raw.IndexOf('|');
			if (bar < 0) continue;
			var when = raw[..bar].Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
			var action = raw[(bar + 1)..].Trim();
			if (when.Length == 0 || action.Length == 0 || when[^1] != now.ToString("HH:mm")) continue;
			if (when.Length > 1 && !ScheduledPlugin.DayMatches(when[0], now.DayOfWeek)) continue;
			try { await Run(action); }
			catch (Exception ex) { Log.Warn("Zamanlanmış çalma '{0}' çalışmadı: {1}", action, ex.Message); }
		}
	}

	private async Task Run(string action)
	{
		var sp = action.IndexOf(' ');
		var verb = (sp < 0 ? action : action[..sp]).ToLowerInvariant();
		var arg = sp < 0 ? "" : action[(sp + 1)..].Trim();
		switch (verb)
		{
		case "radyo" or "radio":
			{
				var st = FrixRadio.Find(arg) ?? throw new InvalidOperationException("radyo yok: " + arg);
				var url = await FrixRadio.ResolveStreamUrl(st.Url);
				var res = new AudioResource(url, st.Name, "media").Add("radio", st.Name);
				await Ctx.RunOnBot(() => Ctx.PlayManager.PlayNow(FrixChat.SystemInvoker("schedule"), res));
				break;
			}
		case "liste" or "playlist":
			{
				var list = FrixData.FindPlaylist(arg) ?? throw new InvalidOperationException("liste yok: " + arg);
				var items = FrixData.PlaylistItems(list.Id).Select(i => new PlaylistItem(new AudioResource(i.ResourceId, i.Title, i.Type))).ToList();
				if (items.Count == 0) return;
				await Ctx.RunOnBot(() => Ctx.PlayManager.Play(FrixChat.SystemInvoker("schedule"), items));
				break;
			}
		case "dur" or "stop":
			await Ctx.RunOnBot(() => Ctx.PlayManager.Stop());
			break;
		case "ses" or "volume" or "vol":
			if (int.TryParse(arg, out var v)) await Ctx.RunOnBot(() => { Ctx.Player.Volume = Math.Clamp(v, 0, 100); return Task.CompletedTask; });
			break;
		}
	}
}

/// <summary>Sunucudaki çevrimiçi kişi sayısını 5 dakikada bir kaydeder (dashboard grafiği).</summary>
public sealed class ActivityPlugin : FrixPlugin
{
	public override string Id => "activity";
	public override bool DefaultEnabled => true;
	public override string NameTr => "Sunucu aktivitesi";
	public override string NameEn => "Server activity";
	public override string DescTr => "Sunucudaki çevrimiçi kişi sayısını kaydeder; dashboard'da son 24 saat grafiği ve en yoğun saatler görünür.";
	public override string DescEn => "Records how many people are online; the dashboard shows the last 24 hours and the busiest hours.";
	public override PluginField[] Schema =>
	[
		new("keepDays", "number", "Kaç gün saklansın", "Keep data for (days)", 90),
	];

	private string lastBucket = "";

	public override Task OnTick(DateTime utcNow)
	{
		var bucket = new DateTime(utcNow.Year, utcNow.Month, utcNow.Day, utcNow.Hour, utcNow.Minute / 5 * 5, 0, DateTimeKind.Utc);
		var key = bucket.ToString("O");
		if (key == lastBucket) return Task.CompletedTask;
		lastBucket = key;
		FrixData.RecordActivity(Ctx.ServerName, bucket, Ctx.RealClients().Count(), Math.Clamp(I("keepDays"), 1, 3650));
		return Task.CompletedTask;
	}
}
