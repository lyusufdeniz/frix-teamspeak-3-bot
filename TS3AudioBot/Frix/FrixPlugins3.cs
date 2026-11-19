// Frix eklentileri (3): müzik ve radyo özelliklerinin aç/kapa anahtarı ve
// çalan şarkıyı bot adı / açıklaması yapan eklenti.

using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace TS3AudioBot.Frix;

/// <summary>Müzik komutları (!ara, !link, !liste, !istatistik ve oynatma kontrolleri). Kapalıyken bu komutlar çalışmaz.</summary>
public sealed class MusicPlugin : FrixPlugin
{
	public override string Id => "music";
	public override bool DefaultEnabled => true;
	public override string NameTr => "Müzik";
	public override string NameEn => "Music";
	public override string DescTr => "YouTube/link ile şarkı çalma: !ara, !link, !liste, !istatistik ve !gec, !dur, !ses gibi kontrol komutları. Kapatırsan bu komutlar devre dışı kalır (kontroller radyo açıksa çalışmaya devam eder).";
	public override string DescEn => "Playing songs from YouTube/links: !ara, !link, !liste, !istatistik and controls like !gec, !dur, !ses. Turning it off disables these commands (controls keep working while Radio is on).";
	public override PluginField[] Schema =>
	[
		new("forcePlay", "bool", "Direkt çal (kuyruğa ekleme)", "Play now (don't queue)", false, null,
			"Açıkken !ara ve !link çalan şarkıyı bırakıp yeni şarkıyı hemen çalar. Kuyruktaki diğer şarkılar korunur.",
			"When on, !ara and !link stop the current song and play the new one right away. The rest of the queue is kept."),
		new("announce", "bool", "Çalan şarkıyı kanala duyur", "Announce the playing song in the channel", true, null,
			"Her şarkı başladığında kanala 'Şimdi çalıyor' mesajı yazılır (metni: Bot metinleri → nowplaying).",
			"Posts a 'Now playing' message to the channel when each song starts (text: Bot texts → nowplaying)."),
	];

	public bool ForcePlay => B("forcePlay");
	public bool Announce => B("announce");
}

/// <summary>!radyo komutu.</summary>
public sealed class RadioPlugin : FrixPlugin
{
	public override string Id => "radio";
	public override bool DefaultEnabled => true;
	public override string NameTr => "Radyo";
	public override string NameEn => "Radio";
	public override string DescTr => "Panelde eklenen radyoları !radyo <isim> ile çalar. Kapatırsan !radyo devre dışı kalır.";
	public override string DescEn => "Plays the radios added in the panel with !radyo <name>. Turning it off disables !radyo.";
	public override PluginField[] Schema =>
	[
		new("forcePlay", "bool", "Direkt çal (kuyruğa ekleme)", "Play now (don't queue)", true, null,
			"Açıkken radyo çalan şarkıyı bırakıp hemen başlar. Kapalıysa radyo kuyruğun sonuna eklenir.",
			"When on, the radio starts right away and the current song stops. When off, the radio is added to the end of the queue."),
		new("announce", "bool", "Açılan radyoyu kanala duyur", "Announce the radio in the channel", true, null,
			"Radyo başladığında kanala mesaj yazılır (metni: Bot metinleri → nowplaying.radio).",
			"Posts a message to the channel when a radio starts (text: Bot texts → nowplaying.radio)."),
	];

	public bool ForcePlay => B("forcePlay");
	public bool Announce => B("announce");
}

