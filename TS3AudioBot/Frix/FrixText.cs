// Frix: TeamSpeak'e giden bot cevapları. Dil seçilebilir (tr/en) ve
// her metin panelden düzenlenebilir (texts tablosunda saklanır).

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace TS3AudioBot;

public static partial class FrixText
{
	public static readonly string[] Languages = ["tr", "en", "de", "es", "fr", "ru", "pt", "pl", "it"];

	static FrixText()
	{
		foreach (var (lang, texts) in ExtraLanguages())
			Defaults[lang] = texts;
	}

	// {anahtar} yer tutucuları çalışma anında doldurulur.
	public static readonly Dictionary<string, Dictionary<string, string>> Defaults = new()
	{
		["tr"] = new()
		{
			["ara.empty"] = "❌ Lütfen bir şarkı adı yazın! Örn: !ara Tarkan Kuzu Kuzu",
			["ara.notfound"] = "❌ Şarkı bulunamadı.",
			["ara.found"] = "🔍 Müzik bulundu: 🎶 {title} - Yayın açılıyor...",
			["ara.queued"] = "📑 Kuyruğa eklendi: 🎶 {title} (Sıra: {pos})",
			["nowplaying"] = "🎵 Şimdi çalıyor: 🎶 {title} 🎼 ({user} istedi) 🎧",
			["nowplaying.radio"] = "📻 Radyo çalıyor: {title} ({user} açtı)",
			["failed"] = "❌ Çalınamadı: {title} — {reason}",
			["stream.error"] = "❌ Şarkı çalınırken hata oluştu: {reason}",
			["radio.empty"] = "📻 Radyo listesi: {list} — Kullanım: !radyo <isim>",
			["radio.none"] = "📻 Henüz radyo eklenmemiş. Web panelden ekleyebilirsiniz.",
			["radio.notfound"] = "❌ '{name}' adında radyo yok. Mevcut radyolar: {list}",
			["radio.opening"] = "📻 Radyo açılıyor: {name}",
			["radio.invalid"] = "❌ Radyo adresi okunamadı: {reason}",
			["user.webpanel"] = "Web panel",
			["user.unknown"] = "Bilinmeyen",
			["plugin.off"] = "❌ Bu özellik bu botta kapalı (Panel → Eklentiler).",
			["web.error"] = "❌ Servise şu an ulaşılamıyor, biraz sonra tekrar dene.",
			["reg.noperm"] = "❌ Kayıt yapma yetkin yok.",
			["reg.noconfig"] = "❌ Kayıt eklentisinde üye grubu ayarlanmamış.",
			["reg.notfound"] = "❌ '{user}' adında biri bulunamadı.",
			["reg.ambiguous"] = "❓ Birden fazla kişi eşleşti: {list}. Tam adı yaz.",
			["reg.failed"] = "❌ Grup verilemedi: {reason}",
			["reg.done"] = "✅ {user} kaydedildi.",
			["vote.count"] = "🗳️ Geçmek için oy: {count}/{needed}",
			["dj.notdj"] = "🎧 Bunu sadece DJ'ler yapabilir.",
			["dj.limit"] = "📑 Kuyrukta en fazla {max} şarkın olabilir.",
			["pl.none"] = "📂 Henüz çalma listesi yok. Kaydetmek için: !liste kaydet <ad>",
			["pl.list"] = "📂 Çalma listeleri: {list} — Çalmak için: !liste cal <ad>",
			["pl.emptyqueue"] = "❌ Kuyruk boş, kaydedilecek şarkı yok.",
			["pl.saved"] = "💾 '{name}' listesi kaydedildi ({count} şarkı).",
			["pl.added"] = "➕ {title} → '{name}' listesine eklendi.",
			["pl.noradio"] = "❌ Radyolar listeye eklenemez.",
			["pl.notfound"] = "❌ '{name}' adında liste yok.",
			["pl.empty"] = "📂 Bu liste boş.",
			["pl.usage"] = "📂 Kullanım: !liste kaydet|cal|ekle|goster|sil <ad>",
			["pl.queued"] = "📂 '{name}' listesi kuyruğa eklendi ({count} şarkı).",
			["pl.header"] = "📂 {name} ({count} şarkı):",
			["pl.deleted"] = "🗑️ '{name}' listesi silindi.",
			["fav.added"] = "⭐ Favorilerine eklendi: {title}",
			["fav.exists"] = "⭐ {title} zaten favorilerinde.",
			["fav.nothing"] = "❌ Şu an favoriye eklenecek bir şarkı çalmıyor.",
			["fav.none"] = "⭐ Henüz favorin yok. Çalan şarkıyı eklemek için: !fav",
			["fav.queued"] = "⭐ Favorilerin kuyruğa eklendi ({count} şarkı).",
			["fav.header"] = "⭐ Favorilerin ({count}):",
			["fav.removed"] = "🗑️ Favorilerinden çıkarıldı: {title}",
			["fav.badno"] = "❌ Böyle bir favori numarası yok. Liste için: !favs liste",
			["fav.limit"] = "❌ En fazla {max} favorin olabilir.",
			["fav.nouser"] = "❌ Bu komut sadece TeamSpeak'ten kullanılabilir.",
			["autoplay.next"] = "🔁 Otomatik devam: {title}",
			["stats.header"] = "📊 Son 30 gün: {count} şarkı çalındı.",
			["stats.songs"] = "🎶 En çok çalınanlar:",
			["stats.users"] = "🎧 En çok şarkı açanlar:",
			["online.none"] = "⏱️ Henüz süre kaydı yok (Aktiflik rütbeleri eklentisi açık olmalı).",
			["online.header"] = "⏱️ En aktifler:",
			["online.me"] = "⏱️ Sunucudaki toplam süren: {time} (sıralama: #{rank})",
			["lyrics.notfound"] = "❌ '{title}' için söz bulunamadı.",
			["lyrics.sent"] = "🎤 Sözler {user} kişisine özel mesajla gönderildi.",
			["poll.usage"] = "🗳️ Kullanım: !anket Soru? | Seçenek 1 | Seçenek 2 (2-10 seçenek)",
			["poll.running"] = "❌ Zaten devam eden bir anket var (!anket ile gör, !anket bitir ile bitir).",
			["poll.header"] = "🗳️ ANKET: {question}",
			["poll.result"] = "🗳️ ANKET SONUCU: {question}",
			["poll.howto"] = "Oy vermek için: !oy <numara> · {minutes} dk kaldı",
			["poll.none"] = "❌ Şu an açık anket yok.",
			["poll.badoption"] = "❌ 1 ile {max} arasında bir numara yaz.",
			["poll.voted"] = "✅ Oyun alındı: {option}",
			["poll.changed"] = "🔁 Oyun değiştirildi: {option}",
			["weather"] = "🌍 {place}: {desc}, {temp}°C (hissedilen {feels}°C) · nem %{humidity} · rüzgâr {wind} km/s",
			["weather.notfound"] = "❌ '{city}' bulunamadı.",
			["fx.usage"] = "💱 Kullanım: !doviz USD 100 · !dolar · !euro",
			["fx.unknown"] = "❌ '{code}' para birimi bulunamadı.",
			["fx.rate"] = "💱 {amount} {code} = {value} {base}",
			["dice"] = "🎲 {user} {dice} attı: {result}",
			["coin.heads"] = "🪙 {user}: YAZI",
			["coin.tails"] = "🪙 {user}: TURA",
			["pick.usage"] = "🤔 Kullanım: !sec pizza | döner | lahmacun",
			["pick"] = "🤔 Seçimim: {choice}",
			["avatar.subtitle"] = "boştayım · komut yaz:",
			["avatar.ara"] = "şarkı adı / link",
			["avatar.radyo"] = "radyo listesi",
			["avatar.gec"] = "sonraki şarkı",
			["avatar.dur"] = "duraklat · devam",
			["avatar.bitir"] = "durdur",
			["avatar.ses"] = "ses seviyesi",
			["avatar.calan"] = "çalan şarkı",
			["avatar.siradaki"] = "kuyruk",
			["avatar.yardim"] = "tüm komutlar",
			["avatar.footer"] = "Örn: !ara tarkan kuzu kuzu",
			["cmd.skipped"] = "⏭️ Şarkı atlandı!",
			["cmd.paused"] = "⏸ Müzik duraklatıldı.",
			["cmd.resumed"] = "▶️ Müzik devam ediyor.",
			["cmd.stopped"] = "🛑 Müzik durduruldu ve kuyruk temizlendi.",
			["cmd.volume.now"] = "🔊 Mevcut ses seviyesi: {volume}%",
			["cmd.volume.set"] = "🔊 Ses seviyesi: {volume}%",
			["cmd.np"] = "🎵 Şu an çalıyor: 🎶 {title} [{time}] 🎧 (İsteyen: {user})",
			["cmd.np.none"] = "❌ Şu an çalınan bir şarkı yok.",
			["cmd.np.live"] = "CANLI",
			["cmd.queue.empty"] = "📑 Kuyrukta bekleyen şarkı yok.",
			["cmd.queue.header"] = "📑 Sıradaki şarkılar ({count}):",
			["cmd.queue.line"] = "{pos}. {title}",
			["cmd.cleared"] = "🗑️ Şarkı kuyruğu temizlendi.",
			["cmd.help"] = "🎮 Komutlar:\n🎵 !ara <şarkı/link> - Çalar veya kuyruğa ekler\n📻 !radyo <isim> - Radyo çalar (!radyo = liste)\n⏭️ !gec - Sonraki şarkı\n⏸ !dur - Duraklat/devam | ▶️ !devam\n🛑 !bitir - Durdurur ve kuyruğu siler\n📑 !siradaki - Kuyruk | 🗑️ !temizle - Kuyruğu boşaltır\n🎶 !calan - Çalan şarkı\n🔊 !ses <0-100> - Ses seviyesi\n📂 !liste - Çalma listeleri | 🎤 !soz - Şarkı sözleri\n🗳️ !anket | 🌍 !hava <şehir> | 💱 !doviz | 🎲 !zar | 🪙 !yazitura | 🤔 !sec\n📊 !istatistik | ⏱️ !sure | 🏆 !top",
		},
		["en"] = new()
		{
			["ara.empty"] = "❌ Please type a song name! E.g. !ara Never Gonna Give You Up",
			["ara.notfound"] = "❌ Song not found.",
			["ara.found"] = "🔍 Found: 🎶 {title} - starting stream...",
			["ara.queued"] = "📑 Added to queue: 🎶 {title} (Position: {pos})",
			["nowplaying"] = "🎵 Now playing: 🎶 {title} 🎼 (requested by {user}) 🎧",
			["nowplaying.radio"] = "📻 Now playing radio: {title} (started by {user})",
			["failed"] = "❌ Could not play: {title} — {reason}",
			["stream.error"] = "❌ Playback error: {reason}",
			["radio.empty"] = "📻 Radio stations: {list} — Usage: !radyo <name>",
			["radio.none"] = "📻 No radio stations yet. Add them in the web panel.",
			["radio.notfound"] = "❌ No radio named '{name}'. Available: {list}",
			["radio.opening"] = "📻 Opening radio: {name}",
			["radio.invalid"] = "❌ Could not read radio stream: {reason}",
			["user.webpanel"] = "Web panel",
			["user.unknown"] = "Unknown",
			["plugin.off"] = "❌ This feature is disabled on this bot (Panel → Plugins).",
			["web.error"] = "❌ The service is unreachable right now, try again later.",
			["reg.noperm"] = "❌ You are not allowed to register users.",
			["reg.noconfig"] = "❌ The member group is not set in the registration plugin.",
			["reg.notfound"] = "❌ Nobody named '{user}' was found.",
			["reg.ambiguous"] = "❓ Several users match: {list}. Type the full name.",
			["reg.failed"] = "❌ Could not grant the group: {reason}",
			["reg.done"] = "✅ {user} has been registered.",
			["vote.count"] = "🗳️ Votes to skip: {count}/{needed}",
			["dj.notdj"] = "🎧 Only DJs can do that.",
			["dj.limit"] = "📑 You can have at most {max} songs in the queue.",
			["pl.none"] = "📂 No playlists yet. Save one with: !liste kaydet <name>",
			["pl.list"] = "📂 Playlists: {list} — Play with: !liste cal <name>",
			["pl.emptyqueue"] = "❌ The queue is empty, nothing to save.",
			["pl.saved"] = "💾 Playlist '{name}' saved ({count} songs).",
			["pl.added"] = "➕ {title} → added to '{name}'.",
			["pl.noradio"] = "❌ Radio stations can't be added to playlists.",
			["pl.notfound"] = "❌ No playlist named '{name}'.",
			["pl.empty"] = "📂 This playlist is empty.",
			["pl.usage"] = "📂 Usage: !liste kaydet|cal|ekle|goster|sil <name>",
			["pl.queued"] = "📂 Playlist '{name}' queued ({count} songs).",
			["pl.header"] = "📂 {name} ({count} songs):",
			["pl.deleted"] = "🗑️ Playlist '{name}' deleted.",
			["fav.added"] = "⭐ Added to your favourites: {title}",
			["fav.exists"] = "⭐ {title} is already in your favourites.",
			["fav.nothing"] = "❌ Nothing is playing that could be added.",
			["fav.none"] = "⭐ No favourites yet. Add the playing song with !fav",
			["fav.queued"] = "⭐ Your favourites were queued ({count} songs).",
			["fav.header"] = "⭐ Your favourites ({count}):",
			["fav.removed"] = "🗑️ Removed from your favourites: {title}",
			["fav.badno"] = "❌ No favourite with that number. See them with !favs list",
			["fav.limit"] = "❌ You can have at most {max} favourites.",
			["fav.nouser"] = "❌ This command only works from TeamSpeak.",
			["autoplay.next"] = "🔁 Autoplay: {title}",
			["stats.header"] = "📊 Last 30 days: {count} songs played.",
			["stats.songs"] = "🎶 Most played:",
			["stats.users"] = "🎧 Top requesters:",
			["online.none"] = "⏱️ No online time recorded yet (enable the Activity ranks plugin).",
			["online.header"] = "⏱️ Most active:",
			["online.me"] = "⏱️ Your total time on the server: {time} (rank #{rank})",
			["lyrics.notfound"] = "❌ No lyrics found for '{title}'.",
			["lyrics.sent"] = "🎤 Lyrics sent to {user} in a private message.",
			["poll.usage"] = "🗳️ Usage: !anket Question? | Option 1 | Option 2 (2-10 options)",
			["poll.running"] = "❌ A poll is already running (!anket to view, !anket bitir to end).",
			["poll.header"] = "🗳️ POLL: {question}",
			["poll.result"] = "🗳️ POLL RESULT: {question}",
			["poll.howto"] = "Vote with: !oy <number> · {minutes} min left",
			["poll.none"] = "❌ There is no open poll.",
			["poll.badoption"] = "❌ Type a number between 1 and {max}.",
			["poll.voted"] = "✅ Vote counted: {option}",
			["poll.changed"] = "🔁 Vote changed: {option}",
			["weather"] = "🌍 {place}: {desc}, {temp}°C (feels {feels}°C) · humidity {humidity}% · wind {wind} km/h",
			["weather.notfound"] = "❌ '{city}' not found.",
			["fx.usage"] = "💱 Usage: !doviz USD 100 · !dolar · !euro",
			["fx.unknown"] = "❌ Unknown currency '{code}'.",
			["fx.rate"] = "💱 {amount} {code} = {value} {base}",
			["dice"] = "🎲 {user} rolled {dice}: {result}",
			["coin.heads"] = "🪙 {user}: HEADS",
			["coin.tails"] = "🪙 {user}: TAILS",
			["pick.usage"] = "🤔 Usage: !sec pizza | burger | sushi",
			["pick"] = "🤔 I pick: {choice}",
			["avatar.subtitle"] = "idle · type a command:",
			["avatar.ara"] = "song name / link",
			["avatar.radyo"] = "radio stations",
			["avatar.gec"] = "next song",
			["avatar.dur"] = "pause · resume",
			["avatar.bitir"] = "stop",
			["avatar.ses"] = "volume",
			["avatar.calan"] = "now playing",
			["avatar.siradaki"] = "queue",
			["avatar.yardim"] = "all commands",
			["avatar.footer"] = "E.g. !ara never gonna give you up",
			["cmd.skipped"] = "⏭️ Skipped!",
			["cmd.paused"] = "⏸ Paused.",
			["cmd.resumed"] = "▶️ Resumed.",
			["cmd.stopped"] = "🛑 Stopped and cleared the queue.",
			["cmd.volume.now"] = "🔊 Current volume: {volume}%",
			["cmd.volume.set"] = "🔊 Volume: {volume}%",
			["cmd.np"] = "🎵 Now playing: 🎶 {title} [{time}] 🎧 (requested by {user})",
			["cmd.np.none"] = "❌ Nothing is playing right now.",
			["cmd.np.live"] = "LIVE",
			["cmd.queue.empty"] = "📑 The queue is empty.",
			["cmd.queue.header"] = "📑 Up next ({count}):",
			["cmd.queue.line"] = "{pos}. {title}",
			["cmd.cleared"] = "🗑️ Queue cleared.",
			["cmd.help"] = "🎮 Commands:\n🎵 !ara <song/link> - play or queue\n📻 !radyo <name> - play a radio (!radyo = list)\n⏭️ !gec - next song\n⏸ !dur - pause/resume | ▶️ !devam\n🛑 !bitir - stop and clear queue\n📑 !siradaki - queue | 🗑️ !temizle - clear queue\n🎶 !calan - now playing\n🔊 !ses <0-100> - volume\n📂 !liste - playlists | 🎤 !soz - lyrics\n🗳️ !anket | 🌍 !hava <city> | 💱 !doviz | 🎲 !zar | 🪙 !yazitura | 🤔 !sec\n📊 !istatistik | ⏱️ !sure | 🏆 !top",
		},
	};

