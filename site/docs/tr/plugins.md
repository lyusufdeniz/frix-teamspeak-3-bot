# Eklentiler

Eklentiler **her bot için ayrı** açılır: Panel → **Eklentiler** → kenar çubuğundan botu seç → bir eklentiye tıkla. Birden çok özelliği olan eklentilerde her özellik kendi anahtarı, ayarları ve komutlarıyla bir karttır. Kanal alanları, bot bir kez bağlandıktan sonra botun kanallarını liste olarak gösterir; kanal adını ya da ID'sini (`5` veya `/5`) yazabilirsin de. Grup alanlarına virgülle ayrılmış sunucu grubu ID'leri yazılır (TeamSpeak → *İzinler → Sunucu Grupları*). Mesaj alanları BBCode destekler ve canlı önizlemeli bir editör açar.

## Müzik

Şarkı çalma ve müzik özellikleri: komutlar, ses eşitleme, otomatik devam, favoriler, oylamayla geçme, DJ modu, yalnızken duraklatma, çalan şarkıyı bot adı / açıklaması yapma.

### Müzik

*varsayılan açık*

YouTube/link ile şarkı çalma: !ara, !link, !liste, !istatistik ve !gec, !dur, !ses gibi kontrol komutları. Kapatırsan bu komutlar devre dışı kalır (kontroller radyo açıksa çalışmaya devam eder).

| Ayar | Varsayılan | Açıklama |
|---|---|---|
| Direkt çal (kuyruğa ekleme) | kapalı | Açıkken !ara ve !link çalan şarkıyı bırakıp yeni şarkıyı hemen çalar. Kuyruktaki diğer şarkılar korunur. |
| Çalan şarkıyı kanala duyur | açık | Her şarkı başladığında kanala 'Şimdi çalıyor' mesajı yazılır (metni: Bot metinleri → nowplaying). |

**TeamSpeak yetkisi:** `b_client_use_channel_commander` (yalnızca bot ayarlarında kanal komutanı açıksa)

### Ses seviyesi eşitleme

*varsayılan açık*

Çok yüksek ya da çok kısık kaydedilmiş şarkıları aynı ses seviyesine çeker. Değişiklik bir sonraki şarkıda başlar.

| Ayar | Varsayılan | Açıklama |
|---|---|---|
| Hedef seviye | normal | kısık / normal / yüksek — quiet: daha kısık, normal: önerilen, loud: daha yüksek |

### Otomatik devam

*varsayılan kapalı*

Kuyruk bitince bot susmaz; son çalan şarkıya benzer şarkıları kendisi seçip çalar. !dur/!bitir ile durdurulursa devam etmez.

| Ayar | Varsayılan | Açıklama |
|---|---|---|
| Sadece kanalda biri varken | açık |  |
| Kanala 'otomatik devam' mesajı yaz | açık |  |
| Son kaç şarkıyı tekrar çalma | `30` |  |

### Favoriler

*varsayılan açık*

!fav çalan şarkıyı kişinin favorilerine ekler, !favs kişinin favorilerini kuyruğa ekler. !favs liste ile listelenir, !fav sil <no> ile silinir.

| Ayar | Varsayılan | Açıklama |
|---|---|---|
| Kişi başı en fazla favori | `200` |  |
| !favs karışık sırayla eklesin | kapalı |  |

### Oylamayla geç

*varsayılan kapalı*

!gec yazıldığında şarkı hemen geçilmez; botun kanalındakilerin belirli bir oranı oy verince geçilir.

| Ayar | Varsayılan | Açıklama |
|---|---|---|
| Gereken oy oranı (%) | `50` |  |
| Şarkıyı açan kişi oylamasız geçebilsin | açık |  |
| Oylamasız geçebilen grup ID'leri | boş |  |

### DJ yetkisi ve kuyruk limiti

*varsayılan kapalı*

