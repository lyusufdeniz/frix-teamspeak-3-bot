> **Tam dokümantasyon / Full documentation:** https://frix-teamspeak.github.io/docs.html

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

Eklentiler her bot için ayrı ayrı açılır: Panel → Eklentiler → bot seç → eklentiye tıkla → içindeki özellikleri tek tek aç, ayarla → Kaydet. Benzer özellikler tek eklentide toplanır (ör. AFK taşıyıcı ve flood koruması *Moderasyon* içinde). Kanal alanlarının yanındaki düğmeyle kanalı listeden seçebilirsin. Kanal alanlarına kanalın **adını** ya da **ID'sini** (`5` veya `/5`) yazabilirsin. Grup alanlarına sunucu grubu ID'lerini virgülle yaz (TeamSpeak'te *İzinler → Sunucu Grupları* penceresinde görünür).

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
Evet: Panel → Eklentiler (her eklenti kartının altındaki *Komutlar*). Yeni adı yaz (ör. `ara` → `cal`) ve kaydet. *Eski ad da çalışsın* işaretini kaldırırsan eski ad kapanır. Bot cevaplarındaki, yardım metnindeki ve avatardaki komut adları kendiliğinden yeni adı gösterir.

**Kendi komutumu nasıl eklerim?**
Panel → Eklentiler → *Özel komutlar*. *Metin* türü sabit bir cevap verir (ör. `!discord` → davet linki; `{user}`, `{args}`, `{time}`, `{date}` kullanılabilir). *Yönlendirme* türü başka bir komutu çalıştırır (ör. `!sarki` → `!ara`). Özel komut adı yerleşik bir komutla aynı olamaz.

**Müzik ya da radyoyu kapatabilir miyim?**
Evet: Panel → Eklentiler → *Müzik* / *Radyo*. *Müzik* kapalıyken `!ara`, `!link`, `!liste`, `!istatistik` çalışmaz; *Radyo* kapalıyken `!radyo` çalışmaz. `!gec`, `!dur`, `!ses` gibi kontroller ikisinden biri açık olduğu sürece çalışır. Kapalı bir özelliğin komutu yazılırsa bot "bu özellik kapalı" der. Komutlar sayfası ve `!yardim` sadece açık eklentilerin komutlarını gösterir.

**!ara yazınca şarkı kuyruğa değil direkt çalsın**
Panel → Eklentiler → *Müzik* → *Direkt çal (kuyruğa ekleme)*. Açıkken yeni şarkı çalanı bırakıp hemen başlar, kuyruk korunur. Aynı seçenek *Radyo* eklentisinde de var (varsayılan açık); kapatırsan radyo kuyruğa eklenir.

**Bot sunucudan düşünce kendi geri girsin**
Panel → Eklentiler → *Otomatik yeniden bağlan* (varsayılan açık). Bot zaman aşımı, sunucu yeniden başlaması ya da sunucudan atılma sonrası kendiliğinden bağlanır ve aynı kanala döner. İlk deneme süresini, en fazla deneme sayısını ve atılınca geri girip girmeyeceğini ayarlayabilirsin. Banlanınca tekrar denemez.

**Bazı şarkılar çok yüksek, bazıları çok kısık çalıyor**
Panel → Eklentiler → *Müzik* → *Ses seviyesi eşitleme* (varsayılan açık). Her şarkı aynı ses seviyesine çekilir; hedef seviyeyi kısık / normal / yüksek seçebilirsin. Değişiklik bir sonraki şarkıda başlar.

**Kuyruk bitince bot sussun istemiyorum**
Panel → Eklentiler → *Müzik* → *Otomatik devam*. Kuyruk bitince son çalan şarkıya benzer bir şarkı (YouTube mix) seçilip çalınır. `!dur` / `!bitir` ile durdurursan devam etmez. İstersen sadece kanalda biri varken çalışır.

**Favoriler nasıl çalışır?**
*Favoriler* eklentisi açıkken `!fav` çalan şarkıyı senin favorilerine ekler, `!favs` hepsini kuyruğa ekler. `!favs liste` listeyi gösterir, `!fav sil 3` 3. favoriyi siler. Favoriler TeamSpeak kimliğine göre kişiye özeldir.

**Her sabah radyo otomatik başlasın**
Panel → Eklentiler → *Zamanlanmış çalma* → *Plan ekle*. Her plan için günleri işaretle (hiçbiri = her gün), saati seç ve işlemi belirle: radyo çal, liste çal, durdur ya da ses ayarla. Radyo ve liste açılır listeden seçilir.

**Duyuru/reklam ve zamanlı duyuru nerede?**
İkisi tek eklentide: Panel → Eklentiler → *Karşılama ve duyurular* → *Duyurular*. Dönen mesajlar belirli aralıklarla gider; *Zamanlı duyurular* alanına `21:00 | mesaj` biçiminde gün/saat bazlı mesajlar yazılır. Sadece zamanlı duyuru istiyorsan dönen mesajları boş bırak.

**Dashboard'daki sunucu aktivitesi**
*Sunucu aktivitesi* eklentisi (varsayılan açık) çevrimiçi kişi sayısını 5 dakikada bir kaydeder. Dashboard'da son 24 saat grafiği ve gün × saat ısı haritası (en yoğun saatler) görünür.

**Çalan şarkı botun adında / açıklamasında görünsün**
Panel → Eklentiler → *Müzik* → *Şarkı adı → bot adı/açıklaması*. Ad ve açıklama biçimini ayrı ayrı ayarlarsın (`{title}`, `{user}`, `{bot}`). TeamSpeak adları en fazla 30 karakter olduğu için uzun adlar kısaltılır. Çalma bitince bot eski adına döner.

**Avatarı nasıl kapatırım ya da şarkı kapağını istemiyorum**
Panel → Eklentiler → *Avatar*: boşta, müzik çalarken ve radyo çalarken ayrı ayrı seçilir. Boşta: komut kartı, kendi resmin ya da avatar yok. Müzik: şarkı kapağı, kendi resmin ya da boştaki avatar. Radyo: boştaki avatar ya da kendi resmin. *Kendi resmim* seçilince *Resim seç* ile yüklersin. Eklentiyi kapatırsan bot avatarı kaldırılır.

**Bot cevaplarını renkli / kalın yapabilir miyim?**
Evet: Panel → Eklentiler → *Bot metinleri*. Üstteki *Mesaj görünümü* kartında tüm cevapların varsayılan rengini, hata rengini ve kalınlığı seçersin. Her cevabın üstündeki araç çubuğuyla seçili yazıyı kalın, italik, altı çizili ya da renkli yaparsın (TeamSpeak BBCode: `[b]`, `[i]`, `[u]`, `[s]`, `[color=#FF0000]`, `[url]`); altındaki önizleme TeamSpeak'te nasıl görüneceğini gösterir. Kendi `[color]` etiketi olan cevaplara varsayılan renk eklenmez.

**Botun avatarı neden komut listesi gösteriyor?**
Bot boştayken avatarı, ayarladığın komutlara göre otomatik çizilir (yeniden adlandırılan komutlar, özel komutlar ve radyo varsa `!radyo`). Açıklamaları Panel → Eklentiler → *Bot metinleri* bölümündeki `avatar.*` satırlarından değiştirebilirsin. Şarkı çalarken avatar şarkının kapağı olur. Tamamen kendi resmini kullanmak istersen `bots/<bot-adı>/avatars/` klasörüne `sleep.png` koy.

**Dili nasıl değiştiririm?**
Tek bir dil ayarı var: Panel → Ayarlar → Genel → *Dil* (ilk kurulumda da sorulur). Panel, botun TeamSpeak cevapları ve komut adları hep bu dili kullanır: Türkçe, English, Deutsch, Español, Français, Русский, Português, Polski, Italiano. Komut adları Türkçe seçilirse Türkçe, diğer tüm dillerde İngilizce gelir (`!play`, `!next`, `!help`…). Dil değişince komutlara verdiğin özel adlar bu varsayılanlara döner; Türkçe adlar her zaman çalışır ve her komutu panelden istediğin gibi adlandırabilirsin. Tek tek cevapları Panel → Eklentiler → *Bot metinleri* bölümünden düzenleyebilirsin.


**Kendi müzik dosyalarımı nasıl çalarım?**
Panel → *Kütüphane* → *Dosya yükle* (mp3, ogg, opus, wav, flac, m4a, aac, webm, wma; dosya başına en fazla 200 MB). Dosyalar `music-library/` klasöründe durur. Satırdaki düğmelerle çalar, kuyruğa ya da bir çalma listesine eklersin; sohbette `!dosya <ad>` da çalışır.

**Scriptlerle ne yapabilirim?**
Panel → Eklentiler → *Scriptler*. JavaScript ile kendi komutunu (`frix.command('selam', (args, ctx) => 'Merhaba ' + ctx.user)`) ya da olaylara tepki veren otomasyonlar (`frix.on('connect' | 'channel' | 'message' | 'song' | 'tick', …)`) yazarsın. Scriptler dosya, ağ ya da sistem erişimi olmadan çalışır; her çağrı 2 saniye ve 32 MB ile sınırlıdır. Tam liste düzenleyicideki *API başvurusu* bölümünde, hatalar *Script logları* penceresinde.

**Ayarlarımı nasıl yedeklerim / taşırım?**
Panel → Ayarlar → *Yedekleme*. İstediğin bölümleri seçip tek bir JSON dosyası indirirsin; *Yedekten geri yükle* ile başka bir kurulumda içeriğini görüp seçtiğin bölümleri geri yüklersin. Kullanıcılar ve şifreler, bot bağlantı ayarları ve kütüphanedeki müzik dosyaları yedeğe girmez.

**Botu kanalıma nasıl çağırırım, biri en son ne zaman buradaydı?**
`!gel` botu yazdığın kanala taşır. `!gorulme <isim>` kişinin şu an çevrimiçi olup olmadığını ya da en son ne zaman görüldüğünü söyler (botların gördüğü herkes dakikada bir kaydedilir).

**Docker ya da Pterodactyl ile kurabilir miyim?**
Evet: README'deki *Docker* ve *Pterodactyl* bölümlerine bak. Docker'da veriler `/data` biriminde, Pterodactyl'de sunucunun klasöründe durur; panel portu `FRIX_WEB_PORT` ile (Pterodactyl'de otomatik) ayarlanır.

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