	private static readonly ConcurrentDictionary<string, string> Overrides = new();
	private static string language = "tr";

	public static string Language => language;

	public static void Load()
	{
		try
		{
			language = Frix.FrixDb.GetMeta("lang") is { } l && Languages.Contains(l) ? l : "tr";
			Overrides.Clear();
			foreach (var row in Frix.FrixDb.Query("SELECT lang, key, value FROM texts"))
				Overrides[$"{row["lang"]}:{row["key"]}"] = (string)row["value"]!;
		}
		catch
		{
			// Veritabanı yoksa varsayılanlar kullanılır
		}
	}

	public static void SetLanguage(string lang)
	{
		if (!Languages.Contains(lang))
			throw new ArgumentException("Bilinmeyen dil");
		Frix.FrixDb.SetMeta("lang", lang);
		language = lang;
	}

	public static string Raw(string key, string? lang = null)
	{
		lang ??= language;
		if (Overrides.TryGetValue($"{lang}:{key}", out var o))
			return o;
		if (Defaults.TryGetValue(lang, out var d) && d.TryGetValue(key, out var v))
			return v;
		if (lang != "tr" && Defaults["en"].TryGetValue(key, out var en))
			return en;
		return Defaults["tr"].GetValueOrDefault(key, key);
	}

	public static string Get(string key, params (string name, string value)[] args)
	{
		var text = Frix.FrixCommands.ApplyNames(Raw(key));
		// Metinler BBCode içerebilir; değişkenlerdeki (şarkı adı, kullanıcı adı…) köşeli parantezler etkisizleştirilir
		foreach (var (name, value) in args)
			text = text.Replace("{" + name + "}", FrixChat.Escape(value));
		return text;
	}

	public static void SetOverride(string lang, string key, string? value)
	{
		if (string.IsNullOrEmpty(value) || value == (Defaults.GetValueOrDefault(lang)?.GetValueOrDefault(key)))
		{
			Frix.FrixDb.Exec("DELETE FROM texts WHERE lang=$l AND key=$k", ("$l", lang), ("$k", key));
			Overrides.TryRemove($"{lang}:{key}", out _);
		}
		else
		{
			Frix.FrixDb.Exec("INSERT INTO texts(lang,key,value) VALUES($l,$k,$v) ON CONFLICT(lang,key) DO UPDATE SET value=excluded.value",
				("$l", lang), ("$k", key), ("$v", value));
			Overrides[$"{lang}:{key}"] = value;
		}
	}

	public static IEnumerable<object> All(string lang)
		=> Defaults["tr"].Keys.Select(k => new
		{
			key = k,
			value = Raw(k, lang),
			defaultValue = Defaults.GetValueOrDefault(lang)?.GetValueOrDefault(k) ?? Defaults["tr"][k],
			custom = Overrides.ContainsKey($"{lang}:{k}"),
		});
}
