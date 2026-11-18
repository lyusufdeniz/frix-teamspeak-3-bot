# Frix Panel API

All endpoints live under `/panel/api/`. Bodies and responses are JSON (camelCase).
Authentication uses an HttpOnly session cookie (`frix_session`, SameSite=Strict) set by `login`/`setup`.

**Every non-GET request must send the header `X-Panel: 1`** (CSRF protection). Send `Content-Type: application/json` for JSON bodies.

Errors: HTTP 4xx/5xx with `{"error": "<code>"}`. Common codes:
`unauthorized` (401 → show login), `forbidden` (403, role too low), `csrf`, `bad_credentials`, `locked` (+`minutes`),
`bad_setup_code`, `already_setup`, `bad_username`, `weak_password`, `wrong_password`, `duplicate` (409),
`bot_offline` (409), `bot_not_found`, `song_not_found`, `radio_not_found`, `bad_bot_name`, `bad_nickname`,
`cannot_remove_current`, `cannot_delete_self`, `cannot_demote_self`, `bad_cookie_file`, `bad_repo`, `no_repo`, `no_update`,
`timeout`, or a free-text (already translated Turkish) message.

Roles: `viewer` < `dj` < `admin`.

## Public

| Method | Path | Body | Response |
|---|---|---|---|
| GET | `state` | – | `{setupRequired, setupNeedsCode, user: {name, role} \| null, lang: "tr"\|"en", version}` |
| POST | `setup` | `{user, pass, code?, lang?}` | `{ok}` and logs in. `code` is required only when `setupNeedsCode` is true (remote setup; code is in `KURULUM_KODU.txt` next to the bot and in the logs). |
| POST | `login` | `{user, pass}` | `{ok}` |
| POST | `logout` | – | `{ok}` |

Username: 3-32 chars, letters/digits/`_-.`. Password: 8-200 chars.

## viewer+

| Method | Path | Body | Response |
|---|---|---|---|
| POST | `me/password` | `{old, new}` | `{ok}` (other sessions of this user are logged out) |
| GET | `bots` | – | `Bot[]` (see below) |
| GET | `bots/{name}/queue` | – | `{index, items: [{index, title, type, link, requestedBy}]}` |
| GET | `radios` | – | `[{id, name, url, genre}]` |

```jsonc
// Bot
{
  "name": "default",              // template / config name (id used in URLs)
  "status": "connected",          // "offline" | "connecting" | "connected"
  "run": true,                    // auto start with the program
  "address": "localhost:9987",
  "nickname": "Frix",
  "channel": "",                  // "" = default channel, "/5" = channel id, or channel name/path
  "commander": true,              // channel commander
  "bitrate": 64,
  "hasPassword": false,           // server password set
  "plugins": ["welcome"],         // enabled plugin ids
  "live": null | {                // null when the bot is not running
    "playing": null | {
      "title": "...", "link": "https://youtu.be/..." | null,
      "thumbnail": "https://i.ytimg.com/vi/<id>/mqdefault.jpg" | null,
      "radio": "Radio name" | null,  // set when a radio station is playing
      "position": 12.3, "length": 242.0,   // seconds; length 0 = unknown / live
      "paused": false, "requestedBy": "user"
    },
    "loading": null | { "title": "...", "seconds": 1.4 },   // a song is being resolved/started
    "volume": 30.0, "queueCount": 3, "queueIndex": 0,
    "repeat": "off" | "one" | "all", "random": false
  }
}
```

| GET | `stats?days=30&bot=` | – | `{days, plays, topSongs:[{title,type,id,count,link,thumbnail}], topUsers:[{user,count}], online:[{name,seconds,lastSeen}]}` (`days=0` = all time) |
| GET | `playlists` | – | `[{id, name, count, createdBy}]` |
| GET | `playlists/{id}` | – | `[{index, type, id, title, link, thumbnail}]` |

## dj+

| Method | Path | Body | Response |
|---|---|---|---|
| GET | `search?q=...&bot=name` | – | `[{id, title, type, thumbnail}]` (YouTube, max 15; needs at least one running bot) |
| POST | `bots/{name}/play` | `{query}` or `{id, title}` (+ `now: true` to play immediately instead of queueing) | `{ok, title}` – returns immediately; loading progress is visible in `bots[].live.loading` |
| POST | `bots/{name}/radio` | `{id}` | `{ok, name}` |
| POST | `bots/{name}/control` | `{action, value?}` | `{ok}` |

| POST | `playlists` | `{name, bot}` | saves the bot's current queue as a playlist (overwrites the same name) → `{ok, id, count}` |
| POST | `playlists/{id}/play` | `{bot}` | queues the playlist → `{ok, count}` |
| DELETE | `playlists/{id}/items/{index}` | – | removes one song |
| DELETE | `playlists/{id}` | – | deletes the playlist |