Sadece belirli grupların şarkı eklemesine izin verir ve kişi başına kuyruk sınırı koyar.

| Ayar | Varsayılan | Açıklama |
|---|---|---|
| DJ grup ID'leri (boş = herkes ekleyebilir) | boş |  |
| Kişi başına sıradaki en fazla şarkı (0 = sınırsız) | `3` |  |
| Geç/durdur/ses/bitir de sadece DJ'ler | kapalı |  |

### Yalnızken duraklat

*varsayılan kapalı*

Botun kanalında dinleyen kimse kalmayınca müziği duraklatır, biri gelince devam ettirir.

| Ayar | Varsayılan | Açıklama |
|---|---|---|
| Biri gelince devam et | açık |  |
| Bu kadar dakika sonra tamamen durdur (0 = hiç) | `0` |  |

### Şarkı adı → bot adı/açıklaması

*varsayılan kapalı*

Çalan şarkıyı botun TeamSpeak adı ve/veya açıklaması yapar. Çalma bitince ad ve açıklama eski haline döner.

| Ayar | Varsayılan | Açıklama |
|---|---|---|
| Bot adını değiştir | açık |  |
| Ad biçimi | `♪ {title}` | {title}, {artist}, {song}, {user}, {bot} kullanılabilir. TeamSpeak adı en fazla 30 karakter; uzunsa kısaltılır. |
| Açıklamayı değiştir | açık |  |
| Açıklama biçimi | `🎵 {title} · {user}` | {title}, {artist}, {song}, {user}, {bot} kullanılabilir. {artist}: "Sanatçı - Şarkı" başlıklarında sanatçı; {song}: şarkı adı. |
| Boştayken açıklama | boş | Boş bırakırsan açıklama temizlenir. |

**TeamSpeak yetkisi:** `b_client_modify_own_description` (açıklama için)

## Radyo

Panelde eklenen radyolar (!radyo) ve kuyruk boşken otomatik radyo.

### Radyo

*varsayılan açık*

Panelde eklenen radyoları !radyo <isim> ile çalar. Kapatırsan !radyo devre dışı kalır.

| Ayar | Varsayılan | Açıklama |
|---|---|---|
| Direkt çal (kuyruğa ekleme) | açık | Açıkken radyo çalan şarkıyı bırakıp hemen başlar. Kapalıysa radyo kuyruğun sonuna eklenir. |
| Açılan radyoyu kanala duyur | açık | Radyo başladığında kanala mesaj yazılır (metni: Bot metinleri → nowplaying.radio). |

### Boştayken radyo

*varsayılan kapalı*

Kuyruk bitip bot belirli bir süre boş kalınca seçilen radyoyu çalar.

| Ayar | Varsayılan | Açıklama |
|---|---|---|
| Radyo | boş |  |
| Kaç dakika boş kalınca | `5` |  |

## Avatar

Botun avatarı: boşta, müzik çalarken ve radyo çalarken ayrı ayrı (komut kartı, şarkı kapağı ya da kendi resmin).

| Ayar | Varsayılan | Açıklama |
|---|---|---|
| Boştayken | komut kartı | komut kartı / kendi resmim / yok |
| Boştaki resim | — |  |
| Müzik çalarken | şarkı kapağı | şarkı kapağı / kendi resmim / boştaki avatar |
| Müzik resmi | — |  |
| Radyo çalarken | boştaki avatar | boştaki avatar / kendi resmim |
| Radyo resmi | — |  |

**TeamSpeak yetkisi:** resme yetecek `i_client_max_avatar_filesize` (komut kartı yaklaşık 30 KB)

## Otomatik yeniden bağlan

Bot sunucudan düşünce kendiliğinden tekrar bağlanır ve aynı kanala girer.

