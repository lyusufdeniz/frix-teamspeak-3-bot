# Scriptler

Scriptler, kendi sohbet komutlarını ekleyen ve olaylara tepki veren küçük JavaScript programlarıdır. Panel → Eklentiler →
**Scriptler** → *Yeni script* ile yazılır. Yeni bir script açıklamalı bir örnekle başlar.

- Açık her script **tüm botlarda** çalışır.
- Scriptler yalnızca `frix` nesnesini görür: dosya, ağ ya da sistem erişimi yok.
- Her çağrı **2 saniye**, **32 MB** bellek ve 500.000 adımla sınırlıdır; sonsuz döngüler durdurulur.
- Kaydederken önce sözdizimi kontrol edilir; sözdizimi hatası olan script kaydedilmez.
- Çalışma hataları scriptin yanında ve `frix.log` satırlarınla birlikte **Script logları** penceresinde görünür.
- Yerleşik komut adları bir script tarafından alınamaz.

## API

| Çağrı | Ne yapar |
|---|---|
| `frix.command(ad, (args, ctx) => metin)` | `!ad` komutunu kaydeder. `args` komuttan sonraki metindir. Dönen metin cevaptır ve komutun yazıldığı yere (kanal, özel ya da sunucu sohbeti) gönderilir. Cevap istemiyorsan hiçbir şey döndürme. |
| `frix.on(olay, (e) => { … })` | Bir olayı dinler (aşağıya bak). |
| `frix.sendChannel(metin)` | Botun kanalına mesaj. |
| `frix.sendServer(metin)` | Tüm sunucuya mesaj. |
| `frix.sendPrivate(id, metin)` | Bir istemciye özel mesaj (istemci ID'si). |
| `frix.poke(id, metin)` | Bir istemciyi dürter. |
| `frix.clients()` | Çevrimiçi kişiler: `id`, `uid`, `name`, `channel`, `channelId`, `away`, `groups`. |
| `frix.store.get(anahtar)` / `frix.store.set(anahtar, değer)` | Yeniden başlatmada kaybolmayan, script başına veri (JSON, anahtar başına en fazla 10 KB). |
| `frix.log(değer)` | Script loguna yazar. |
| `frix.botName`, `frix.serverName` | Botun ve TeamSpeak sunucusunun adı. |

Mesajlar [Bot metinleri](#customization)'ndeki varsayılan renkle gönderilir; BBCode çalışır.

Bir komutun `ctx` değeri: `id`, `uid`, `user`, `channel`, `target` (`channel`, `private` ya da `server`).

## Olaylar

| Olay | Ne zaman | `e` |
|---|---|---|
| `connect` | Biri sunucuya bağlanınca | `id`, `uid`, `user` |
| `channel` | Biri bir kanala girince | `id`, `uid`, `user`, `channelId`, `channel` |
| `message` | Botun gördüğü her sohbet mesajında | `id`, `uid`, `user`, `text` |
| `song` | Bir şarkı ya da radyo başlayınca | `title`, `radio` |
| `tick` | 10 saniyede bir | `time` |

Bot bağlandıktan sonraki ilk 15 saniyedeki girişler (zaten çevrimiçi olanlar) yok sayılır.

## Örnekler

**Discord bağlantısı ve zar komutu**

```js
frix.command('discord', () => 'Bize katıl: [url=https://discord.gg/ornek]discord.gg/ornek[/url]');

frix.command('salla', (args, ctx) => {
  const max = parseInt(args) || 100;
  return ctx.user + ' ' + (1 + Math.floor(Math.random() * max)) + ' attı (1-' + max + ')';
});
```

**Lobiye gireni karşıla**

```js
frix.on('channel', (e) => {
  if (e.channel === 'Lobi') frix.sendPrivate(e.id, 'Hoş geldin ' + e.user + '! Komutlar için !yardim yaz.');
});
```

**Şarkıları say, her 100.'yü duyur**

```js
frix.on('song', (e) => {
  const n = (frix.store.get('songs') || 0) + 1;
  frix.store.set('songs', n);
  if (n % 100 === 0) frix.sendChannel('🎉 ' + n + '. şarkı: ' + e.title);
});
```

**Uzakta olanları listele**

```js
frix.command('uzakta', () => {
  const away = frix.clients().filter((c) => c.away).map((c) => c.name);
  return away.length ? 'Uzakta: ' + away.join(', ') : 'Kimse uzakta değil.';
});
```
