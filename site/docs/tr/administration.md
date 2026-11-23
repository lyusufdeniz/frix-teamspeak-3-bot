# Yönetim

## Kullanıcılar ve roller

Panel → **Kullanıcılar**. Paneli kullanacak herkes için hesap aç ve bir rol ver: *yönetici* (her şey), *DJ* (müzik,
radyolar, çalma listeleri) ya da *izleyici* (yalnızca bakma). Her kullanıcı kendi şifresini hesap menüsünden değiştirebilir;
yönetici diğerlerinin şifresini sıfırlayabilir.

Panel kullanıcıları TeamSpeak kullanıcılarından ayrıdır. TeamSpeak'te kimin sohbet komutlarını kullanabileceğine eklentiler
(ör. DJ modu, kayıt botunun yetkili grupları) ve TS3AudioBot'un kendi komutları için `rights.toml` karar verir.

## Birden çok bot

Panel → **Botlar → Yeni bot** başka bir TeamSpeak istemcisi ekler; örneğin bir müzik botu ve bir moderasyon botu. Her botun
kendi eklentileri vardır. Bazı özellikler sunucu başına **tek** botta açık olmalı: *Aktiflik rütbeleri* (süre iki kez
sayılır) ve *Sunucu aktivitesi*.

## Yedek alma ve geri yükleme

Panel → **Ayarlar → Yedekleme**.

- **Yedek al**: bölümleri seç ve tek bir JSON dosyası indir. Bölümler:
  - eklenti ayarları, bot metinleri, komutlar, radyolar,
  - çalma listeleri ve favoriler, genel ayarlar, avatar resimleri,
  - istatistikler (isteğe bağlı; büyük olabilir).
- **Yedekten geri yükle**: bir yedek dosyası seç. Frix içinde ne olduğunu ve hangi sürümle alındığını gösterir. Bölümleri sen
  seçersin; seçtiğin bölümlerin şu anki içeriği yedektekiyle değiştirilir.

Panel kullanıcıları ve şifreleri, botların bağlantı ayarları ve kütüphanedeki müzik dosyaları yedeğe girmez. Her şeyi başka
bir makineye taşımak için Frix kapalıyken tüm Frix klasörünü (ya da Docker birimini) kopyala.

## Güncellemeler

Panel → **Ayarlar → Güncellemeler → Güncellemeleri denetle**, kurulu ve en son sürümü sürüm notlarıyla gösterir.
*Şimdi güncelle* paketi ilerleme çubuğuyla indirir ve yeniden başlatmada kurar. Güncellemeler her zaman resmi depodan gelir.
Docker kullanıcıları yeni imajı da çekebilir; Pterodactyl kullanıcıları sunucuyu yeniden kurabilir.

## YouTube

YouTube sık değişir ve bazen sunucuları engeller.

- **yt-dlp güncelle** (Ayarlar → YouTube), "ses yok" ve "video kullanılamıyor" sorunlarının çoğunu çözer.
- **YouTube çerezleri**: log *Sign in to confirm you're not a bot* diyorsa, YouTube'a giriş yapmışken *Get cookies.txt
  LOCALLY* tarayıcı eklentisiyle youtube.com için `cookies.txt` dışa aktar ve Ayarlar → YouTube'dan yükle. Tercihen ikinci bir
  Google hesabı kullan.

## Dosyalar ve klasörler

Her şey Frix klasöründe durur (Docker: `/data`):

| Yol | İçerik |
|---|---|
| `frix.db` | Panel kullanıcıları, eklenti ayarları, metinler, komutlar, radyolar, çalma listeleri, istatistikler |
| `bots/<ad>/bot.toml` | Her botun bağlantı ayarları |
| `ts3audiobot.toml`, `rights.toml` | Temel ayarlar ve TS3AudioBot komut yetkileri |
| `music-library/` | Yüklenen müzik dosyaları |
| `scripts/` | Scriptler (`.js`) |
| `avatars/` | Kendi avatar resimlerin |
| `logs/` | Log dosyaları |
| `ytdlp/` | yt-dlp ve ayar dosyası |
| `youtube_cookies.txt` | Yüklenen YouTube çerezleri |
| `KURULUM_KODU.txt` | Tek kullanımlık kurulum kodu (kurulumdan sonra silinir) |

## Portlar ve ortam

| Ayar | Varsayılan | Açıklama |
|---|---|---|
| Panel portu | `58913` | `ts3audiobot.toml` → `[web] port`, ya da `FRIX_WEB_PORT` ortam değişkeni (Docker, Pterodactyl) |
| Dinlenen adresler | hepsi | `ts3audiobot.toml` → `[web] hosts` |
| TeamSpeak | — | Bot, sunucunun ses portuna dışarı doğru bağlanır (varsayılan `9987/udp`) |

## Güvenlik

- Şifreler PBKDF2-SHA256 özetleri olarak saklanır. Oturumlar HttpOnly ve SameSite=Strict çerezler ile CSRF kontrolü kullanır.
- 5 hatalı girişten sonra adres 10 dakika kilitlenir.
- Başka bir bilgisayardan ilk kurulum, tek kullanımlık kurulum kodunu gerektirir.
- Panel düz HTTP konuşur. İnternetten erişilecekse HTTPS'li bir ters vekil kullan ([örnek](#installation)).