/// <summary>Çalan şarkıyı botun adı ve/veya açıklaması yapar; boşta eski haline döner.</summary>
public sealed class NowPlayingStatusPlugin : FrixPlugin
{
	public override string Id => "npstatus";
	public override string NameTr => "Şarkı adı → bot adı/açıklaması";
	public override string NameEn => "Song → bot name/description";
	public override string DescTr => "Çalan şarkıyı botun TeamSpeak adı ve/veya açıklaması yapar. Çalma bitince ad ve açıklama eski haline döner.";
	public override string DescEn => "Shows the playing song as the bot's TeamSpeak nickname and/or description. Restores them when playback stops.";
	public override PluginField[] Schema =>
	[
		new("nick", "bool", "Bot adını değiştir", "Change the nickname", true),
		new("nickFormat", "text", "Ad biçimi", "Nickname format", "♪ {title}", null,
			"{title}, {user}, {bot} kullanılabilir. TeamSpeak adı en fazla 30 karakter; uzunsa kısaltılır.",
			"Use {title}, {user}, {bot}. TeamSpeak nicknames are max 30 characters; longer ones are cut."),
		new("desc", "bool", "Açıklamayı değiştir", "Change the description", true),
		new("descFormat", "text", "Açıklama biçimi", "Description format", "🎵 {title} · {user}", null,
			"{title}, {user}, {bot} kullanılabilir.", "Use {title}, {user}, {bot}."),
		new("idleDesc", "text", "Boştayken açıklama", "Description when idle", "", null,
			"Boş bırakırsan açıklama temizlenir.", "Leave empty to clear the description."),
	];

	/// <summary>Bot.cs'deki yerleşik "açıklamaya şarkı adı yaz" özelliği bu eklenti açıkken devre dışı kalır.</summary>
	public bool HandlesDescription => B("desc");

	private bool nickChanged;

	public override Task OnSongStarted() => Apply();
	public override Task OnPlaybackStopped() => Restore(true, true);
	protected override void OnDisable() => _ = Restore(true, true);
	protected override void OnConfigChanged()
	{
		_ = Task.Run(async () =>
		{
			if (!B("nick")) await Restore(true, false);
			await Apply();
		});
	}

	private Task Apply()
	{
		var data = Ctx.PlayManager.CurrentPlayData;
		if (data is null || !Ctx.Connected)
			return Task.CompletedTask;
		var title = data.ResourceData.ResourceTitle ?? "?";
		var values = new[] { ("title", title), ("user", FrixChat.NameOf(data.EffectiveOwnerUid)), ("bot", Ctx.BaseNickname()) };
		return Ctx.RunOnBot(async () =>
		{
			if (B("nick"))
			{
				var nick = Cut(Fill(S("nickFormat"), values).Trim(), 30);
				if (nick.Length >= 3)
				{
					try { await Ctx.Client.ChangeName(nick); nickChanged = true; }
					catch (Exception ex) { Log.Debug("Ad değiştirilemedi: {0}", ex.Message); }
				}
			}
			if (B("desc"))
				await Ctx.Full.ChangeDescription(Cut(Fill(S("descFormat"), values).Trim(), 200));
		});
	}

	private Task Restore(bool nick, bool desc)
	{
		if (!Ctx.Connected)
			return Task.CompletedTask;
		return Ctx.RunOnBot(async () =>
		{
			if (nick && nickChanged)
			{
				try { await Ctx.Client.ChangeName(Ctx.BaseNickname()); nickChanged = false; }
				catch (Exception ex) { Log.Debug("Ad geri alınamadı: {0}", ex.Message); }
			}
			if (desc)
				await Ctx.Full.ChangeDescription(Cut(S("idleDesc").Trim(), 200));
		});
	}

	private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();

	/// <summary>Emoji ve birleşik karakterleri bölmeden kısaltır.</summary>
	private static string Cut(string text, int max)
	{
		var info = new StringInfo(text);
		if (info.LengthInTextElements <= max) return text;
		return info.SubstringByTextElements(0, max - 1).TrimEnd() + "…";
	}
}