Plugins are enabled per bot: Panel → Plugins → pick a bot → click the plugin → switch on and configure the features inside → Save. Related features are grouped into one plugin (e.g. AFK mover and anti-flood are in *Moderation*). Channel fields have a button to pick the channel from a list. Channel fields accept the channel **name** or **ID** (`5` or `/5`). Group fields take comma-separated server group IDs (shown in TeamSpeak under *Permissions → Server Groups*).

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
Yes: Panel → Plugins (the *Commands* list at the bottom of each plugin card). Enter a new name (e.g. `ara` → `play`) and save. Untick *keep old name* to disable the original. Command names in replies, the help text and the avatar update automatically.

**How do I add my own command?**
Panel → Plugins → *Custom commands*. *Text* replies with fixed text (e.g. `!discord` → invite link; `{user}`, `{args}`, `{time}`, `{date}` are available). *Alias* runs another command (e.g. `!song` → `!ara`). Custom names can't clash with built-in commands.

**Can I turn music or radio off?**
Yes: Panel → Plugins → *Music* / *Radio*. With *Music* off, `!ara`, `!link`, `!liste` and `!istatistik` don't work; with *Radio* off, `!radyo` doesn't. Controls like `!gec`, `!dur` and `!ses` work while either is on. If someone types a disabled command, the bot says the feature is off. The commands page and `!yardim` only list commands whose plugin is on.

