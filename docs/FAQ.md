# SSS / FAQ

🇹🇷 Türkçe aşağıda · 🇬🇧 [English below](#english)

## Türkçe

**Şarkı bulunuyor ama ses gelmiyor / logda "Song queue ended" yazıyor**
Panel → Loglar sayfasındaki sarı/kırmızı satırlara bak. Genelde sebep YouTube'un engellemesidir: Ayarlar → *yt-dlp'yi güncelle*, olmazsa YouTube çerezi yükle (aşağıda). Bot TeamSpeak'e bağlı değilse (Panelde "Bağlanıyor") ses gitmez.

**Logda "YouTube bot doğrulaması istedi / Sign in to confirm you're not a bot"**
Sunucunun IP'si YouTube tarafından engelleniyor. Kendi bilgisayarında tarayıcıya **Get cookies.txt LOCALLY** eklentisini kur, YouTube'a giriş yapmışken youtube.com için `cookies.txt` dosyasını dışa aktar ve Panel → Ayarlar → **YouTube çerezi yükle** ile yükle. Mümkünse ayrı (ikincil) bir Google hesabı kullan.

**YouTube bir anda çalmayı bıraktı**
YouTube sık değişir. Panel → Ayarlar → **yt-dlp'yi güncelle**.

**Bot sunucuda "Frix1" olarak görünüyor**
Sunucuda aynı isimde başka bir istemci var (eski bot, ya da bot kapanmadan yeniden bağlandı). Diğerini kapat; bot bir sonraki bağlantıda asıl adını alır.

**"You are missing the 'cmd.xxx' right" hatası**
Bot klasöründeki `rights.toml` dosyasında ilgili komutu en üstteki `"+" = [ ... ]` listesine ekle ve botu yeniden başlat. (Web panel kullanıcıları bu dosyadan etkilenmez.)

**Channel commander açılmıyor**
Botun sunucu grubunda `b_client_use_channel_commander` yetkisi olmalı. Logda uyarı yazar.

**Bot sunucuya bağlanamıyor**
Adresi `ip:port` olarak gir (varsayılan port 9987), sunucu şifresi varsa bot ayarlarına yaz. İlk bağlantıda sunucu "güvenlik seviyesi" isteyebilir; bot bunu otomatik hesaplar, 10-60 sn sürebilir. Logda sebep yazar.

**Panele dışarıdan ulaşamıyorum**
Güvenlik duvarında **TCP 58913** portunu aç (Windows Güvenlik Duvarı / `ufw allow 58913/tcp`). Bulut sağlayıcının (AWS, Hetzner…) ağ kurallarını da kontrol et.

**Yönetici şifremi unuttum**
Başka bir yönetici Panel → Kullanıcılar sayfasından şifreni sıfırlayabilir. Hiç yönetici kalmadıysa botu kapat, `frix.db` dosyasını sil ve yeniden başlat (radyolar, komutlar ve eklenti ayarları da sıfırlanır; botlar ve TeamSpeak ayarları kalır).

**Radyo çalmıyor**
Doğrudan yayın adresi (`.mp3`, `.aac`, Icecast/Shoutcast) ya da `.m3u`, `.pls`, `.m3u8` linki kullan. Linki önce VLC'de dene. Web sayfası linkleri (ör. radyonun ana sayfası) çalışmaz.

**Canlı yayın çalınabilir mi?**
Evet: YouTube canlı yayınları, Twitch ve internet radyoları desteklenir. Canlı yayınlarda ileri sarma yoktur.

**Linux'ta ses yok / "libopus" hatası**
`sudo apt install libopus0 ffmpeg` (Debian/Ubuntu). `install.sh` bunları otomatik kurar.

**Bilgisayar açılınca otomatik başlasın**
Windows: tray simgesi → *Windows açılışında başlat* (varsayılan açık). Linux: `install.sh` systemd servisini açar (`systemctl enable frix`).

**Roller ne demek?**
*Yönetici*: her şey. *DJ*: şarkı/radyo ekleme ve oynatıcı kontrolü. *İzleyici*: sadece durumu görür.

**Birden fazla bot nasıl çalıştırılır?**
Panel → Botlar → Yeni bot. Örneğin bir müzik botu ve sadece karşılama/duyuru eklentileri açık bir karşılama botu oluşturabilirsin. Her bot ayrı bir TeamSpeak istemcisi olarak bağlanır.

### Eklentiler

Eklentiler her bot için ayrı ayrı açılır: Panel → Eklentiler → bot seç → kartı aç → ayarla → Kaydet. Kanal alanlarına kanalın **adını** ya da **ID'sini** (`5` veya `/5`) yazabilirsin. Grup alanlarına sunucu grubu ID'lerini virgülle yaz (TeamSpeak'te *İzinler → Sunucu Grupları* penceresinde görünür).

| Eklenti | Ne yapar | Botun TeamSpeak'te ihtiyacı olan yetki |
|---|---|---|
| Karşılama mesajı | Sunucuya bağlanana / botun kanalına girene özel mesaj, dürtme ya da kanal mesajı gönderir | — (dürtme için `i_client_poke_power`) |
| Duyuru / Reklam | Belirli aralıklarla kanala ya da sunucuya mesaj atar (sıralı veya rastgele) | Sunucu mesajı için `b_client_server_textmessage_send` |
| Yalnızken duraklat | Botun kanalında kimse kalmayınca müziği duraklatır, biri gelince devam eder | — |
| Boştayken radyo | Kuyruk bitip bot bir süre boş kalınca seçilen radyoyu çalar | — |
| AFK taşıyıcı | Boşta kalan, "uzakta" olan ya da hoparlörü kapalı olanları AFK kanalına taşır, dönünce geri alır | `i_client_move_power` (taşınacak kişilerin `i_client_needed_move_power` değerinden yüksek) |
| İsim filtresi | Takma adında yasaklı kelime geçenleri uyarır ya da sunucudan atar | Atmak için `i_client_kick_from_server_power` |
| Destek bildirimi | Destek kanalına biri girince yetkili gruplardaki çevrimiçi kişilere dürtme gönderir, kişiye bilgi verir | `i_client_poke_power` |
| Kanal adında bilgi | Seçilen kanalın adını çevrimiçi sayısı ve saatle günceller | `b_channel_modify_name` (o kanalda) |

**AFK taşıyıcı kimseyi taşımıyor**
- *AFK kanalı* alanındaki ad sunucudaki kanal adıyla birebir aynı olmalı (ya da ID yaz). Bulunamazsa Loglar sayfasında "kanal bulunamadı" uyarısı çıkar.
- Botun taşıma gücü, taşınacak kişininkinden yüksek olmalı. Sunucu yöneticilerini taşıyamaz; onları *Hariç tutulan gruplar* alanına yazmak da iyi olur.
- Kontrol 30 saniyede bir yapılır, yani taşıma birkaç saniye gecikebilir.

**İsim filtresi yanlış kişileri yakalıyor**
Kelime takma adın herhangi bir yerinde geçerse eşleşir (büyük/küçük harf fark etmez). Çok kısa kelimelerden kaçın, istisna grupları *Hariç tutulan gruplar* alanına ekle. Önce *dürt* ile dene, emin olunca *at*'a geç.

**Destek bildirimi kimseye gitmiyor**
*Yetkili grup ID'leri* boşsa ya da o gruplardan kimse çevrimiçi değilse kişiye "yetkili yok" mesajı gider. Aynı kişi için bekleme süresi (varsayılan 5 dk) dolmadan tekrar bildirim gitmez.

**Kanal adı değişmiyor**
Botun o kanalda adı düzenleme yetkisi olmalı (Loglar'da "Kanal adı değiştirilemedi" uyarısı çıkar). Kanal adları en fazla 40 karakter olabilir, şablon uzunsa kesilir. Adı değişeceği için kanalı mümkünse **ID** ile seç.

**Karşılama mesajı bot açılınca herkese gidiyor mu?**
Hayır. Bot bağlandıktan sonraki ilk 15 saniyedeki girişler (zaten sunucuda olanlar) yok sayılır ve aynı kişiye *tekrar* süresi dolmadan ikinci kez gönderilmez.

### Yeni eklentiler

| Eklenti | Ne yapar | Komut | Botun ihtiyacı olan yetki |
|---|---|---|---|
| Kayıt botu | Kayıt kanalına giren kayıtsızları yetkililere dürter; yetkili kaydedince üye grubunu verir, kayıtsız grubunu kaldırır, lobiye taşır | `!kayit <isim>` (sadece yetkili gruplar) | `i_group_member_add_power`, `i_group_member_remove_power`, `i_client_move_power`, `i_client_poke_power` |
| Geçici kanal oluşturucu | "Kanal oluştur" kanalına girene kendi adıyla geçici kanal açar ve taşır; kanal boşalınca TeamSpeak siler | — | `b_channel_create_temporary`, `i_client_move_power` |
| Aktiflik rütbeleri | Sunucuda geçirilen süreyi sayar, `10=12` gibi tanımlanan saatlere ulaşana grup verir | `!sure`, `!top` | `i_group_member_add_power` (ve önceki rütbeyi kaldırmak için `..._remove_power`) |
| Kanal koruma | Korunan kanala izinli grup dışından gireni dışarı taşır ya da kanaldan atar | — | `i_client_move_power` veya `i_client_kick_from_channel_power` |
| Flood koruması | Kısa sürede çok mesaj atanı dürterek uyarır, tekrarlarsa kanaldan/sunucudan atar | — | atma yetkileri, `i_client_poke_power` |
| Oylamayla geç | `!gec` hemen geçmez; kanaldakilerin belirli oranı oy verince geçer (şarkıyı açan ve muaf gruplar direkt geçer) | `!gec` | — |
| DJ yetkisi ve kuyruk limiti | Şarkı eklemeyi DJ gruplarına sınırlar, kişi başına kuyruk sınırı koyar; istenirse geç/dur/ses/bitir de sadece DJ'ler | — | — |
| Zamanlı duyuru | `21:00 \| mesaj` ya da `Cmt,Paz 12:00 \| mesaj` satırlarıyla belirli gün/saatte mesaj atar | — | sunucu mesajı için `b_client_server_textmessage_send` |
| Eğlence ve araçlar (varsayılan açık) | Zar, yazı-tura, seçim, anket, hava durumu, döviz, şarkı sözleri; istemediğin komutu kapatabilirsin | `!zar`, `!yazitura`, `!sec`, `!anket`, `!oy`, `!hava`, `!doviz`, `!dolar`, `!euro`, `!soz` | — |

**Komut örnekleri**
- `!kayit Ahmet` — kayıt kanalındaki Ahmet'i kaydeder. Birden fazla kişi eşleşirse bot isimleri listeler; tam adı yaz.
- `!liste kaydet rock` (kuyruğu kaydet) · `!liste cal rock` · `!liste ekle rock` (çalan şarkıyı ekle) · `!liste goster rock` · `!liste sil rock` · `!liste` (tüm listeler)
- `!anket Bu akşam ne oynayalım? | CS2 | LoL | Valorant` → herkes `!oy 2` yazar; süre dolunca (varsayılan 2 dk) ya da `!anket bitir` ile sonuç kanala yazılır.
- `!hava istanbul` · `!doviz EUR 50` · `!dolar` · `!euro 20` · `!zar 2d6` · `!yazitura` · `!sec pizza | döner | lahmacun`
- `!soz` çalan şarkının sözlerini **özel mesajla** gönderir; `!soz tarkan kuzu kuzu` ile başka şarkı aranabilir.
- `!istatistik` son 30 günün en çok çalınan şarkılarını ve en çok şarkı açanlarını gösterir; panelde İstatistik sayfasında daha fazlası var.

**Aktiflik rütbeleri süre saymıyor / !top boş**
Eklenti açık olmalı ve sadece **bir** botta açılmalı (iki botta açılırsa süre iki kez sayılır). Süre dakikada bir eklenir. Rütbeler `saat=grupID` biçiminde, her satıra bir tane yazılır.

**Geçici kanal açılmıyor**
Botun geçici kanal oluşturma ve taşıma yetkisi olmalı; Loglar'da "Geçici kanal açılamadı" uyarısı sebebi yazar. Aynı kişi için 30 saniyede bir kanal açılır.

**Hava, döviz ve söz servisleri hangileri?**
Ücretsiz ve anahtarsız servisler: open-meteo.com (hava), open.er-api.com (döviz), lrclib.net (şarkı sözleri). Bu servislere sadece şehir adı, döviz kodu ya da şarkı adı gider; kullanıcı bilgisi gitmez. Sonuçlar kısa süre önbellekte tutulur.

### Komutlar, metinler ve dil

**!ara, !gec gibi komutların adını değiştirebilir miyim?**
Evet: Panel → Komutlar → *Yerleşik komutlar*. Yeni adı yaz (ör. `ara` → `cal`) ve kaydet. *Eski ad da çalışsın* işaretini kaldırırsan eski ad kapanır. Bot cevaplarındaki, yardım metnindeki ve avatardaki komut adları kendiliğinden yeni adı gösterir.

**Kendi komutumu nasıl eklerim?**
Panel → Komutlar → *Özel komutlar*. *Metin* türü sabit bir cevap verir (ör. `!discord` → davet linki; `{user}`, `{args}`, `{time}`, `{date}` kullanılabilir). *Yönlendirme* türü başka bir komutu çalıştırır (ör. `!sarki` → `!ara`). Özel komut adı yerleşik bir komutla aynı olamaz.

**Botun avatarı neden komut listesi gösteriyor?**
Bot boştayken avatarı, ayarladığın komutlara göre otomatik çizilir (yeniden adlandırılan komutlar, özel komutlar ve radyo varsa `!radyo`). Açıklamaları Panel → Komutlar → *Metinler* bölümündeki `avatar.*` satırlarından değiştirebilirsin. Şarkı çalarken avatar şarkının kapağı olur. Tamamen kendi resmini kullanmak istersen `bots/<bot-adı>/avatars/` klasörüne `sleep.png` koy.

**Botun cevapları başka dilde olsun**
Panel → Ayarlar → *TeamSpeak cevap dili*: Türkçe, English, Deutsch, Español, Français, Русский, Português, Polski, Italiano. Tek tek cevapları Panel → Komutlar → *Metinler* bölümünden düzenleyebilirsin. Komut adları dile göre değişmez; istersen yeniden adlandır.

---

## English

**A song is found but there is no sound / "Song queue ended" in the log**
Check the yellow/red lines in Panel → Logs. Usually YouTube is blocking: Settings → *Update yt-dlp*; if that doesn't help, upload YouTube cookies (below). If the bot is not connected to TeamSpeak ("connecting"), no audio is sent.

**"Sign in to confirm you're not a bot" in the log**
YouTube blocks the server IP. Install the **Get cookies.txt LOCALLY** browser extension, export `cookies.txt` for youtube.com while logged in, and upload it in Panel → Settings → **Upload YouTube cookies**. Preferably use a secondary Google account.

**YouTube suddenly stopped working**
YouTube changes often. Panel → Settings → **Update yt-dlp**.

**The bot appears as "Frix1"**
Another client with the same nickname is on the server (an old bot, or a reconnect before the old session timed out). Close the other one; the bot gets its name back on the next connect.

**"You are missing the 'cmd.xxx' right"**
Add the command to the top `"+" = [ ... ]` list in `rights.toml` in the bot folder and restart. (Web panel users are not affected by this file.)

**Channel commander doesn't turn on**
The bot's server group needs the `b_client_use_channel_commander` permission. A warning is logged.

**The bot can't connect**
Use `ip:port` (default port 9987) and set the server password in the bot settings if needed. On first connect the server may require an identity security level; the bot computes it automatically (10-60 s). The log shows the reason.

**The panel is not reachable from outside**
Open **TCP 58913** in your firewall (Windows Firewall / `ufw allow 58913/tcp`) and in your cloud provider's network rules.

**I forgot the admin password**
Another admin can reset it in Panel → Users. If there is no admin left, stop the bot, delete `frix.db` and start again (radios, custom commands and plugin settings are reset; bots and TeamSpeak settings are kept).

**A radio station doesn't play**
Use a direct stream URL (`.mp3`, `.aac`, Icecast/Shoutcast) or a `.m3u`, `.pls`, `.m3u8` link. Test it in VLC first. Web page URLs don't work.

**Can it play live streams?**
Yes: YouTube live, Twitch and internet radio. Seeking is not available for live streams.

**No sound on Linux / "libopus" error**
`sudo apt install libopus0 ffmpeg` (Debian/Ubuntu). `install.sh` installs them automatically.

**Start automatically after reboot**
Windows: tray icon → *Start with Windows* (on by default). Linux: `install.sh` enables the systemd service.

**What do the roles mean?**
*Admin*: everything. *DJ*: add songs/radios and control playback. *Viewer*: read-only.

**How do I run multiple bots?**
Panel → Bots → New bot — e.g. a music bot plus a welcome bot that only has the welcome/announcement plugins enabled. Each bot connects as its own TeamSpeak client.

### Plugins

Plugins are enabled per bot: Panel → Plugins → pick a bot → expand the card → configure → Save. Channel fields accept the channel **name** or **ID** (`5` or `/5`). Group fields take comma-separated server group IDs (shown in TeamSpeak under *Permissions → Server Groups*).

| Plugin | What it does | Permission the bot needs |
|---|---|---|
| Welcome message | Private message, poke or channel message for users who connect / join the bot's channel | — (poke: `i_client_poke_power`) |
| Announcements / Ads | Posts messages to the channel or server at intervals (sequential or random) | Server messages: `b_client_server_textmessage_send` |
| Pause when alone | Pauses when nobody is left in the bot's channel, resumes when someone joins | — |
| Idle radio | Plays a chosen radio station when the queue has been empty for a while | — |
| AFK mover | Moves idle, away or speaker-muted users to an AFK channel and back when they return | `i_client_move_power` (higher than the target's `i_client_needed_move_power`) |
| Nickname filter | Warns or kicks users whose nickname contains a forbidden word | Kick: `i_client_kick_from_server_power` |
| Support notifier | Pokes online staff when someone joins the support channel and informs the user | `i_client_poke_power` |
| Channel name info | Keeps a channel name updated with the online count and time | `b_channel_modify_name` (on that channel) |

**The AFK mover doesn't move anyone**
- The *AFK channel* must match the channel name exactly (or use its ID). If it can't be found, a "channel not found" warning appears in Logs.
- The bot's move power must be higher than the user's. It can't move server admins; also add them to *Excluded groups*.
- Checks run every 30 seconds, so a move can be delayed a little.

**The nickname filter catches the wrong people**
A word matches anywhere in the nickname (case-insensitive). Avoid very short words and add exempt groups to *Excluded groups*. Start with *poke*, switch to *kick* once you're sure.

**The support notifier doesn't notify anyone**
If *Staff group IDs* is empty or nobody from those groups is online, the user gets the "no staff" message. The same user is not notified again until the cooldown (default 5 min) has passed.

**The channel name doesn't change**
The bot needs permission to edit that channel's name (a "could not change channel name" warning appears in Logs). Channel names are limited to 40 characters; longer templates are cut. Since the name changes, prefer selecting the channel by **ID**.

**Does the welcome message go to everyone when the bot starts?**
No. Joins during the first 15 seconds after the bot connects (people who were already online) are ignored, and the same user isn't greeted again before the *repeat* time.

### New plugins

| Plugin | What it does | Command | Permission the bot needs |
|---|---|---|---|
| Registration | Pokes staff when an unregistered user joins the registration channel; staff registration grants the member group, removes the unregistered group and moves the user to the lobby | `!kayit <name>` (staff groups only) | `i_group_member_add_power`, `i_group_member_remove_power`, `i_client_move_power`, `i_client_poke_power` |
| Temporary channel creator | Creates a temporary channel named after users who join the creator channel and moves them in; TeamSpeak deletes it when empty | — | `b_channel_create_temporary`, `i_client_move_power` |
| Activity ranks | Counts time spent on the server and grants groups at hours defined like `10=12` | `!sure`, `!top` | `i_group_member_add_power` (and `..._remove_power` to remove the previous rank) |
| Channel guard | Moves or kicks users without an allowed group out of protected channels | — | `i_client_move_power` or `i_client_kick_from_channel_power` |
| Anti-flood | Pokes users who send too many messages quickly and kicks repeat offenders from the channel/server | — | kick powers, `i_client_poke_power` |
| Vote skip | `!gec` doesn't skip immediately; it skips once a share of listeners voted (the requester and exempt groups skip directly) | `!gec` | — |
| DJ mode & queue limit | Limits adding songs to DJ groups and caps queued songs per user; optionally skip/pause/volume/stop for DJs only | — | — |
| Scheduled messages | Sends messages at set days/times with lines like `21:00 \| message` or `Sat,Sun 12:00 \| message` | — | server messages: `b_client_server_textmessage_send` |
| Fun & tools (on by default) | Dice, coin flip, pick, polls, weather, currency, lyrics; turn off the commands you don't want | `!zar`, `!yazitura`, `!sec`, `!anket`, `!oy`, `!hava`, `!doviz`, `!dolar`, `!euro`, `!soz` | — |

**Command examples**
- `!kayit John` — registers John in the registration channel. If several users match, the bot lists them; type the full name.
- `!liste kaydet rock` (save the queue) · `!liste cal rock` · `!liste ekle rock` (add the current song) · `!liste goster rock` · `!liste sil rock` · `!liste` (all playlists)
- `!anket What do we play tonight? | CS2 | LoL | Valorant` → everyone types `!oy 2`; the result is posted when time is up (default 2 min) or with `!anket bitir`.
- `!hava istanbul` · `!doviz EUR 50` · `!dolar` · `!euro 20` · `!zar 2d6` · `!yazitura` · `!sec pizza | burger | sushi`
- `!soz` sends the current song's lyrics as a **private message**; `!soz never gonna give you up` searches another song.
- `!istatistik` shows the most played songs and top requesters of the last 30 days; the panel's Stats page shows more.

**Activity ranks don't count time / !top is empty**
The plugin must be enabled, and on **one** bot only (two bots would count time twice). Time is added once a minute. Write ranks as `hours=groupID`, one per line.

**The temporary channel isn't created**
The bot needs permission to create temporary channels and move clients; Logs show the reason ("could not create temporary channel"). A user gets at most one channel every 30 seconds.

**Which services do weather, currency and lyrics use?**
Free services without API keys: open-meteo.com (weather), open.er-api.com (currency), lrclib.net (lyrics). Only the city, currency code or song name is sent — no user data. Results are cached briefly.

### Commands, texts and language

**Can I rename commands like !ara and !gec?**
Yes: Panel → Commands → *Built-in commands*. Enter a new name (e.g. `ara` → `play`) and save. Untick *keep old name* to disable the original. Command names in replies, the help text and the avatar update automatically.

**How do I add my own command?**
Panel → Commands → *Custom commands*. *Text* replies with fixed text (e.g. `!discord` → invite link; `{user}`, `{args}`, `{time}`, `{date}` are available). *Alias* runs another command (e.g. `!song` → `!ara`). Custom names can't clash with built-in commands.

**Why does the bot's avatar show a command list?**
While idle, the avatar is drawn automatically from your commands (renamed commands, custom commands, and `!radyo` if you have radio stations). Edit the descriptions via the `avatar.*` lines in Panel → Commands → *Texts*. While playing, the avatar shows the song's cover. To use your own image instead, put `sleep.png` into `bots/<bot-name>/avatars/`.

**I want the bot to reply in another language**
Panel → Settings → *TeamSpeak reply language*: Türkçe, English, Deutsch, Español, Français, Русский, Português, Polski, Italiano. Individual replies can be edited in Panel → Commands → *Texts*. Command names don't change with the language; rename them if you like.