| Ayar | Varsayılan | Açıklama |
|---|---|---|
| İlk deneme (saniye) | `3` | Sonraki denemeler bu sürenin katlarıyla en fazla 2 dakikaya kadar uzar. |
| En fazla deneme (0 = sınırsız) | `0` |  |
| Aynı kanala geri dön | açık | Kapalıysa bot ayarlardaki varsayılan kanala girer. |
| Sunucudan atılınca da geri gir | açık |  |
| Sunucu kapanıp açılınca tekrar dene | açık |  |

## Karşılama ve duyurular

Gelenlere karşılama mesajı, dönen duyuru/reklam mesajları ve zamanlı duyurular.

### Karşılama mesajı

*varsayılan kapalı*

Sunucuya bağlanan ya da botun kanalına giren kullanıcılara mesaj gönderir.

| Ayar | Varsayılan | Açıklama |
|---|---|---|
| Ne zaman | sunucu | sunucu / kanal / ikisi de — server: sunucuya bağlanınca · channel: botun kanalına girince |
| Gönderim şekli | özel mesaj | özel mesaj / dürtme / kanal |
| Mesaj | `[b][color=#00FF00]👋 Hoş geldin {user}! {serve…` | Değişkenler: {user} {server} {channel} {time} {date} |
| Aynı kişiye tekrar (dakika) | `60` |  |

**TeamSpeak yetkisi:** `i_client_poke_power` (yalnızca *dürtme* ile gönderimde)

### Duyurular

*varsayılan kapalı*

Belirli aralıklarla dönen duyuru/reklam mesajları ve belirli gün ve saatlerde gönderilen zamanlı duyurular.

| Ayar | Varsayılan | Açıklama |
|---|---|---|
| Dönen mesajlar (her satır bir mesaj) | `[b][color=#00FF00]🎵 Müzik botunu kullanmak iç…` | Boş bırakırsan sadece zamanlı duyurular gönderilir. |
| Aralık (saniye) | `1800` | En az 30 saniye. Örn: 1800 = 30 dakika. |
| Sıra | sırayla | sırayla / rastgele |
| Nereye | kanal | kanal / sunucu |
| Zamanlı duyurular (her satır: [günler] SS:DD \| mesaj) | boş | Örn: 21:00 \| 🎮 Akşam etkinliği başlıyor! — Cmt,Paz 12:00 \| ☀️ İyi hafta sonları! Günler: pzt sal çar per cum cmt paz; boş = her gün. |

**TeamSpeak yetkisi:** `b_client_server_textmessage_send` (yalnızca tüm sunucuya mesajda)

## Zamanlanmış çalma

Belirli gün ve saatlerde radyo / çalma listesi başlatır, durdurur veya sesi ayarlar.

| Ayar | Varsayılan | Açıklama |
|---|---|---|
| Plan | plan düzenleyici | İşlemler: radyo <ad>, liste <ad>, dur, ses <0-100>. Günler: pzt sal çar per cum cmt paz; boş = her gün. |

## Moderasyon

AFK taşıyıcı, takma ad filtresi, kanal koruma ve flood koruması.

### AFK taşıyıcı

*varsayılan kapalı*

Belirli süre boşta kalan, 'uzakta' olan ya da hoparlörünü kapatan kullanıcıları AFK kanalına taşır; dönünce geri alır.

| Ayar | Varsayılan | Açıklama |
|---|---|---|
| AFK kanalı | `AFK` |  |
| Boşta kalma süresi (dakika, 0 = kapalı) | `15` |  |
| 'Uzakta' durumundakileri taşı | açık |  |
| Hoparlörü kapalı olanları taşı | kapalı |  |
| Dönünce eski kanalına geri al | açık |  |
| Hariç tutulan sunucu grubu ID'leri (virgülle) | boş | Örn: 6, 9 (yöneticiler taşınmaz) |
| Taşınana özel mesaj (boş = gönderme) | `💤 AFK olduğun için AFK kanalına taşındın.` |  |

**TeamSpeak yetkisi:** kullanıcıların `i_client_needed_move_power` değerinden yüksek `i_client_move_power`