**Make !ara play right away instead of queueing**
Panel → Plugins → *Music* → *Play now (don't queue)*. When on, the new song replaces the current one immediately and the queue is kept. The *Radio* plugin has the same option (on by default); turn it off to queue radios instead.

**Make the bot rejoin by itself after it drops**
Panel → Plugins → *Auto reconnect* (on by default). After a timeout, a server restart or a kick, the bot reconnects and goes back to the same channel. You can set the first retry delay, the maximum number of retries and whether to come back after a kick. It never retries after a ban.

**Some songs are much louder than others**
Panel → Plugins → *Music* → *Volume levelling* (on by default). Every song is brought to the same loudness; choose quiet / normal / loud. Changes apply from the next song.

**I don't want the bot to go silent when the queue ends**
Panel → Plugins → *Music* → *Autoplay*. When the queue ends, a song similar to the last one (YouTube mix) is played. It does not continue after `!stop`. Optionally only while someone is in the channel.

**How do favourites work?**
With the *Favourites* plugin on, `!fav` adds the playing song to your favourites and `!favs` queues all of them. `!favs list` shows the list, `!fav del 3` removes the 3rd one. Favourites belong to your TeamSpeak identity.

**Start a radio every morning**
Panel → Plugins → *Scheduled playback* → *Add plan*. For each plan tick the days (none = every day), pick the time and the action: play a radio, play a playlist, stop or set the volume. Radios and playlists are picked from a list.

**Where are announcements and scheduled messages?**
Both are one plugin now: Panel → Plugins → *Welcome & announcements* → *Announcements*. Rotating messages go out at an interval; put day/time based messages like `21:00 | message` in *Scheduled messages*. Leave the rotating messages empty if you only want scheduled ones.

**Server activity on the dashboard**
The *Server activity* plugin (on by default) records how many people are online every 5 minutes. The dashboard shows the last 24 hours and a day × hour heat map of the busiest times.

**Show the playing song in the bot's nickname / description**
Panel → Plugins → *Music* → *Song → bot name/description*. Set the nickname and description formats separately (`{title}`, `{user}`, `{bot}`). TeamSpeak nicknames are max 30 characters, so long titles are cut. The bot gets its own name back when playback stops.

**How do I turn the avatar or the song cover off?**
Panel → Plugins → *Avatar*: chosen separately for idle, music and radio. Idle: command card, your own image or no avatar. Music: song cover, your own image or the idle avatar. Radio: the idle avatar or your own image. With *My image* selected, upload it with *Choose image*. Turning the plugin off removes the bot's avatar.

**Can the bot's replies be coloured / bold?**
Yes: Panel → Plugins → *Bot texts*. The *Message look* card sets the default colour, error colour and bold for all replies. The toolbar above each reply makes the selected text bold, italic, underlined or coloured (TeamSpeak BBCode: `[b]`, `[i]`, `[u]`, `[s]`, `[color=#FF0000]`, `[url]`); the preview below shows how it will look in TeamSpeak. Replies with their own `[color]` tag don't get the default colour.

**Why does the bot's avatar show a command list?**
While idle, the avatar is drawn automatically from your commands (renamed commands, custom commands, and `!radyo` if you have radio stations). Edit the descriptions via the `avatar.*` lines in Panel → Plugins → *Bot texts*. While playing, the avatar shows the song's cover. To use your own image instead, put `sleep.png` into `bots/<bot-name>/avatars/`.

**How do I change the language?**
There is one language setting: Panel → Settings → General → *Language* (also asked during setup). The panel, the bot's TeamSpeak replies and the command names all use it: Türkçe, English, Deutsch, Español, Français, Русский, Português, Polski, Italiano. Command names are Turkish when Turkish is chosen and English in every other language (`!play`, `!next`, `!help`…). Changing the language resets custom command names to these defaults; the Turkish names always work and every command can be renamed in the panel. Individual replies can be edited in Panel → Plugins → *Bot texts*.

**How do I play my own music files?**
Panel → *Library* → *Upload files* (mp3, ogg, opus, wav, flac, m4a, aac, webm, wma; up to 200 MB each). Files are kept in `music-library/`. Use the buttons on each row to play, queue or add them to a playlist; `!file <name>` works in chat too.

**What can I do with scripts?**
Panel → Plugins → *Scripts*. Write your own commands in JavaScript (`frix.command('hello', (args, ctx) => 'Hi ' + ctx.user)`) or automations that react to events (`frix.on('connect' | 'channel' | 'message' | 'song' | 'tick', …)`). Scripts have no file, network or system access; each call is limited to 2 seconds and 32 MB. The full list is under *API reference* in the editor; errors show in *Script logs*.

**How do I back up / move my settings?**
Panel → Settings → *Backup*. Choose the parts and download one JSON file; *Restore from backup* on another install shows what is inside and restores the parts you pick. Users and passwords, bot connection settings and the library's music files are not included.

**How do I bring the bot to my channel, or see when someone was last online?**
`!come` moves the bot to your channel. `!lastseen <name>` says whether someone is online right now or when they were last seen (everyone the bots see is recorded once a minute).

**Can I run Frix with Docker or Pterodactyl?**
Yes: see the *Docker* and *Pterodactyl* sections of the README. With Docker all data is in the `/data` volume, with Pterodactyl in the server's folder; the panel port is set with `FRIX_WEB_PORT` (automatic on Pterodactyl).
