// Frix: Türkçe sohbet komutları. Her botta (yeni oluşturulanlar dahil) çalışır.
// Cevap metinleri panelden düzenlenebilir (FrixText).

using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TS3AudioBot.Audio;
using TS3AudioBot.CommandSystem;
using TS3AudioBot.Dependency;
using TS3AudioBot.Playlists;
using TS3AudioBot.ResourceFactories;

namespace TS3AudioBot;

public static partial class MainCommands
{
	private static string Fmt(TimeSpan t) => t.TotalHours >= 1 ? t.ToString(@"h\:mm\:ss") : t.ToString(@"m\:ss");

	[Command("link")]
	public static Task<string?> CommandLink(ExecutionInformation info, PlayManager playManager, PlaylistManager playlistManager, ResolveContext resolver, InvokerData invoker, string url)
		=> CommandAra(info, playManager, playlistManager, resolver, invoker, url);

	private static void DjControl(ExecutionInformation info, InvokerData invoker)
	{
		if (invoker is ClientCall cc && info.TryGet<Config.ConfBot>(out var conf)
			&& Frix.FrixPluginHost.Enabled<Frix.DjModePlugin>(conf.Name) is { } dj && dj.CheckControl(cc) is { } err)
			throw new CommandException(err, CommandExceptionReason.CommandError);
	}

	[Command("gec")]
	public static async Task<string> CommandGec(ExecutionInformation info, PlayManager playManager, InvokerData invoker)
	{
		if (invoker is ClientCall cc && info.TryGet<Config.ConfBot>(out var conf)
			&& Frix.FrixPluginHost.Enabled<Frix.VoteSkipPlugin>(conf.Name) is { } vote)
		{
			var (skip, msg) = vote.Vote(cc, playManager);
			if (!skip) return msg ?? "";
		}
		else
		{
			DjControl(info, invoker);
		}
		await playManager.Next(invoker);
		return FrixText.Get("cmd.skipped");
	}

	[Command("skip")]
	public static Task<string> CommandSkip(ExecutionInformation info, PlayManager playManager, InvokerData invoker) => CommandGec(info, playManager, invoker);

	[Command("dur")]
	public static string CommandDur(ExecutionInformation info, InvokerData invoker, Player player)
	{
		DjControl(info, invoker);
		player.Paused = !player.Paused;
		return FrixText.Get(player.Paused ? "cmd.paused" : "cmd.resumed");
	}

	[Command("duraklat")]
	public static string CommandDuraklat(Player player)
	{
		player.Paused = true;
		return FrixText.Get("cmd.paused");
	}

	[Command("devam")]
	public static string CommandDevam(Player player)
	{
		player.Paused = false;
		return FrixText.Get("cmd.resumed");
	}

	[Command("bitir")]
	public static async Task<string> CommandBitir(ExecutionInformation info, InvokerData invoker, PlayManager playManager, PlaylistManager playlistManager)
	{
		DjControl(info, invoker);
		await playManager.Stop();
		playlistManager.Clear();
		return FrixText.Get("cmd.stopped");
	}

	[Command("kapat")]
	public static Task<string> CommandKapat(ExecutionInformation info, InvokerData invoker, PlayManager playManager, PlaylistManager playlistManager) => CommandBitir(info, invoker, playManager, playlistManager);

	[Command("ses")]
	public static string CommandSes(ExecutionInformation info, InvokerData invoker, Player player, int? seviye = null)
	{
		if (seviye != null) DjControl(info, invoker);
		if (seviye is null)
			return FrixText.Get("cmd.volume.now", ("volume", player.Volume.ToString("0")));
		player.Volume = Math.Clamp(seviye.Value, 0, 100);
		return FrixText.Get("cmd.volume.set", ("volume", player.Volume.ToString("0")));
	}

	[Command("calan")]
	public static string CommandCalan(PlayManager playManager, Player player)
	{
		var data = playManager.CurrentPlayData;
		if (data is null)
			return FrixText.Get("cmd.np.none");
		var title = FrixChat.Escape(data.ResourceData.ResourceTitle ?? data.ResourceData.ResourceId);
		var pos = player.Position ?? TimeSpan.Zero;
		var len = player.Length ?? data.PlayResource.SongInfo?.Length;
		var time = len is { } l && l > TimeSpan.Zero ? $"{Fmt(pos)} / {Fmt(l)}" : FrixText.Raw("cmd.np.live");
		return FrixText.Get("cmd.np", ("title", title), ("time", time), ("user", FrixChat.NameOf(data.EffectiveOwnerUid)));
	}

	[Command("siradaki")]
	public static string CommandSiradaki(PlaylistManager playlistManager, PlayManager playManager)
	{
		var items = playlistManager.CurrentList.Items;
		var start = playManager.IsPlaying ? playlistManager.Index + 1 : playlistManager.Index;
		var next = items.Skip(Math.Max(0, start)).Take(10).ToArray();
		if (next.Length == 0)
			return FrixText.Get("cmd.queue.empty");
		var sb = new StringBuilder(FrixText.Get("cmd.queue.header", ("count", (items.Count - start).ToString())));
		for (int i = 0; i < next.Length; i++)
			sb.Append('\n').Append(FrixText.Get("cmd.queue.line", ("pos", (i + 1).ToString()),
				("title", FrixChat.Escape(next[i].AudioResource.ResourceTitle ?? next[i].AudioResource.ResourceId))));
		return sb.ToString();
	}

	[Command("temizle")]
	public static string CommandTemizle(PlaylistManager playlistManager, PlayManager playManager)
	{
		// Çalan şarkı kalsın, sadece sıradakiler silinsin
		var idx = playlistManager.Index;
		var playing = playManager.IsPlaying;
		playlistManager.ModifyPlaylist(".mix", list =>
		{
			for (int i = list.Items.Count - 1; i >= 0; i--)
				if (!(playing && i == idx))
					list.RemoveAt(i);
		});
		playlistManager.Index = 0;
		return FrixText.Get("cmd.cleared");
	}

	[Command("yardim")]
	public static string CommandYardim() => FrixText.Get("cmd.help");
}