### İsim filtresi

*varsayılan kapalı*

Takma adında yasaklı kelime geçen kullanıcıları uyarır ya da sunucudan atar.

| Ayar | Varsayılan | Açıklama |
|---|---|---|
| Yasaklı kelimeler (her satıra bir tane) | boş |  |
| Ne yapılsın | dürtme | dürtme / at |
| Mesaj | `⚠️ Takma adın sunucu kurallarına uymuyor, lüt…` |  |
| Hariç tutulan sunucu grubu ID'leri | boş |  |

**TeamSpeak yetkisi:** `i_client_poke_power`, *at* için `i_client_kick_from_server_power`

### Kanal koruma

*varsayılan kapalı*

Korunan kanallara izinli gruplar dışından girenleri dışarı taşır ya da kanaldan atar.

| Ayar | Varsayılan | Açıklama |
|---|---|---|
| Korunan kanallar | boş |  |
| İzinli grup ID'leri (virgülle) | boş |  |
| Ne yapılsın | at | at / taşı |
| Taşınacak kanal (taşı seçiliyse) | boş |  |
| Kişiye mesaj | `⛔ Bu kanala girme yetkin yok.` |  |

**TeamSpeak yetkisi:** `i_client_move_power` (taşıma) ya da `i_client_kick_from_channel_power` (atma)

### Flood koruması

*varsayılan kapalı*

Botun gördüğü sohbette kısa sürede çok mesaj atanı uyarır, tekrarlarsa atar (bot kanal ve özel mesajları görür).

| Ayar | Varsayılan | Açıklama |
|---|---|---|
| En fazla mesaj | `5` |  |
| Kaç saniyede | `8` |  |
| Atmadan önce uyarı sayısı | `2` |  |
| Son adım | kanal | kanal / sunucu — channel: kanaldan at · server: sunucudan at |
| Uyarı (dürtme) | `⚠️ Lütfen flood yapma! ({count}/{max})` |  |
| Atma sebebi | `Flood` |  |
| Hariç tutulan grup ID'leri | boş |  |

**TeamSpeak yetkisi:** `i_client_poke_power`, `i_client_kick_from_channel_power` / `i_client_kick_from_server_power`

## Sunucu araçları

Kayıt botu, geçici kanal oluşturucu, kanal adında bilgi ve destek bildirimi.

### Kayıt botu

*varsayılan kapalı*

Kayıt kanalına giren kayıtsızları yetkililere bildirir; yetkili !kayit <isim> yazınca üye grubunu verir ve lobiye taşır.

| Ayar | Varsayılan | Açıklama |
|---|---|---|
| Kayıt kanalı | `Kayıt` |  |
| Verilecek üye grubu ID'si | boş |  |
| Kaldırılacak kayıtsız grubu ID'si (boş = yok) | boş |  |
| Kayıt yapabilen yetkili grup ID'leri (virgülle) | boş |  |
| Kayıttan sonra taşınacak kanal (boş = taşıma) | boş |  |
| Yetkililere dürtme | `📝 {user} kayıt bekliyor → !kayit {user}` |  |
| Bekleyen kişiye mesaj | `👋 Hoş geldin {user}! Yetkililer bilgilendiril…` |  |
| Kayıt sonrası mesaj | `✅ Kaydın tamamlandı, aramıza hoş geldin {user}!` |  |
| Aynı kişi için bekleme (dakika) | `3` |  |

**TeamSpeak yetkisi:** `i_group_member_add_power`, `i_group_member_remove_power`, `i_client_move_power`, `i_client_poke_power`

### Geçici kanal oluşturucu

*varsayılan kapalı*

'Kanal oluştur' kanalına giren kişiye kendi adıyla geçici bir kanal açar ve taşır; kanal boşalınca TeamSpeak siler.

