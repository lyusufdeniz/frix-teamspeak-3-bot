# Komutlar

Komut adları panel diline göre gelir: panel Türkçe ise **Türkçe**, diğer tüm dillerde **İngilizce** adlar. Türkçe adlar her zaman çalışır ve her komutun adı panelden değiştirilebilir (Eklentiler → bir eklenti → *Komutlar*, ya da *Genel komutlar* kartı). Eklentisi kapalı bir komut "bu özellik kapalı" cevabını verir; `!yardim` yalnızca açık eklentilerin komutlarını listeler.

Komutlar botun kanalında, botla özel sohbette ve sunucu sohbetinde çalışır.

| Komut | İngilizce adı | Eklenti | Ne yapar |
|---|---|---|---|
| `!ara <şarkı ya da link>` | `!play` | müzik | YouTube'da arar ya da linki açar, çalar veya kuyruğa ekler |
| `!link <link>` | `!link` | müzik | Linki doğrudan çalar (YouTube, SoundCloud, Twitch, yayın adresi…) |
| `!dosya <ad>` | `!file` | müzik | Panel kütüphanesinden bir dosyayı kuyruğa ekler |
| `!radyo [isim]` | `!radio` | radyo | Paneldeki bir radyoyu çalar ya da radyoları listeler |
| `!gec` | `!next` | müzik / radyo | Sonraki şarkı (oylamayla geç açıksa oy verir) |
| `!skip` | `!skip` | müzik / radyo | Geç ile aynı |
| `!dur` | `!pause` | müzik / radyo | Duraklat / devam ettir |
| `!duraklat` | `!hold` | müzik / radyo | Duraklat |
| `!devam` | `!resume` | müzik / radyo | Devam ettir |
| `!bitir` | `!stop` | müzik / radyo | Durdurur ve kuyruğu temizler |
| `!kapat` | `!end` | müzik / radyo | Bitir ile aynı |
| `!ses [0-100]` | `!vol` | müzik / radyo | Ses seviyesini gösterir ya da ayarlar |
| `!calan` | `!np` | müzik / radyo | Çalan şarkı |
| `!siradaki` | `!queue` | müzik / radyo | Kuyruğu gösterir |
| `!temizle` | `!clear` | müzik / radyo | Kuyruğu temizler (çalan şarkı devam eder) |
| `!liste [kaydet\|cal\|ekle\|goster\|sil] <ad>` | `!playlist` | müzik | Çalma listeleri: kuyruğu kaydet, çal, çalanı ekle, göster, sil; tek başına listeleri gösterir |
| `!fav [sil <no>]` | `!fav` | favoriler | Çalan şarkıyı favorilerine ekler (ya da birini siler) |
| `!favs [liste]` | `!favs` | favoriler | Favorilerini kuyruğa ekler (ya da listeler) |
| `!istatistik` | `!stats` | müzik | Son 30 günün en çok çalınanları ve en çok şarkı açanları |
| `!soz [şarkı]` | `!lyrics` | eğlence (sözler) | Çalan (ya da başka) şarkının sözleri, özel mesajla |
| `!anket soru \| a \| b` | `!poll` | eğlence (anket) | Anket başlatır (`bitir` ile biter); kanala gönderilir |
| `!oy <no>` | `!vote` | eğlence (anket) | Ankette oy ver; güncel yüzdeler kanalda paylaşılır |
| `!hava <şehir>` | `!weather` | eğlence (hava) | Hava durumu |
| `!doviz <kod> [miktar]` | `!fx` | eğlence (döviz) | Döviz kuru |
| `!dolar [miktar]` | `!usd` | eğlence (döviz) | Dolar kuru |
| `!euro [miktar]` | `!eur` | eğlence (döviz) | Euro kuru |
| `!zar [2d6]` | `!dice` | eğlence (zar) | Zar atar |
| `!yazitura` | `!coin` | eğlence (zar) | Yazı tura atar |
| `!sec a \| b \| c` | `!pick` | eğlence (zar) | Seçeneklerden birini seçer |
| `!kayit <isim>` | `!register` | kayıt | Bekleyen kullanıcıyı kaydeder (yalnızca yetkili gruplar) |
| `!sure` | `!time` | rütbeler | Sunucudaki toplam süren ve sıran |
| `!top` | `!top` | rütbeler | En aktif kullanıcılar |
| `!gel` | `!come` | — | Botu kanalına çağırır |
| `!gorulme <isim>` | `!lastseen` | — | Biri şu an çevrimiçi mi, değilse en son ne zaman görüldü |
| `!yardim` | `!help` | — | Açık eklentilerin tüm komutları |

## Örnekler

- `!ara tarkan kuzu kuzu` — YouTube'da arar, ilk sonucu kuyruğa ekler
- `!liste kaydet rock` · `!liste cal rock` · `!liste ekle rock` · `!liste goster rock` · `!liste sil rock`
- `!anket Bu akşam ne oynayalım? | CS2 | LoL | Valorant` → herkes `!oy 2` yazar; `!anket bitir` ile biter
- `!hava istanbul` · `!doviz EUR 50` · `!zar 2d6` · `!sec pizza | burger | sushi`
- `!fav` · `!favs` · `!favs liste` · `!fav sil 3`
- `!gorulme Ahmet` · `!gel` · `!dosya intro`

TS3AudioBot'un kendi komutları (`!play`, `!add`, `!list`, `!volume`…) da vardır; onlar da aynı eklenti anahtarlarına bağlıdır.
