# Kurulum

Sistemine uygun paketi [son sürümden](https://github.com/lyusufdeniz/frix-teamspeak-bot/releases/latest) indir ya da
Docker / Pterodactyl kullan. Tüm paketlerde bot, web panel, ffmpeg (Windows) ve yt-dlp hazır gelir.

## Windows

1. `Frix-win-x64.zip` dosyasını indir ve örneğin `C:\Frix` klasörüne çıkar.
2. **`Frix.exe`**'yi çalıştır. Sistem tepsisinde Frix tilki simgesi çıkar; konsol penceresi açılmaz.
3. **http://localhost:58913** adresini aç ve yönetici hesabını oluştur.

Tepsi simgesinden paneli açar, botu yeniden başlatır, *Windows ile başlat*'ı (varsayılan açık) açıp kapatır ve çıkarsın.
Bot çökerse kendiliğinden yeniden başlatılır. `konsol-modu.bat` botu konsol penceresiyle başlatır; hata ararken işe yarar.

> Kimse oturum açmadan yeniden başlayan bir sunucuda tepsi uygulamasının açılması için Windows otomatik oturum açmayı
> (`netplwiz`) etkinleştir.

## Linux

```bash
wget https://github.com/lyusufdeniz/frix-teamspeak-bot/releases/latest/download/Frix-linux-x64.zip
unzip Frix-linux-x64.zip && cd Frix-linux-x64
sudo bash install.sh
```

ARM sunucularda (Raspberry Pi 4/5, Ampere…) `Frix-linux-arm64.zip` kullan. `install.sh` şunları yapar:

- `ffmpeg` ve `libopus` kurar (apt, dnf, pacman ya da apk),
- `frix` sistem kullanıcısını oluşturur ve botu `/opt/frix`'e kopyalar (başka klasör: `sudo bash install.sh /yol`),
- `frix` systemd servisini etkinleştirip başlatır,
- panel adresini ve tek kullanımlık kurulum kodunu yazar.

```bash
systemctl status frix        # durum
journalctl -u frix -f        # loglar (panelde de var)
sudo systemctl restart frix  # yeniden başlat
sudo ufw allow 58913/tcp     # güvenlik duvarı kullanıyorsan panel portunu aç
```

Yeni bir paketten `install.sh`'yi tekrar çalıştırmak Frix'i günceller, verilerin kalır.

## Docker

```bash
docker run -d --name frix --restart unless-stopped \
  -p 58913:58913 -v frix-data:/data \
  ghcr.io/lyusufdeniz/frix:latest
```

- Tüm veriler (veritabanı, botlar, kütüphane, scriptler, avatarlar) `/data` biriminde durur.
- İmajlar `amd64` ve `arm64` için, sürüm (`2.16.7`) ve `latest` etiketleriyle yayınlanır.
- `FRIX_WEB_PORT` konteyner içindeki panel portunu değiştirir; `-p` eşlemesini de buna göre ayarla.
- Depoda hazır bir [`docker-compose.yml`](https://github.com/lyusufdeniz/frix-teamspeak-bot/blob/main/docker-compose.yml) var.
- Güncelleme: `docker pull ghcr.io/lyusufdeniz/frix:latest` ve konteyneri yeniden oluştur. Veriler birimde kalır.

## Pterodactyl

1. [`egg-frix.json`](https://github.com/lyusufdeniz/frix-teamspeak-bot/blob/main/packaging/pterodactyl/egg-frix.json) dosyasını indir.
2. Pterodactyl → **Admin → Nests → Import Egg**.
3. *Frix TeamSpeak Bot* egg'iyle bir sunucu oluştur. Panel sunucunun **ana portunda** çalışır.

*Frix release* değişkeni sürümü seçer (`latest` ya da `v2.16.7` gibi bir etiket). Sunucuyu yeniden kurmak Frix'i günceller,
veriler kalır.

## İlk açılış

Paneli tarayıcıda aç:

- **Aynı bilgisayardan** (`localhost`): kurulum sihirbazı doğrudan açılır.
- **Başka bir bilgisayardan**: sihirbaz 8 karakterlik bir **kurulum kodu** ister. Kod loga yazılır ve Frix klasöründeki
  `KURULUM_KODU.txt` dosyasına kaydedilir (Linux: `/opt/frix`, Docker: `docker logs frix`). Kurulumdan sonra dosya silinir.

Sihirbaz dili, yönetici kullanıcı adını ve şifresini sorar. Seçilen dil panelin, botun cevaplarının ve komut adlarının
dilini belirler.

## Bot ekleme

Panel → **Botlar → Yeni bot**.

| Alan | Ne yazılır |
|---|---|
| Ad | Kısa iç ad (harf, rakam, `-`, `_`), ör. `muzik` |
| Takma ad | TeamSpeak'te görünen ad (3–30 karakter) |
| Sunucu adresi | `adres:port`, ör. `ts.ornek.com:9987` ya da `localhost:9987` |
| Kanal | Girilecek kanal. İlk bağlantıdan sonra kanallar liste olarak gelir |
| Sunucu şifresi | Yalnızca sunucuda şifre varsa |
| Ses kalitesi | 32 / 48 / 64 / 96 kbps — 64 önerilir |
| Kanal komutanı | Botu kanal komutanı olarak gösterir (yetki gerekir) |
| Otomatik başlat | Botu Frix ile birlikte başlatır |

İlk bağlantıda sunucu bir kimlik güvenlik seviyesi isteyebilir; bot bunu kendisi hesaplar (10–60 saniye sürebilir).
Ardından bu bot için ihtiyacın olan [eklentileri](#plugins) aç.

## Dışarıdan erişim ve HTTPS

Panel tüm ağ arayüzlerinde TCP **58913** portunu dinler. Dışarıdan erişmek için bu portu güvenlik duvarında ve bulut
sağlayıcının ağ kurallarında aç. Bağlantı düz HTTP'dir; internet üzerinden erişim için HTTPS'li bir ters vekil (reverse
proxy) kullan, örneğin Caddy:

```
frix.ornek.com {
  reverse_proxy 127.0.0.1:58913
}
```

## Güncelleme

Panel → **Ayarlar → Güncellemeler → Güncellemeleri denetle**. Yeni sürüm ilerleme çubuğuyla indirilir ve bir sonraki
yeniden başlatmada kurulur (Windows tepsi uygulaması, Linux servisi ve Docker hepsi uygular). Verilerin asla üzerine yazılmaz.