`control` actions: `pause` (toggle; or `value:"true"/"false"`), `next`, `previous`, `stop`, `clear`,
`seek` (`value` seconds), `volume` (`value` 0-100), `jump` (`value` queue index), `remove` (`value` queue index),
`move` (`from`, `to` indexes), `repeat` (`value` `off|one|all`), `random` (`value` `"true"|"false"`).

## admin

### Bots
| Method | Path | Body |
|---|---|---|
| POST | `bots` | `{name, address, nickname?, channel?, serverPassword?, commander?, run?, bitrate?, start?}` – name: 2-24 chars `[A-Za-z0-9_-]` |
| PUT | `bots/{name}` | any of `{address, nickname, channel, serverPassword, commander, run, bitrate, moveNow}` → `{ok, restarted}` |
| DELETE | `bots/{name}` | – |
| POST | `bots/{name}/start` \| `stop` \| `restart` | – |

### Radios
| Method | Path | Body |
|---|---|---|
| POST | `radios` | `{name, url, genre?}` → `{id}` (url: http/https; .m3u/.pls/.xspf/.m3u8 and direct streams are supported) |
| POST | `radios/import` | `{text}` (contents of an .m3u file with `#EXTINF` names) → `{added, skipped}` |
| PUT | `radios/{id}` | `{name, url, genre?}` |
| DELETE | `radios/{id}` | – |

TeamSpeak usage: `!radyo <name>`.

### Plugins (per bot)
| Method | Path | Body |
|---|---|---|
| GET | `plugins/{bot}` | → `[{id, name:{tr,en}, description:{tr,en}, enabled, config:{...}, schema:[Field]}]` |
| PUT | `plugins/{bot}/{id}` | `{enabled, config:{key: value}}` |

`Field = {key, type, label:{tr,en}, help:{tr,en}|null, default, options: string[]|null}`;
`type` is one of `text`, `textarea`, `number`, `bool`, `select` (use `options`), `radio` (pick a radio station name from `GET radios`).
Missing config keys fall back to `default`.

Current plugins: `welcome`, `advertisement`, `alonepause`, `idleradio`, `afkmover`, `namefilter`, `supportnotify`, `channelinfo`, `registration`, `tempchannel`, `ranks`, `channelguard`, `antiflood`, `voteskip`, `djmode`, `scheduled`, `fun` (`fun` is enabled by default).

### Custom TeamSpeak commands
| Method | Path | Body |
|---|---|---|
| GET | `commands` | → `{custom:[{id, trigger, kind, value, enabled, description}], builtin:[{trigger, usage, alias, keepOld}]}` |
| PUT | `commands/builtin/{trigger}` | `{alias, keepOld}` – rename a built-in command (`alias` empty = reset). `keepOld: false` disables the original name. |
| POST | `commands/builtin/defaults` | `{lang}` – set all built-in command names to a language's defaults (`tr` = original names) → `{ok, skipped}` |
| POST | `commands` | `{trigger, kind: "text"\|"alias", value, enabled?, description?}` |
| PUT | `commands/{id}` | same |
| DELETE | `commands/{id}` | – |

`text`: the bot replies with `value` (variables `{user} {args} {time} {date}`, BBCode allowed).
`alias`: `!trigger args` runs `value args` (value must start with `!`, e.g. `!ara`).

`text`/`alias` triggers may not clash with built-in command names or their aliases.

| GET | `avatar` | → PNG of the bot's current idle avatar (generated from commands, texts and radios) |
|---|---|---|

### Bot reply texts (translations / customisation)
| Method | Path | Body |
|---|---|---|
| GET | `texts?lang=tr` | → `[{key, value, defaultValue, custom}]` |
| PUT | `texts` | `{lang, key, value}` (empty value or default value = reset) |

### Users
| Method | Path | Body |
|---|---|---|
| GET | `users` | → `[{id, name, role, created, lastLogin}]` |
| POST | `users` | `{name, pass, role}` |
| PUT | `users/{id}` | `{role?, pass?}` |
| DELETE | `users/{id}` | – |

### Settings & maintenance
| Method | Path | Body / Response |
|---|---|---|
| GET | `settings` | → `{lang, languages, version, updateRepo, os, ytdlp, cookies:{exists, updated}}` |
| PUT | `settings` | `{lang?, updateRepo?}` (`lang` changes the TeamSpeak reply language; `updateRepo` = `owner/repo`) |
| POST | `cookies` | raw text body (`Content-Type: text/plain`) – Netscape cookies.txt |
| DELETE | `cookies` | – |
| POST | `ytdlp/update` | → `{ok, output}` (can take up to a minute) |
| POST | `restart` | restarts the whole program (panel is down for a few seconds) |
| GET | `update/check` | → `{current, latest, available, notes, url, downloadUrl, error}` |
| POST | `update/apply` | downloads and restarts into the new version |
| GET | `logs?since=<seq>` | → `{last, items:[{seq, time, level: "Info"\|"Warn"\|"Error"\|"Fatal", message}]}` |
| POST | `logs/clear` | – |
| GET | `audit` | → last 300 `[{time, user, ip, action, detail}]` |