| Ayar | Varsayılan | Açıklama |
|---|---|---|
| Oluşturucu kanal | `➕ Kanal oluştur` |  |
| Kanalların açılacağı üst kanal (boş = oluşturucunun üstü) | boş |  |
| Kanal adı şablonu | `🔊 {user} kanalı` | Değişkenler: {user} {n} |
| En fazla kişi (0 = sınırsız) | `0` |  |
| Aynı kişi için bekleme (saniye) | `30` |  |

**TeamSpeak yetkisi:** `b_channel_create_temporary`, `i_client_move_power`

### Kanal adında bilgi

*varsayılan kapalı*

Seçilen kanalın adını çevrimiçi kişi sayısı ve saatle günceller (botun kanal düzenleme yetkisi gerekir).

| Ayar | Varsayılan | Açıklama |
|---|---|---|
| Kanal | boş |  |
| Kanal adı şablonu | `🎧 Çevrimiçi: {online} \| {time}` | Değişkenler: {online} {record} {max} {time} {date} — en fazla 40 karakter. {record}: sunucuda aynı anda görülen en yüksek çevrimiçi sayısı. |
| Güncelleme aralığı (saniye) | `60` | En az 10 saniye. |

**TeamSpeak yetkisi:** o kanalda `b_channel_modify_name`

### Destek bildirimi

*varsayılan kapalı*

Destek kanalına biri girince yetkili gruplardaki çevrimiçi kişilere dürtme gönderir.

| Ayar | Varsayılan | Açıklama |
|---|---|---|
| Destek kanalı | `Destek` |  |
| Yetkili sunucu grubu ID'leri (virgülle) | boş |  |
| Yetkililere giden mesaj | `🆘 {user} destek kanalında bekliyor.` |  |
| Kullanıcıya giden mesaj | `👋 Yetkililere haber verildi ({count} kişi çev…` |  |
| Yetkili yoksa kullanıcıya | `😔 Şu an çevrimiçi yetkili yok, lütfen daha so…` |  |
| Aynı kişi için bekleme (dakika) | `5` |  |

**TeamSpeak yetkisi:** `i_client_poke_power`

## Aktivite

Çevrimiçi kişi sayısını kaydeder (dashboard grafiği) ve sunucuda geçirilen süreye göre rütbe verir.

### Sunucu aktivitesi

*varsayılan açık*

Sunucudaki çevrimiçi kişi sayısını kaydeder; dashboard'da son 24 saat grafiği ve en yoğun saatler görünür.

| Ayar | Varsayılan | Açıklama |
|---|---|---|
| Kaç gün saklansın | `90` |  |

### Aktiflik rütbeleri

*varsayılan kapalı*

Sunucuda kalma süresini sayar ve belirlenen saatlere ulaşanlara sunucu grubu verir. Süreler !sure ve !top ile görülür. Sadece bir botta açın.

| Ayar | Varsayılan | Açıklama |
|---|---|---|
| Rütbeler | rütbe düzenleyici | Örn: 10=12 → 10 saate ulaşana 12 numaralı grup verilir |
| Yeni rütbe gelince eskisini kaldır | açık |  |
| Rütbe verilmeyecek grup ID'leri | boş |  |
| Rütbe atlayana özel mesaj | `🎖️ Tebrikler {user}! {hours} saate ulaştın ve…` |  |

**TeamSpeak yetkisi:** `i_group_member_add_power` (önceki rütbeyi kaldırmak için `i_group_member_remove_power`)

## Eğlence ve araçlar

!zar, !yazitura, !sec, !anket, !hava, !doviz ve !soz komutları.

| Ayar | Varsayılan | Açıklama |
|---|---|---|
| !zar, !yazitura, !sec | açık |  |
| !anket ve !oy | açık |  |
| Anket süresi (dakika) | `2` |  |
| !hava | açık |  |
| !doviz, !dolar, !euro | açık |  |
| Döviz karşılığı para birimi | `TRY` |  |
| !soz (şarkı sözleri) | açık |  |
