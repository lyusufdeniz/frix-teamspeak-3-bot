# Web panel

Panel tarayıcıda tek bir sayfadır. Sol kenar çubuğunda şunlar var:
- **bot seçici** (sayfaların üzerinde çalıştığı bot),
- sayfalar,
- en altta mini oynatıcı, sürüm, GitHub bağlantısı ve destek düğmesi.

Üst çubukta sayfa başlığı ve işlemleri, açık/koyu tema düğmesi ve hesap menün var. Telefonda kenar çubuğu menüye dönüşür.

## Genel bakış

- **Kartlar**:
  - açık botlar,
  - bugün ve bu hafta çalınanlar (değişimle birlikte),
  - çevrimiçi kişiler,
  - bağlantı kalitesi,
  - çalışma süresi.

  Her kartta ilgili sayfaya götüren bir *aç* düğmesi var.
  - **Çevrimiçi** kartına tıklayınca kimin çevrimiçi olduğu listelenir (ad, kanal, uzakta / mikrofon / ses durumu).
  - **Bağlantı** kartına tıklayınca [bağlantı durumu](#panel) penceresi açılır.
- Üst çubuktaki **Seçili bot / Tüm botlar** seçimi: genel bakış kenar çubuğunda seçili botu ya da tüm botları birlikte gösterir.
- **Sistem**: son 3 dakikanın sunucu CPU'su ve Frix CPU'su, RAM, Frix belleği ve iş parçacıkları.
- **Sunucu aktivitesi**: son 24 saatin çevrimiçi kişi grafiği ve en yoğun saatlerin ısı haritası. Aktivite eklentisinin
  *Sunucu aktivitesi* özelliği gerekir. Kapalıyken kart bulanık görünür ve nasıl açılacağını yazar.
- **Son 14 gün**, **En çok çalınanlar** ve bot listesi.

### Bağlantı durumu

Her bot için ayrı, 2 saniyede bir yenilenir:
- ping (anlık, ortalama, dalgalanma) ve paket kaybı,
- indirme / yükleme hızı ve son dakikanın trafiği,
- ping geçmişi grafiği,
- bağlantı süresi, sunucu sürümü ve platformu, kişi sayısı,
- botun adı ve kodekle birlikte kanalı,
- ses bit hızı ve paket sayıları.

## Botlar

Her bot için bir kart: durum, sunucu, kanal, ses kalitesi, çalan şarkı ve açık eklentiler. Botları buradan başlatır,
durdurur, yeniden başlatır, düzenler ya da silersin. Bot **ayarları**, [Bot ekleme](#installation) bölümündeki alanlardır.
Bot bir kez bağlandıktan sonra kanal listeden seçilir.

## Çalar

Seçili botun oynatıcısı:
- arama (küçük resimli YouTube sonuçları), linkle ekleme, hemen çal ya da kuyruğa ekle,
- sıralanabilen ve silinebilen kuyruk,
- sarma, ses, duraklatma, geçme, tekrar ve karışık çalma.

Şarkı yüklenirken "şarkı ekleniyor" ilerlemesi görünür.

## Listeler

Kayıtlı çalma listeleri: aç, çal, şarkı çıkar. Listeler TeamSpeak'te `!liste kaydet <ad>` ile ve kütüphaneden de oluşturulur.

## Radyo

İnternet radyoları: ad, yayın adresi ve tür.
- Tek tek ekleyebilir ya da bir `.m3u` / `.m3u8` listesini **içe aktarabilirsin** (türler `group-title`'dan okunur).
- Radyo, seçili botta *Çal* ile ya da TeamSpeak'te `!radyo <isim>` ile çalar.
- Web sayfası değil, doğrudan yayın adresi kullan: `.mp3`, `.aac`, Icecast/Shoutcast, `.m3u`, `.pls`, `.m3u8`.

## Kütüphane

Kendi müzik dosyaların:
- yükle (aynı anda birden çok; dosya başına 200 MB'a kadar — mp3, ogg, opus, wav, flac, m4a, aac, webm, wma),
- ara, çal, kuyruğa ekle, çalma listesine ekle,
- yeniden adlandır ve sil.

Dosyalar `music-library` klasöründe saklanır. TeamSpeak'te: `!dosya <ad>`.

## Eklentiler

Seçili botun 10 eklentisi üç grupta durur: Temel, Topluluk, Ekstralar.
- Anahtar eklentiyi açar ya da kapatır.
- Eklentiye tıklamak, her özellik için bir kart içeren penceresini açar. Her kartta:
  - kendi anahtarı ve ayarları,
  - *Diğer ayarlar* altında az kullanılan ayarlar,
  - özelliğin adını değiştirebileceğin komutları.
- **Genel komutlar** kartında hiçbir eklentiye bağlı olmayan komutlar var.

Ayrıntılar: [Eklentiler](#plugins).

Kenar çubuğundaki alt sayfalar: **Özel komutlar**, **Bot metinleri** ve **Scriptler** — bkz. [Metinler ve komutlar](#customization)
ve [Scriptler](#scripts).

## Kullanıcılar

Panel hesapları ve rolleri:

| Rol | Yapabildikleri |
|---|---|
| Yönetici | Her şey: botlar, eklentiler, kullanıcılar, ayarlar, güncellemeler |
| DJ | Şarkı ve radyo ekleme, çalmayı yönetme, çalma listeleri |
| İzleyici | Yalnızca bakma |

## Ayarlar

Açılır kapanır bölümlerden oluşan tek bir sayfa:
- **Genel**: dil, tema.
- **YouTube**: yt-dlp güncelleme, çerez yükleme.
- **Güncellemeler**.
- **Yedekleme**: yedek alma ve geri yükleme.
- **Sistem**: sürüm, işletim sistemi, yeniden başlatma.

## Loglar ve istatistikler

**Loglar**:
- canlı log,
- önceki log dosyaları,
- işlem kaydı (panelde kim neyi değiştirdi).

Logları temizlemek log dosyalarını da siler.

**İstatistik**: en çok çalınanlar, en çok şarkı açanlar ve en aktif kullanıcılar. 7 gün, 30 gün, 1 yıl ya da tüm zaman
için; tek bot ya da tüm botlar için.
