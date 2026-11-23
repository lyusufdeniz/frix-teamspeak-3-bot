# Metinler ve komutlar

## Dil

Tek bir dil ayarı var: Panel → **Ayarlar → Genel → Dil** (kurulum sihirbazında da sorulur). Panelin, botun cevaplarının
ve varsayılan komut adlarının dilini belirler. Diller: Türkçe, English, Deutsch, Español, Français, Русский, Português,
Polski, Italiano.

Komut adları Türkçe seçilirse Türkçe, diğer tüm dillerde İngilizce gelir. Dil değişince yeniden adlandırdığın komutlar bu
varsayılanlara döner. Türkçe adlar her zaman çalışır.

## Bot metinleri

Panel → Eklentiler → **Bot metinleri**, botun gönderebileceği tüm cevapları önizlemeleriyle listeler. Tüm botlarda kapalı
olan eklentilere ait metinler soluk görünür, *Eklenti kapalı* etiketi taşır ve listenin sonunda durur.

- **Mesaj görünümü** (üstteki kart): tüm cevapların varsayılan rengi, hata rengi ve kalınlık. Kendi `[color]` etiketi olan
  cevap kendi rengini korur.
- **Düzenle** editörü açar. Editör, botun gerçekte gönderdiği BBCode'u varsayılan renk ve kalınlıkla birlikte gösterir;
  böylece onları da değiştirebilirsin. Araç çubuğu kalın, italik, altı çizili, üstü çizili, bağlantı ve renk ekler; alttaki
  düğmeler bu metnin desteklediği değişkenleri ekler; önizleme TeamSpeak'te nasıl görüneceğini gösterir.
- **Sıfırla** varsayılan metni geri getirir. Varsayılanı değiştirmeden kaydetmek de metni varsayılanda bırakır.

TeamSpeak BBCode: `[b]`, `[i]`, `[u]`, `[s]`, `[color=#FF0000]`, `[url=https://…]yazı[/url]`.

## Değişkenler

| Nerede | Değişkenler |
|---|---|
| Karşılama mesajı | `{user}` `{server}` `{channel}` `{time}` `{date}` |
| Duyurular | `{server}` `{channel}` `{time}` |
| Şarkı → bot adı / açıklaması | `{title}` `{artist}` `{song}` `{user}` `{bot}` |
| Kanal adında bilgi | `{online}` `{record}` `{max}` `{time}` `{date}` |
| Geçici kanal adı | `{user}` `{n}` |
| Rütbe atlama mesajı | `{user}` `{hours}` |
| Özel metin komutları | `{user}` `{args}` `{time}` `{date}` |
| Bot metinleri | her metnin editöründe düğme olarak görünür |

`{artist}` ve `{song}`, *Sanatçı - Şarkı (Official Video)* gibi başlıklardan ayrılır; ayraç yoksa `{artist}` boştur ve
`{song}` başlığın tamamıdır. `{record}`, o sunucuda aynı anda görülen en yüksek çevrimiçi sayısıdır.

## Özel komutlar

Panel → Eklentiler → **Özel komutlar**. İki türü var:

- **Metin** — sabit bir cevap verir, ör. `!discord` → `Bize katıl: https://discord.gg/…`. BBCode ve yukarıdaki değişkenler
  çalışır.
- **Yönlendirme** — başka bir komutu çalıştırır, ör. `!sarki` → `!ara`, ya da `!lofi` → `!radyo Lofi Hip Hop`.

Her komut silinmeden kapatılabilir ve avatar kartında görünen bir açıklama verilebilir. Özel komut adları
yerleşik komutlarla aynı olamaz.

## Yerleşik komutları yeniden adlandırma

Bir eklentiyi aç → özellik kartı → **Komutlar**, ya da **Genel komutlar** kartı. Yeni adı yaz; alandan çıkınca ya da Enter'a
basınca kaydedilir. Eski ad çalışmaya devam eder. Alanı boş bırakırsan varsayılana döner. Cevaplar, `!yardim` ve avatar
kartı yeni adları kendiliğinden gösterir.

## Avatar

*Avatar* eklentisi botun TeamSpeak avatarını üç durum için ayrı ayrı ayarlar:

| Durum | Seçenekler |
|---|---|
| Boşta | komut kartı · kendi resmim · avatar yok |
| Müzik çalarken | şarkı kapağı · kendi resmim · boştaki avatar |
| Radyo çalarken | boştaki avatar · kendi resmim |

**Komut kartı**, açık eklentilerin komutlarını, özel komutlarını ve radyo varsa `!radyo`'yu listeler. Açıklamaları Bot
metinlerindeki `avatar.*` satırlarıdır. **Kendi resimlerin** eklenti penceresinden yüklenir ve 300 piksele küçültülür.
TeamSpeak sunucuları avatar boyutunu sınırlar (`i_client_max_avatar_filesize`); yükleme reddedilirse log bunu yazar ve eski
avatar kalır.
