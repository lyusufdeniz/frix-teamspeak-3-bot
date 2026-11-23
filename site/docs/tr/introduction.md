# Giriş

Frix, **TeamSpeak 3 ve TeamSpeak 6** sunucuları için kendi sunucunda çalışan bir bottur. Sunucuna normal bir istemci gibi
bağlanır ve sesi doğrudan Opus kodeğiyle gönderir; bu yüzden TeamSpeak istemcisi, VLC ya da sanal ses kablosu gerekmez.
Windows ya da Linux sunucuda ekransız, Docker'da veya Pterodactyl'da çalışır.

Her şey tarayıcıdaki **web panelden** yönetilir: botlar, müzik, radyolar, eklentiler, metinler, kullanıcılar ve güncellemeler.

## Neler yapabilir

| Alan | Öne çıkanlar |
|---|---|
| Müzik | YouTube araması ve linkleri, canlı yayınlar (YouTube, Twitch), SoundCloud, internet radyoları, kendi yüklediğin dosyalar, çalma listeleri, favoriler, otomatik devam, ses eşitleme, oylamayla geç, DJ modu |
| Moderasyon | AFK taşıyıcı, isim filtresi, kanal koruma, flood koruması |
| Sunucu araçları | Kayıt botu, geçici kanallar, canlı kanal adları (çevrimiçi sayısı, rekor, saat), destek bildirimi |
| Topluluk | Karşılama mesajları, dönen ve zamanlı duyurular, aktiflik rütbeleri, anketler, eğlence komutları |
| Otomasyon | Zamanlanmış çalma, JavaScript scriptleri, özel komutlar |
| Panel | Genel bakış, bağlantı durumu, istatistikler, kullanıcılar ve roller, yedek alma / geri yükleme, tek tıkla güncelleme, 9 dil |

## Nasıl düzenlenmiş

- **Botlar** — tek bir Frix kurulumu birden çok bot çalıştırabilir. Her bot kendi sunucu adresi, kanalı ve adıyla ayrı
  bir TeamSpeak istemcisidir (ör. bir müzik botu ve bir moderasyon botu).
- **Eklentiler** — her bot için ayrı açılan 10 eklenti. Bir eklenti benzer özellikleri toplar; her özelliğin kendi
  anahtarı, ayarları ve komutları vardır. Bkz. [Eklentiler](#plugins).
- **Komutlar** — TeamSpeak'teki sohbet komutları (`!ara`, `!radyo`…). Bir komut yalnızca eklentisi açıkken çalışır.
  Bkz. [Komutlar](#commands).
- **Panel kullanıcıları** — web paneli açabilen kişiler; *yönetici*, *DJ* ve *izleyici* rolleri vardır. TeamSpeak
  kullanıcılarından ayrıdır.

## Gereksinimler

- Botun bağlanabileceği bir TeamSpeak 3 ya da TeamSpeak 6 sunucusu (adres ve varsa sunucu şifresi).
- **Windows** 10 / 11 / Server (x64), ya da **Linux** x64 / arm64 (Debian, Ubuntu, Fedora, Arch, Alpine), ya da **Docker**.
- Kurulum başına yaklaşık 150 MB RAM, bot başına biraz daha. ffmpeg, yt-dlp ve Opus pakette gelir ya da otomatik kurulur.
- Panel için boş bir TCP portu (varsayılan **58913**).

## Sonraki adımlar

1. [Frix'i kur](#installation) ve paneli aç.
2. Sunucu adresinle bir bot ekle.
3. İhtiyacın olan [eklentileri](#plugins) aç.
4. TeamSpeak'te `!yardim` yaz.
