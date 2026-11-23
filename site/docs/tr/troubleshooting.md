# Sorun giderme

Cevapların çoğu Panel → **Loglar**'dadır: sarı satırlar uyarı, kırmızı satırlar hatadır.

## Ses ve müzik

**Şarkı bulunuyor ama çalmıyor.** Sebebi logda ara. Genelde YouTube engelliyordur: Ayarlar → YouTube → *yt-dlp güncelle*;
işe yaramazsa YouTube çerezlerini yükle ([nasıl](#administration)). Bot henüz bağlanıyorken ses gönderilmez.

**"Sign in to confirm you're not a bot".** YouTube sunucunun IP adresini engelliyor. YouTube çerezlerini yükle.

**ffmpeg şarkıyı çalamadı.** Sonraki log satırı yt-dlp'nin sebebini gösterir (yaş sınırı, bölge kısıtı, kaldırılmış video,
bitmiş canlı yayın…).

**Bazı şarkılar diğerlerinden çok yüksek.** Müzik eklentisinde *Ses seviyesi eşitleme*'yi aç; bir sonraki şarkıdan itibaren
uygulanır.

**Bir radyo çalmıyor.** Web sayfası değil, doğrudan yayın adresi (`.mp3`, `.aac`, Icecast/Shoutcast) ya da `.m3u` / `.pls` /
`.m3u8` linki kullan. Önce VLC'de dene.

**Linux'ta ses yok / "libopus".** `sudo apt install libopus0 ffmpeg` (Debian/Ubuntu). `install.sh` ve Docker imajında
bunlar hazır gelir.

## Bağlantı

**Bot bağlanamıyor.** `adres:port`'u (varsayılan port 9987) ve sunucu şifresini kontrol et. İlk bağlantıda sunucu bir
kimlik güvenlik seviyesi isteyebilir; bot bunu hesaplar (10–60 saniye). Sebebi logda yazar. Bağlandıktan sonra
[bağlantı durumu](#panel) penceresi ping ve paket kaybını gösterir.

**Bot "Frix1" olarak görünüyor.** Aynı adla başka bir istemci çevrimiçi (eski bir bot ya da eski oturum düşmeden yapılan
yeniden bağlantı). Bot bir sonraki bağlantıda adını geri alır.

**Bot düşüyor ve geri gelmiyor.** *Otomatik yeniden bağlan* eklentisini aç. Zaman aşımı, sunucu yeniden başlaması ve (izin
verilirse) atılma sonrası yeniden dener; banlanınca denemez.

## Panel

**Panele dışarıdan erişilemiyor.** TCP 58913'ü güvenlik duvarında (`ufw allow 58913/tcp`, Windows Güvenlik Duvarı) ve bulut
sağlayıcının ağ kurallarında aç.

**Yönetici şifremi unuttum.** Başka bir yönetici Panel → Kullanıcılar'dan sıfırlayabilir. Başka yönetici yoksa Frix'i
durdur, `frix.db`'yi sil ve yeniden başlat. Eklenti ayarları, metinler, radyolar ve çalma listeleri gider; bunları bir
yedekten geri yükleyebilirsin. Botlar kalır.

**Güncellemeden sonra bir açılır liste ya da sayfa eski görünüyor.** Sayfayı Ctrl+F5 ile yenile.

## Eklentiler

**Bir komut "bu özellik kapalı" diyor.** Bu bot için eklentiyi ve özelliği aç (Panel → Eklentiler).

**AFK taşıyıcı kimseyi taşımıyor.**
- AFK kanalı mevcut olmalı; listeden seç.
- Botun taşıma gücü kullanıcılarınkinden yüksek olmalı.
- Sunucu yöneticileri taşınamaz; gruplarını *Hariç tutulan gruplar*'a ekle.
- Kontroller 30 saniyede bir yapılır.

**İsim filtresi yanlış kişileri yakalıyor.**
- Kelimeler takma adın herhangi bir yerinde, büyük/küçük harf ayırmadan eşleşir.
- Çok kısa kelimelerden kaçın ve muaf grupları ekle.
- *Dürt* ile başla, emin olunca *at*'a geç.

**Kanal adı değişmiyor.** Botun o kanalda `b_channel_modify_name` yetkisi olmalı. Adlar 40 karakterde kesilir. Kanalın adı
değiştiği için onu listeden (ID ile) seç.

**Aktiflik rütbeleri saymıyor / `!top` boş.** Rütbeleri aç; yalnızca tek botta. Süre dakikada bir eklenir. Rütbeler, rütbe
düzenleyicide saat → sunucu grubu olarak yazılır.

**Geçici kanal oluşmuyor.** Botun `b_channel_create_temporary` ve taşıma yetkisi olmalı. Bir kullanıcı 30 saniyede en fazla
bir kanal alır.

**Bot açılınca karşılama mesajı herkese gidiyor.** Gitmiyor: bağlandıktan sonraki ilk 15 saniyedeki girişler yok sayılır ve
bir kullanıcı tekrar süresi dolmadan yeniden karşılanmaz.

**Avatar değişmiyor.** Sunucu avatar dosya boyutunu sınırlar (`i_client_max_avatar_filesize`); log, *Avatar yüklenemedi*
satırında sebebini yazar.

**Kapalı eklentinin komutları hâlâ çalışıyor.** Son sürüme güncelle; 2.16.3'ten beri TS3AudioBot'un kendi komutları
(`!play`, `!next`…) da eklenti anahtarlarına bağlı.

## TeamSpeak yetkileri

Her özelliğin ihtiyaç duyduğu yetkiler [Eklentiler](#plugins) sayfasında özelliğin altında yazar. En hızlı kurulum, bot için
bir sunucu grubu açıp özelliklerinin kullandığı taşıma, dürtme, atma ve grup verme yetkilerini vermektir. Bu grubun
`i_client_move_power` / `i_group_member_add_power` değerleri, yöneteceği gruplarınkinden yüksek olmalı.