/// <summary>Botun avatarı: boştayken komut kartı, çalarken şarkının kapağı.</summary>
public sealed class AvatarPlugin : FrixPlugin
{
	public override string Id => "avatar";
	public override bool DefaultEnabled => true;
	public override string NameTr => "Avatar";
	public override string NameEn => "Avatar";
	public override string DescTr => "Botun TeamSpeak avatarını yönetir. Kapatırsan bot avatarı kaldırılır ve bir daha değiştirilmez.";
	public override string DescEn => "Manages the bot's TeamSpeak avatar. Turning it off removes the avatar and stops changing it.";
	public override PluginField[] Schema =>
	[
		new("idleAuto", "bool", "Boştayken komut kartını otomatik oluştur", "Generate the command card when idle", true, null,
			"Komut adların, özel komutların ve radyolara göre otomatik çizilir. Kapalıysa boşta avatar kaldırılır.",
			"Drawn automatically from your command names, custom commands and radios. If off, the avatar is removed when idle."),
		new("songCover", "bool", "Çalan şarkının görselini koy", "Show the playing song's cover", true, null,
			"YouTube küçük resmi avatar olur. Kapalıysa çalarken de boştaki avatar kalır.",
			"The YouTube thumbnail becomes the avatar. If off, the idle avatar stays while playing."),
	];

	public bool IdleAuto => B("idleAuto");
	public bool SongCover => B("songCover");

	protected override void OnEnable() => Refresh();
	protected override void OnDisable() => Refresh();
	protected override void OnConfigChanged() => Refresh();
	private void Refresh() { if (Ctx is { Connected: true }) _ = Ctx.RefreshAvatar(); }
}

/// <summary>Bot sunucudan düşünce (zaman aşımı, atılma, sunucu kapanması) kendiliğinden tekrar bağlanır ve aynı kanala girer.</summary>
public sealed class AutoReconnectPlugin : FrixPlugin
{
	public override string Id => "autoreconnect";
	public override bool DefaultEnabled => true;
	public override string NameTr => "Otomatik yeniden bağlan";
	public override string NameEn => "Auto reconnect";
	public override string DescTr => "Bot sunucudan düşünce kendiliğinden tekrar bağlanır ve aynı kanala geri girer. Kapalıysa bot düşünce kapalı kalır.";
	public override string DescEn => "When the bot drops from the server it reconnects by itself and rejoins the same channel. If off, the bot stays offline after a drop.";
	public override PluginField[] Schema =>
	[
		new("delay", "number", "İlk deneme (saniye)", "First retry (seconds)", 3, null,
			"Sonraki denemeler bu sürenin katlarıyla en fazla 2 dakikaya kadar uzar.", "Later retries back off up to 2 minutes."),
		new("maxTries", "number", "En fazla deneme (0 = sınırsız)", "Max retries (0 = unlimited)", 0),
		new("sameChannel", "bool", "Aynı kanala geri dön", "Rejoin the same channel", true, null,
			"Kapalıysa bot ayarlardaki varsayılan kanala girer.", "If off, the bot joins its default channel from the bot settings."),
		new("onKick", "bool", "Sunucudan atılınca da geri gir", "Also reconnect after being kicked from the server", true),
		new("onShutdown", "bool", "Sunucu kapanıp açılınca tekrar dene", "Retry when the server shuts down or restarts", true),
	];

	public bool SameChannel => B("sameChannel");

	/// <summary>Bekleme süresi; null = yeniden bağlanma.</summary>
	public TimeSpan? Delay(string type, int attempt)
	{
		var max = Math.Max(0, I("maxTries"));
		if (max > 0 && attempt >= max) return null;
		switch (type)
		{
		case "Ban": return null;
		case "Kick" when !B("onKick"): return null;
		case "ServerShutdown" when !B("onShutdown"): return null;
		}
		var first = Math.Clamp(I("delay"), 1, 600);
		var secs = Math.Min(first * Math.Pow(2, Math.Min(attempt, 6)), Math.Max(first, 120));
		if (type == "ServerShutdown") secs = Math.Max(secs, 15);
		return TimeSpan.FromSeconds(secs);
	}
}
